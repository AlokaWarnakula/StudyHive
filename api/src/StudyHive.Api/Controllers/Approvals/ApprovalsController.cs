using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using StudyHive.Api.Common;
using StudyHive.Api.Contracts;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;
using StudyHive.Api.Security;
using StudyHive.Api.Services;

namespace StudyHive.Api.Controllers.Approvals;

/// <summary>
/// S4: the librarian's approve / reject / request-revision decision on a Proposed quotation — the
/// high-impact path in the whole system (W-03 queue, W-04 review).
///
/// An Approved decision is ONE database transaction: it books every proposed slot through
/// <see cref="IRoomBookingService"/> (the room exclusion constraint is the last line of defence),
/// links each Room quotation line to its new booking, reserves every requested item through
/// <see cref="IConsumableStockService.ReserveAsync"/> (chk_never_oversold is the last line of
/// defence), moves the quotation, request and workflow to Approved, and writes the
/// approval_decisions and audit_logs rows. Any failure rolls all of it back: a room clash or an
/// oversell is a 409 and nothing is written. The quotation row is locked FOR UPDATE first, so two
/// librarians deciding the same quotation at once serialize and the second gets a 409.
///
/// Every decision also queues the student's email_notifications row inside that same transaction,
/// so an email exists only if the decision committed.
///
/// The queue is keyed by quotation id: a quotation awaiting a decision has no decision row yet.
/// </summary>
[ApiController]
[Route("api/approvals")]
[Authorize]
public sealed class ApprovalsController(
    StudyHiveDbContext db,
    IRoomBookingService roomBookingService,
    IConsumableStockService stockService) : ControllerBase
{
    /// <summary>The workflow step whose logged input is the exact proposal the Validation agent
    /// priced — its proposedSlots are the slots an approval books.</summary>
    private const int ValidationStepNumber = 4;

    private static readonly JsonSerializerOptions LogJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Submit a decision on a Proposed quotation. Backs W-04.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Librarian)]
    [ProducesResponseType(typeof(ApprovalDecisionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateApprovalDecisionRequest request, CancellationToken ct)
    {
        var decision = request.Decision!.Value;
        var comments = string.IsNullOrWhiteSpace(request.Comments) ? null : request.Comments.Trim();
        if (decision != ApprovalDecisionType.Approved && comments is null)
        {
            ModelState.AddModelError(nameof(request.Comments), "Comments are required when rejecting or requesting a revision.");
            return ValidationProblem(ModelState);
        }

        var userId = User.GetUserId();
        var bookingRequestId = await db.Quotations.AsNoTracking()
            .Where(q => q.Id == request.QuotationId)
            .Select(q => (Guid?)q.BookingRequestId)
            .SingleOrDefaultAsync(ct);
        if (bookingRequestId is null)
        {
            return NotFound();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Row locks, request first then quotation — the same order BookingRequestsController.Cancel
        // takes them. A student's cancel and a librarian's decision on the same request serialize
        // here, and a concurrent decision on the same quotation waits, then sees it is no longer
        // Proposed and gets the 409 below.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM booking_requests WHERE id = {bookingRequestId.Value} FOR UPDATE", ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM quotations WHERE id = {request.QuotationId} FOR UPDATE", ct);

        var quotation = await db.Quotations
            .Include(q => q.LineItems)
            .SingleAsync(q => q.Id == request.QuotationId, ct);

        if (quotation.Status != QuotationStatus.Proposed)
        {
            return Conflict("already-decided", "Quotation already decided",
                $"This quotation is '{quotation.Status}'; only a Proposed quotation can be decided.");
        }

        var bookingRequest = await db.BookingRequests
            .Include(r => r.Items)
            .SingleAsync(r => r.Id == quotation.BookingRequestId, ct);

        // AUDIT C-01: a request the student cancelled (or any request no longer waiting for a
        // librarian) can never be brought back by deciding its quotation.
        if (bookingRequest.Status != BookingRequestStatus.PendingApproval)
        {
            return Conflict("request-not-pending", "Request is not waiting for approval",
                $"This request is '{bookingRequest.Status}'; only a request awaiting approval can be decided.");
        }
        var execution = await db.WorkflowExecutions
            .Where(w => w.BookingRequestId == bookingRequest.Id)
            .OrderByDescending(w => w.StartedAt)
            .FirstOrDefaultAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var auditDetails = new Dictionary<string, object?>
        {
            ["bookingRequestId"] = bookingRequest.Id,
            ["decision"] = decision.ToString(),
            ["comments"] = comments,
            ["totalAmount"] = quotation.TotalAmount,
        };

        if (decision == ApprovalDecisionType.Approved)
        {
            var failure = await ApproveAsync(quotation, bookingRequest, execution, userId, now, auditDetails, ct);
            if (failure is not null)
            {
                await RollbackAsync(transaction);
                return failure;
            }
        }
        else
        {
            quotation.Status = QuotationStatus.Rejected;
            bookingRequest.Status = decision == ApprovalDecisionType.Rejected
                ? BookingRequestStatus.Rejected
                : BookingRequestStatus.RevisionRequested;
            if (execution is not null)
            {
                execution.Status = WorkflowStatus.Rejected;
                execution.CompletedAt = now;
            }
            auditDetails["releasedReservationIds"] = await stockService.ReleasePendingAsync(
                bookingRequest.Items.Select(i => i.Id).ToList(), ct);
        }

        quotation.UpdatedAt = now;
        bookingRequest.UpdatedAt = now;
        if (execution is not null) execution.UpdatedAt = now;

        var decisionRow = new ApprovalDecision
        {
            Id = Guid.NewGuid(),
            QuotationId = quotation.Id,
            DecidedBy = userId,
            DecidedByRole = User.GetRole() ?? Roles.Librarian,
            Decision = decision,
            Comments = comments,
            DecidedAt = now,
        };
        db.ApprovalDecisions.Add(decisionRow);
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = decision switch
            {
                ApprovalDecisionType.Approved => "QuotationApproved",
                ApprovalDecisionType.Rejected => "QuotationRejected",
                _ => "RevisionRequested",
            },
            EntityType = "Quotation",
            EntityId = quotation.Id,
            Details = JsonSerializer.Serialize(auditDetails, LogJsonOptions),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            CreatedAt = now,
        });

        // The student's email, queued in this transaction: it exists only if the decision commits.
        var studentEmail = await db.StudentProfiles
            .Where(p => p.Id == bookingRequest.StudentId)
            .Select(p => p.User.Email)
            .SingleAsync(ct);
        db.EmailNotifications.Add(EmailNotification.ForBookingRequest(
            studentEmail, EmailTemplates.ForDecision(decision), bookingRequest.Id, now));

        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (FindPostgresError(ex) is { } pg &&
            pg.SqlState is PostgresErrorCodes.ExclusionViolation or PostgresErrorCodes.CheckViolation)
        {
            await RollbackAsync(transaction);
            return Conflict("conflict", "Approval could not be committed", pg.MessageText);
        }

        return CreatedAtAction(nameof(GetById), new { id = quotation.Id }, ApprovalDecisionResponse.From(decisionRow));
    }

    /// <summary>The approval queue: quotations with their decision state, pending first. `status` is
    /// Pending (no decision yet) or a decision (Approved, Rejected, RevisionRequested). Backs W-03.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Librarian)]
    [ProducesResponseType(typeof(PagedResult<ApprovalQueueItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List([FromQuery] PageQuery query, [FromQuery] string? status, CancellationToken ct)
    {
        IQueryable<Quotation> quotations = db.Quotations.AsNoTracking()
            .Where(q => q.Status != QuotationStatus.Draft);

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (string.Equals(status, ApprovalQueueItemResponse.Pending, StringComparison.OrdinalIgnoreCase))
            {
                // A cancel supersedes the quotation; the request check also hides rows cancelled
                // before that fix (AUDIT C-01, CW-07).
                quotations = quotations.Where(q => q.Status == QuotationStatus.Proposed
                    && q.BookingRequest.Status == BookingRequestStatus.PendingApproval);
            }
            else if (Enum.TryParse<ApprovalDecisionType>(status, ignoreCase: true, out var parsed))
            {
                quotations = quotations.Where(q => q.ApprovalDecisions.Any(d => d.Decision == parsed));
            }
            else
            {
                ModelState.AddModelError(nameof(status), $"Unknown status value '{status}'.");
                return ValidationProblem(ModelState);
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{query.Search.Trim()}%";
            quotations = quotations.Where(q => EF.Functions.ILike(q.BookingRequest.Objective, term)
                || EF.Functions.ILike(q.BookingRequest.Student.StudentNumber, term)
                || EF.Functions.ILike(q.BookingRequest.Student.User.FullName, term));
        }

        // Pending first always; sortBy orders within each group.
        var pendingFirst = quotations.OrderBy(q => q.Status == QuotationStatus.Proposed ? 0 : 1);
        var descending = !string.Equals(query.SortDir, "asc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<Quotation>? sorted = query.SortBy?.ToLowerInvariant() switch
        {
            null or "" or "createdat" => descending ? pendingFirst.ThenByDescending(q => q.CreatedAt) : pendingFirst.ThenBy(q => q.CreatedAt),
            "totalamount" => descending ? pendingFirst.ThenByDescending(q => q.TotalAmount) : pendingFirst.ThenBy(q => q.TotalAmount),
            _ => null,
        };
        if (sorted is null)
        {
            ModelState.AddModelError(nameof(query.SortBy), $"Unknown sortBy value '{query.SortBy}'.");
            return ValidationProblem(ModelState);
        }

        var totalItems = await sorted.CountAsync(ct);
        var items = await ProjectQueueItems(sorted.ThenBy(q => q.Id))
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return Ok(PagedResult<ApprovalQueueItemResponse>.Create(items, query.Page, query.PageSize, totalItems));
    }

    /// <summary>One queue item by quotation id, with its line items. Backs W-04.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = Roles.Librarian)]
    [ProducesResponseType(typeof(ApprovalDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var item = await ProjectQueueItems(db.Quotations.AsNoTracking().Where(q => q.Id == id)).SingleOrDefaultAsync(ct);
        if (item is null)
        {
            return NotFound();
        }

        var lines = await db.QuotationLineItems.AsNoTracking()
            .Where(l => l.QuotationId == id)
            .OrderBy(l => l.ItemType).ThenBy(l => l.ItemName)
            .Select(l => new ApprovalLineItemResponse
            {
                Id = l.Id,
                ItemType = l.ItemType.ToString(),
                ItemName = l.ItemName,
                RoomId = l.RoomId,
                RoomBookingId = l.RoomBookingId,
                ConsumableId = l.ConsumableId,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                LineTotal = l.LineTotal,
            })
            .ToListAsync(ct);

        return Ok(new ApprovalDetailResponse { Item = item, LineItems = lines });
    }

    /// <summary>The Approved half of the transaction. Returns null when every write succeeded, or
    /// the 409 to send after the caller rolls back.</summary>
    private async Task<IActionResult?> ApproveAsync(
        Quotation quotation,
        BookingRequest bookingRequest,
        WorkflowExecution? execution,
        Guid userId,
        DateTimeOffset now,
        Dictionary<string, object?> auditDetails,
        CancellationToken ct)
    {
        var slots = await LoadProposedSlotsAsync(execution, ct);
        var roomLines = quotation.LineItems.Where(l => l.ItemType == QuotationLineItemType.Room).ToList();
        if (slots is null || slots.Count != roomLines.Count)
        {
            return Conflict("proposal-incomplete", "Proposal cannot be booked",
                "The quotation's room lines do not match the proposal's recorded slots.");
        }

        // RoomBookingService only books for an Approved request (and reads it from the database),
        // so the status changes are written first — still inside this uncommitted transaction.
        quotation.Status = QuotationStatus.Approved;
        bookingRequest.Status = BookingRequestStatus.Approved;
        if (execution is not null)
        {
            execution.Status = WorkflowStatus.Approved;
            execution.CompletedAt = now;
        }

        try
        {
            await db.SaveChangesAsync(ct);

            // Each Room line is linked to the booking for the same room, in start-time order (the
            // agent names a Room line "<room> <start ISO time>", so name order is start order).
            var linesByRoom = roomLines
                .GroupBy(l => l.RoomId)
                .ToDictionary(g => g.Key, g => new Queue<QuotationLineItem>(g.OrderBy(l => l.ItemName, StringComparer.Ordinal)));
            var bookingIds = new List<Guid>();
            foreach (var slot in slots.OrderBy(s => s.StartsAt))
            {
                var booking = await roomBookingService.CreateAsync(slot.RoomId, bookingRequest.Id, slot.StartsAt, slot.EndsAt, ct);
                if (!booking.Succeeded)
                {
                    return Conflict("room-conflict", "Room can no longer be booked", booking.Detail);
                }

                if (!linesByRoom.TryGetValue(slot.RoomId, out var lines) || !lines.TryDequeue(out var line))
                {
                    return Conflict("proposal-incomplete", "Proposal cannot be booked",
                        $"No quotation room line matches the slot in room {slot.RoomName}.");
                }
                line.RoomBookingId = booking.Booking!.Id;
                bookingIds.Add(booking.Booking.Id);
            }

            var reservationIds = new List<Guid>();
            foreach (var item in bookingRequest.Items)
            {
                var reserved = await stockService.ReserveAsync(item.Id, userId, ct);
                if (!reserved.Succeeded)
                {
                    return Conflict(
                        reserved.Outcome == StockOperationOutcome.InsufficientStock ? "insufficient-stock" : "conflict",
                        reserved.Outcome == StockOperationOutcome.InsufficientStock ? "Insufficient stock" : "Stock cannot be reserved",
                        reserved.Detail);
                }
                reservationIds.Add(reserved.Reservation!.Id);
            }

            auditDetails["roomBookingIds"] = bookingIds;
            auditDetails["stockReservationIds"] = reservationIds;
            return null;
        }
        catch (DbUpdateException ex) when (FindPostgresError(ex) is { } pg &&
            pg.SqlState is PostgresErrorCodes.ExclusionViolation or PostgresErrorCodes.CheckViolation)
        {
            return Conflict("conflict", "Approval could not be committed", pg.MessageText);
        }
    }

    /// <summary>The proposal's slots, read back from the Validation step's logged input — the exact
    /// proposal that was validated and priced. Null when the log is missing or unreadable.</summary>
    private async Task<IReadOnlyList<SchedulingSlot>?> LoadProposedSlotsAsync(WorkflowExecution? execution, CancellationToken ct)
    {
        if (execution is null) return null;

        var inputJson = await db.WorkflowStepLogs.AsNoTracking()
            .Where(s => s.WorkflowExecutionId == execution.Id && s.StepNumber == ValidationStepNumber)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => s.InputJson)
            .FirstOrDefaultAsync(ct);
        if (inputJson is null) return null;

        try
        {
            return JsonSerializer.Deserialize<ValidationRequest>(inputJson, LogJsonOptions)?.ProposedSlots;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IQueryable<ApprovalQueueItemResponse> ProjectQueueItems(IQueryable<Quotation> quotations) =>
        quotations.Select(q => new ApprovalQueueItemResponse
        {
            QuotationId = q.Id,
            BookingRequestId = q.BookingRequestId,
            StudentId = q.BookingRequest.StudentId,
            StudentName = q.BookingRequest.Student.User.FullName,
            StudentNumber = q.BookingRequest.Student.StudentNumber,
            StudentEmail = q.BookingRequest.Student.User.Email,
            Objective = q.BookingRequest.Objective,
            GroupSize = q.BookingRequest.GroupSize,
            Version = q.Version,
            RoomFee = q.RoomFee,
            ConsumableCost = q.ConsumableCost,
            TotalAmount = q.TotalAmount,
            BudgetSnapshot = q.BudgetSnapshot,
            WithinBudget = q.WithinBudget,
            Currency = q.Currency,
            QuotationStatus = q.Status,
            CreatedAt = q.CreatedAt,
            Decision = q.ApprovalDecisions
                .OrderByDescending(d => d.DecidedAt)
                .Select(d => new ApprovalDecisionResponse
                {
                    Id = d.Id,
                    QuotationId = d.QuotationId,
                    DecidedBy = d.DecidedBy,
                    DecidedByRole = d.DecidedByRole,
                    Decision = d.Decision,
                    Comments = d.Comments,
                    DecidedAt = d.DecidedAt,
                })
                .FirstOrDefault(),
        });

    private ObjectResult Conflict(string type, string title, string? detail) => Problem(
        type: $"https://studyhive.dev/errors/{type}",
        title: title,
        statusCode: StatusCodes.Status409Conflict,
        detail: detail);

    private static async Task RollbackAsync(IDbContextTransaction transaction) =>
        await transaction.RollbackAsync(CancellationToken.None);

    private static PostgresException? FindPostgresError(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException pg) return pg;
        }
        return null;
    }
}

public sealed class CreateApprovalDecisionRequest
{
    [Required]
    public Guid QuotationId { get; init; }

    [Required]
    public ApprovalDecisionType? Decision { get; init; }

    [MaxLength(2000)]
    public string? Comments { get; init; }
}

public sealed class ApprovalDecisionResponse
{
    public required Guid Id { get; init; }
    public required Guid QuotationId { get; init; }
    public required Guid DecidedBy { get; init; }
    public required string DecidedByRole { get; init; }
    public required ApprovalDecisionType Decision { get; init; }
    public string? Comments { get; init; }
    public required DateTimeOffset DecidedAt { get; init; }

    public static ApprovalDecisionResponse From(ApprovalDecision d) => new()
    {
        Id = d.Id,
        QuotationId = d.QuotationId,
        DecidedBy = d.DecidedBy,
        DecidedByRole = d.DecidedByRole,
        Decision = d.Decision,
        Comments = d.Comments,
        DecidedAt = d.DecidedAt,
    };
}

/// <summary>One approval-queue row: a quotation and its latest decision, if any.</summary>
public sealed class ApprovalQueueItemResponse
{
    public const string Pending = "Pending";

    public required Guid QuotationId { get; init; }
    public required Guid BookingRequestId { get; init; }
    public required Guid StudentId { get; init; }
    /// <summary>Who asked, so the librarian knows whose booking they are deciding.</summary>
    public required string StudentName { get; init; }
    public required string StudentNumber { get; init; }
    public required string StudentEmail { get; init; }
    public required string Objective { get; init; }
    public required int GroupSize { get; init; }
    public required int Version { get; init; }
    public required decimal RoomFee { get; init; }
    public required decimal ConsumableCost { get; init; }
    public required decimal TotalAmount { get; init; }
    public required decimal BudgetSnapshot { get; init; }
    public required bool WithinBudget { get; init; }
    public required string Currency { get; init; }
    public required QuotationStatus QuotationStatus { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public ApprovalDecisionResponse? Decision { get; init; }

    /// <summary>Pending while the quotation awaits a decision, otherwise the latest decision.</summary>
    public string Status => QuotationStatus == QuotationStatus.Proposed
        ? Pending
        : Decision?.Decision.ToString() ?? QuotationStatus.ToString();
}

public sealed class ApprovalLineItemResponse
{
    public required Guid Id { get; init; }
    public required string ItemType { get; init; }
    public required string ItemName { get; init; }
    public Guid? RoomId { get; init; }
    public Guid? RoomBookingId { get; init; }
    public Guid? ConsumableId { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal LineTotal { get; init; }
}

public sealed class ApprovalDetailResponse
{
    public required ApprovalQueueItemResponse Item { get; init; }
    public required IReadOnlyList<ApprovalLineItemResponse> LineItems { get; init; }
}
