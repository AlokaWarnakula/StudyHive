using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StudyHive.Api.Common;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;
using StudyHive.Api.Security;
using StudyHive.Api.Services;

namespace StudyHive.Api.Controllers.BookingRequests;

/// <summary>S1: the booking request lifecycle, Draft through submit/workflow to a terminal status. See DOCS §11 API table.</summary>
[ApiController]
[Route("api/booking-requests")]
[Authorize]
public sealed class BookingRequestsController(
    StudyHiveDbContext db,
    IBookingEligibilityService eligibilityService,
    IWorkflowOrchestrationService workflowOrchestration,
    IWorkflowQueue workflowQueue,
    IConsumableStockService stockService,
    IAuditWriter audit) : ControllerBase
{
    /// <summary>Requests that still count against the weekly quota / can still be acted on by the student.</summary>
    private static readonly BookingRequestStatus[] CancellableStatuses =
    [
        BookingRequestStatus.Draft,
        BookingRequestStatus.Submitted,
        BookingRequestStatus.Processing,
        BookingRequestStatus.PendingApproval,
        BookingRequestStatus.RevisionRequested,
    ];

    [HttpPost]
    [Authorize(Policy = "StudentOnly")]
    [ProducesResponseType(typeof(BookingRequestResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateBookingRequestRequest request, CancellationToken ct)
    {
        var studentProfile = await GetOwnStudentProfileAsync(ct);
        if (studentProfile is null) return NoStudentProfileProblem();

        var itemsProblem = await ValidateItemsOrProblemAsync(request.Items, ct);
        if (itemsProblem is not null) return itemsProblem;

        var bookingRequest = new BookingRequest
        {
            Id = Guid.NewGuid(),
            StudentId = studentProfile.Id,
            Objective = request.Objective.Trim(),
            GroupSize = request.GroupSize,
            PreferredDateFrom = request.PreferredDateFrom,
            PreferredDateTo = request.PreferredDateTo,
            PreferredTimeFrom = request.PreferredTimeFrom,
            PreferredTimeTo = request.PreferredTimeTo,
            SessionsRequired = request.SessionsRequired,
            SessionDurationMinutes = request.SessionDurationMinutes,
            Budget = request.Budget,
            Notes = request.Notes?.Trim(),
            Status = BookingRequestStatus.Draft,
        };

        foreach (var item in request.Items)
        {
            bookingRequest.Items.Add(new BookingRequestItem
            {
                Id = Guid.NewGuid(),
                BookingRequestId = bookingRequest.Id,
                ConsumableId = item.ConsumableId,
                Quantity = item.Quantity,
            });
        }

        db.BookingRequests.Add(bookingRequest);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = bookingRequest.Id }, BookingRequestResponse.From(bookingRequest));
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<BookingRequestResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] PageQuery query, [FromQuery] string? status, CancellationToken ct)
    {
        IQueryable<BookingRequest> requests = db.BookingRequests.AsNoTracking()
            .Include(r => r.Items)
            .Include(r => r.Student).ThenInclude(s => s.User);

        // DOCS §11 API table scopes this list to "Student (own), Librarian" — StoreOfficer has no
        // business need to see other students' requests, so it is explicitly denied rather than
        // silently falling through to "sees everything" (Codex security review, P1).
        if (User.IsInRole(Roles.Student))
        {
            var studentProfile = await GetOwnStudentProfileAsync(ct);
            if (studentProfile is null)
            {
                return Ok(PagedResult<BookingRequestResponse>.Create([], query.Page, query.PageSize, 0));
            }
            requests = requests.Where(r => r.StudentId == studentProfile.Id);
        }
        else if (!IsStaffReader(User))
        {
            return Forbid();
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<BookingRequestStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                ModelState.AddModelError(nameof(status), $"Unknown status value '{status}'.");
                return ValidationProblem(ModelState);
            }
            requests = requests.Where(r => r.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = $"%{query.Search.Trim()}%";
            // Staff look a request up by what it is for or by who asked (name, student number, email).
            requests = requests.Where(r => EF.Functions.ILike(r.Objective, search)
                || EF.Functions.ILike(r.Student.StudentNumber, search)
                || EF.Functions.ILike(r.Student.User.FullName, search)
                || EF.Functions.ILike(r.Student.User.Email, search));
        }

        var sortDescending = !string.Equals(query.SortDir, "asc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<BookingRequest>? sorted = query.SortBy?.ToLowerInvariant() switch
        {
            null or "" or "createdat" => sortDescending ? requests.OrderByDescending(r => r.CreatedAt) : requests.OrderBy(r => r.CreatedAt),
            "status" => sortDescending ? requests.OrderByDescending(r => r.Status) : requests.OrderBy(r => r.Status),
            "budget" => sortDescending ? requests.OrderByDescending(r => r.Budget) : requests.OrderBy(r => r.Budget),
            _ => null,
        };
        if (sorted is null)
        {
            ModelState.AddModelError(nameof(query.SortBy), $"Unknown sortBy value '{query.SortBy}'.");
            return ValidationProblem(ModelState);
        }
        requests = sorted;

        var totalItems = await requests.CountAsync(ct);
        var page = await requests
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        // Summaries only for the rows already filtered to what this caller may see.
        var pageIds = page.Select(r => r.Id).ToList();
        var summaries = await LoadS4SummariesAsync(pageIds, ct);
        var roomBookings = await LoadRoomBookingsAsync(pageIds, ct);
        var items = page
            .Select(r =>
            {
                var (quotation, decision) = summaries.GetValueOrDefault(r.Id);
                return BookingRequestResponse.From(r, null, quotation, decision, roomBookings.GetValueOrDefault(r.Id));
            })
            .ToList();

        return Ok(PagedResult<BookingRequestResponse>.Create(items, query.Page, query.PageSize, totalItems));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BookingRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var bookingRequest = await db.BookingRequests.AsNoTracking().Include(r => r.Items)
            .Include(r => r.Student).ThenInclude(s => s.User)
            .SingleOrDefaultAsync(r => r.Id == id, ct);
        if (bookingRequest is null) return NotFound();

        if (!await AuthorizeOwnerAsync(bookingRequest.StudentId, ct)) return Forbid();

        var latestWorkflowId = await db.WorkflowExecutions.AsNoTracking()
            .Where(w => w.BookingRequestId == id)
            .OrderByDescending(w => w.StartedAt)
            .Select(w => (Guid?)w.Id)
            .FirstOrDefaultAsync(ct);

        var (latestQuotation, latestDecision) = (await LoadS4SummariesAsync([id], ct)).GetValueOrDefault(id);
        var roomBookings = (await LoadRoomBookingsAsync([id], ct)).GetValueOrDefault(id);
        return Ok(BookingRequestResponse.From(bookingRequest, latestWorkflowId, latestQuotation, latestDecision, roomBookings));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "StudentOnly")]
    [ProducesResponseType(typeof(BookingRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, UpdateBookingRequestRequest request, CancellationToken ct)
    {
        var exists = await db.BookingRequests.AsNoTracking().AnyAsync(r => r.Id == id, ct);
        if (!exists) return NotFound();

        // The request row lock every status writer takes, then the read: a cancel or submit that
        // commits meanwhile is seen, never overwritten back to Draft.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM booking_requests WHERE id = {id} FOR UPDATE", ct);
        var bookingRequest = await db.BookingRequests.Include(r => r.Items).SingleAsync(r => r.Id == id, ct);

        if (!await AuthorizeOwnerAsync(bookingRequest.StudentId, ct, staffAllowed: false)) return Forbid();

        // AUDIT C-02: a request the librarian sent back ("ask for a change") is edited like a
        // draft and becomes a Draft again until it is resent.
        if (bookingRequest.Status is not (BookingRequestStatus.Draft or BookingRequestStatus.RevisionRequested))
        {
            return Problem(
                type: "https://studyhive.dev/errors/conflict",
                title: "Only draft requests can be edited",
                statusCode: StatusCodes.Status409Conflict,
                detail: $"This request is '{bookingRequest.Status}' and can no longer be edited.");
        }

        var itemsProblem = await ValidateItemsOrProblemAsync(request.Items, ct);
        if (itemsProblem is not null) return itemsProblem;

        bookingRequest.Objective = request.Objective.Trim();
        bookingRequest.GroupSize = request.GroupSize;
        bookingRequest.PreferredDateFrom = request.PreferredDateFrom;
        bookingRequest.PreferredDateTo = request.PreferredDateTo;
        bookingRequest.PreferredTimeFrom = request.PreferredTimeFrom;
        bookingRequest.PreferredTimeTo = request.PreferredTimeTo;
        bookingRequest.SessionsRequired = request.SessionsRequired;
        bookingRequest.SessionDurationMinutes = request.SessionDurationMinutes;
        bookingRequest.Budget = request.Budget;
        bookingRequest.Notes = request.Notes?.Trim();
        bookingRequest.Status = BookingRequestStatus.Draft;
        bookingRequest.UpdatedAt = DateTimeOffset.UtcNow;

        db.BookingRequestItems.RemoveRange(bookingRequest.Items);
        bookingRequest.Items.Clear();
        foreach (var item in request.Items)
        {
            // Added through the DbSet: a new item added only to the tracked collection with a preset
            // Guid key is treated as an existing row and UPDATEd (0 rows → 500).
            db.BookingRequestItems.Add(new BookingRequestItem
            {
                Id = Guid.NewGuid(),
                BookingRequestId = bookingRequest.Id,
                ConsumableId = item.ConsumableId,
                Quantity = item.Quantity,
            });
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(BookingRequestResponse.From(bookingRequest));
    }

    /// <summary>Cancel — preserves the row for audit history (DOCS: requests are never physically
    /// deleted). A terminal request (Completed/Cancelled/Rejected/Failed) cannot be cancelled again.
    ///
    /// One transaction, holding the request row lock that ApprovalsController.Create and the
    /// workflow's final step also take, so a cancel and an approval can never both win (AUDIT C-01):
    /// <list type="bullet">
    /// <item>Not yet approved: the Proposed quotation becomes Superseded (it leaves the approval
    /// queue), Pending stock reservations are released, and a workflow still running or waiting for
    /// approval ends Rejected with CANCELLED_BY_STUDENT (C-01, CW-07).</item>
    /// <item>Approved, before its first room booking starts: the room bookings are Cancelled, each
    /// Reserved reservation is released with a Release stock transaction, and a BookingCancelled
    /// email is queued (C-07). Once a booking has started: 409.</item>
    /// </list></summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "StudentOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var exists = await db.BookingRequests.AsNoTracking().AnyAsync(r => r.Id == id, ct);
        if (!exists) return NotFound();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM booking_requests WHERE id = {id} FOR UPDATE", ct);

        var bookingRequest = await db.BookingRequests.Include(r => r.Items).SingleAsync(r => r.Id == id, ct);
        if (!await AuthorizeOwnerAsync(bookingRequest.StudentId, ct, staffAllowed: false)) return Forbid();

        var now = DateTimeOffset.UtcNow;
        var previousStatus = bookingRequest.Status;
        if (bookingRequest.Status == BookingRequestStatus.Approved)
        {
            var conflict = await CancelApprovedAsync(bookingRequest, now, ct);
            if (conflict is not null) return conflict;
        }
        else if (CancellableStatuses.Contains(bookingRequest.Status))
        {
            await CancelUndecidedAsync(bookingRequest, now, ct);
        }
        else
        {
            return Problem(
                type: "https://studyhive.dev/errors/conflict",
                title: "Request cannot be cancelled",
                statusCode: StatusCodes.Status409Conflict,
                detail: $"This request is already '{bookingRequest.Status}'.");
        }

        bookingRequest.Status = BookingRequestStatus.Cancelled;
        bookingRequest.UpdatedAt = now;
        audit.Write("BookingCancelled", "BookingRequest", bookingRequest.Id, new { previousStatus = previousStatus.ToString() });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return NoContent();
    }

    private async Task CancelUndecidedAsync(BookingRequest bookingRequest, DateTimeOffset now, CancellationToken ct)
    {
        var proposed = await db.Quotations
            .Where(q => q.BookingRequestId == bookingRequest.Id && q.Status == QuotationStatus.Proposed)
            .ToListAsync(ct);
        foreach (var quotation in proposed)
        {
            quotation.Status = QuotationStatus.Superseded;
            quotation.UpdatedAt = now;
        }

        await stockService.ReleasePendingAsync(bookingRequest.Items.Select(i => i.Id).ToList(), ct);

        // Only a workflow that is still live is stopped; a finished one (for example the Rejected
        // workflow of a RevisionRequested request) keeps its own outcome.
        var execution = await db.WorkflowExecutions
            .Where(w => w.BookingRequestId == bookingRequest.Id)
            .OrderByDescending(w => w.StartedAt)
            .FirstOrDefaultAsync(ct);
        if (execution is { Status: WorkflowStatus.Started or WorkflowStatus.InProgress or WorkflowStatus.PendingApproval })
        {
            execution.Status = WorkflowStatus.Rejected;
            execution.ErrorCode = BookingCancellation.ErrorCode;
            execution.ErrorMessage = BookingCancellation.ErrorMessage;
            execution.CompletedAt = now;
            execution.UpdatedAt = now;
        }
    }

    /// <summary>Returns the 409 to send when the booking has already started, otherwise null after
    /// releasing the rooms and stock.</summary>
    private async Task<IActionResult?> CancelApprovedAsync(BookingRequest bookingRequest, DateTimeOffset now, CancellationToken ct)
    {
        var bookings = await db.RoomBookings
            .Where(b => b.BookingRequestId == bookingRequest.Id && b.Status != RoomBookingStatus.Cancelled)
            .ToListAsync(ct);
        if (bookings.Any(b => b.StartsAt <= now || b.CheckedInAt is not null || b.Status != RoomBookingStatus.Confirmed))
        {
            return Problem(
                type: "https://studyhive.dev/errors/booking-started",
                title: "Booking cannot be cancelled",
                statusCode: StatusCodes.Status409Conflict,
                detail: "This booking has already started.");
        }

        foreach (var booking in bookings)
        {
            booking.Status = RoomBookingStatus.Cancelled;
            booking.UpdatedAt = now;
        }

        var itemIds = bookingRequest.Items.Select(i => i.Id).ToList();
        var reservedIds = await db.StockReservations.AsNoTracking()
            .Where(r => itemIds.Contains(r.BookingRequestItemId) && r.Status == StockReservationStatus.Reserved)
            .Select(r => r.Id)
            .ToListAsync(ct);
        var userId = User.GetUserId();
        foreach (var reservationId in reservedIds)
        {
            // Joins this transaction: gives the units back and writes the Release stock transaction,
            // so the stock levels and the consumable-usage report agree.
            var released = await stockService.ReleaseAsync(reservationId, userId, ct);
            if (!released.Succeeded)
            {
                throw new InvalidOperationException($"Could not release reservation {reservationId}: {released.Detail}");
            }
        }

        var studentEmail = await db.StudentProfiles
            .Where(p => p.Id == bookingRequest.StudentId)
            .Select(p => p.User.Email)
            .SingleAsync(ct);
        db.EmailNotifications.Add(EmailNotification.ForBookingRequest(
            studentEmail, EmailTemplates.BookingCancelled, bookingRequest.Id, now));
        return null;
    }

    /// <summary>PLAN.md 3.1c: the student pays the Approved quotation's total at the library desk
    /// (outside the system) and the Librarian records it here. 409 when there is no Approved
    /// quotation or it is already paid. Payment does not gate check-in.</summary>
    [HttpPost("{id:guid}/payment")]
    [Authorize(Roles = Roles.Librarian)]
    [ProducesResponseType(typeof(BookingQuotationSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordPayment(Guid id, RecordPaymentRequest request, CancellationToken ct)
    {
        var requestStatus = await db.BookingRequests.AsNoTracking()
            .Where(r => r.Id == id).Select(r => (BookingRequestStatus?)r.Status).SingleOrDefaultAsync(ct);
        if (requestStatus is null) return NotFound();
        // Cancelling an approved booking leaves its quotation Approved, so check the request too.
        if (requestStatus is not (BookingRequestStatus.Approved or BookingRequestStatus.Completed))
        {
            return Problem(
                type: "https://studyhive.dev/errors/conflict",
                title: "Request has no approved quotation",
                statusCode: StatusCodes.Status409Conflict,
                detail: $"Only an approved booking can be marked as paid; this request is '{requestStatus}'.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM quotations WHERE booking_request_id = {id} AND status = 'Approved' FOR UPDATE", ct);

        var quotation = await db.Quotations
            .SingleOrDefaultAsync(q => q.BookingRequestId == id && q.Status == QuotationStatus.Approved, ct);
        if (quotation is null)
        {
            return Problem(
                type: "https://studyhive.dev/errors/conflict",
                title: "Request has no approved quotation",
                statusCode: StatusCodes.Status409Conflict,
                detail: "Only an approved booking can be marked as paid.");
        }
        if (quotation.PaidAt is not null)
        {
            return Problem(
                type: "https://studyhive.dev/errors/conflict",
                title: "Already paid",
                statusCode: StatusCodes.Status409Conflict,
                detail: $"This booking was already marked as paid on {quotation.PaidAt:O}.");
        }

        var now = DateTimeOffset.UtcNow;
        var reference = string.IsNullOrWhiteSpace(request.PaymentReference) ? null : request.PaymentReference.Trim();
        quotation.PaidAt = now;
        quotation.PaidBy = User.GetUserId();
        quotation.PaymentReference = reference;
        quotation.UpdatedAt = now;
        audit.Write("PaymentRecorded", "Quotation", quotation.Id, new
        {
            bookingRequestId = id, amount = quotation.TotalAmount, currency = quotation.Currency, paymentReference = reference,
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Ok(new BookingQuotationSummaryResponse
        {
            Id = quotation.Id,
            Status = quotation.Status,
            Version = quotation.Version,
            TotalAmount = quotation.TotalAmount,
            Currency = quotation.Currency,
            BudgetSnapshot = quotation.BudgetSnapshot,
            WithinBudget = quotation.WithinBudget,
            PaidAt = quotation.PaidAt,
            PaymentReference = quotation.PaymentReference,
        });
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = "StudentOnly")]
    [EnableRateLimiting(RateLimitPolicies.WorkflowSubmit)]
    [ProducesResponseType(typeof(SubmitBookingRequestResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        var bookingRequest = await db.BookingRequests.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (bookingRequest is null) return NotFound();

        if (!await AuthorizeOwnerAsync(bookingRequest.StudentId, ct, staffAllowed: false)) return Forbid();

        // Serializes concurrent submissions from the same student so the weekly-quota check below
        // can't race two Submit calls past each other (Codex security review, P1): FOR UPDATE holds
        // a row lock on the student's own profile for the rest of this transaction, so a second
        // concurrent Submit for a different draft blocks here until the first one commits — by which
        // point its WorkflowExecution already counts toward the quota the second call evaluates.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.StudentProfiles
            .FromSqlInterpolated($"SELECT * FROM student_profiles WHERE id = {bookingRequest.StudentId} FOR UPDATE")
            .AsNoTracking()
            .SingleAsync(ct);

        // Then the request row itself (the lock Cancel, approvals and the workflow take), re-read
        // after it: a cancel or a second submit that committed meanwhile is seen here, never
        // overwritten.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM booking_requests WHERE id = {id} FOR UPDATE", ct);
        await db.Entry(bookingRequest).ReloadAsync(ct);

        // AUDIT C-02: a RevisionRequested request can also be resent as it is.
        if (bookingRequest.Status is not (BookingRequestStatus.Draft or BookingRequestStatus.RevisionRequested))
        {
            return Problem(
                type: "https://studyhive.dev/errors/conflict",
                title: "Only draft requests can be submitted",
                statusCode: StatusCodes.Status409Conflict,
                detail: $"This request is '{bookingRequest.Status}' and cannot be submitted again.");
        }

        // Fail fast, synchronously — the workflow itself never re-litigates eligibility from
        // scratch, it only carries this same verdict to the Planner (see WorkflowOrchestrationService).
        var eligibility = await eligibilityService.EvaluateAsync(bookingRequest.StudentId, bookingRequest.Id, ct);
        if (!eligibility.IsEligible)
        {
            return Problem(
                type: "https://studyhive.dev/errors/validation",
                title: "Student is not eligible to submit a booking request",
                statusCode: StatusCodes.Status422UnprocessableEntity,
                detail: string.Join(" ", eligibility.Reasons));
        }

        // A resent request's earlier quotation (Rejected by the "ask for a change" decision) is
        // superseded; the new workflow writes the next version.
        var earlier = await db.Quotations
            .Where(q => q.BookingRequestId == id && (q.Status == QuotationStatus.Proposed || q.Status == QuotationStatus.Rejected))
            .ToListAsync(ct);
        foreach (var quotation in earlier)
        {
            quotation.Status = QuotationStatus.Superseded;
            quotation.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var workflowId = await workflowOrchestration.StartAsync(id, ct);
        await transaction.CommitAsync(ct);

        await workflowQueue.EnqueueAsync(workflowId, ct);

        return Accepted(value: new SubmitBookingRequestResponse { WorkflowId = workflowId });
    }

    [HttpGet("{id:guid}/status")]
    [ProducesResponseType(typeof(WorkflowStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(Guid id, CancellationToken ct)
    {
        var bookingRequest = await db.BookingRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
        if (bookingRequest is null) return NotFound();

        if (!await AuthorizeOwnerAsync(bookingRequest.StudentId, ct)) return Forbid();

        var execution = await db.WorkflowExecutions.AsNoTracking()
            .Include(w => w.StepLogs)
            .Where(w => w.BookingRequestId == id)
            .OrderByDescending(w => w.StartedAt)
            .FirstOrDefaultAsync(ct);

        if (execution is null)
        {
            return Problem(
                type: "https://studyhive.dev/errors/not-found",
                title: "No workflow has been started for this request",
                statusCode: StatusCodes.Status404NotFound);
        }

        return Ok(WorkflowStatusResponse.From(execution));
    }

    /// <summary>For each request id: its newest non-Draft quotation (highest version) and the newest
    /// librarian decision on that same quotation, or null when it has none. A decision on an older
    /// version is never paired with a newer one, so a student is not shown a stale rejection for a
    /// proposal still waiting. Two queries for the whole page. Callers pass only ids they have
    /// already authorised; nothing here widens what a caller can see.</summary>
    private async Task<Dictionary<Guid, (BookingQuotationSummaryResponse? Quotation, BookingDecisionSummaryResponse? Decision)>>
        LoadS4SummariesAsync(IReadOnlyCollection<Guid> requestIds, CancellationToken ct)
    {
        var result = new Dictionary<Guid, (BookingQuotationSummaryResponse?, BookingDecisionSummaryResponse?)>();
        if (requestIds.Count == 0) return result;

        var quotations = await db.Quotations.AsNoTracking()
            .Where(q => requestIds.Contains(q.BookingRequestId) && q.Status != QuotationStatus.Draft)
            .Select(q => new
            {
                q.BookingRequestId,
                Summary = new BookingQuotationSummaryResponse
                {
                    Id = q.Id,
                    Status = q.Status,
                    Version = q.Version,
                    TotalAmount = q.TotalAmount,
                    Currency = q.Currency,
                    BudgetSnapshot = q.BudgetSnapshot,
                    WithinBudget = q.WithinBudget,
                    PaidAt = q.PaidAt,
                    PaymentReference = q.PaymentReference,
                },
                q.CreatedAt,
            })
            .ToListAsync(ct);

        var decisions = await db.ApprovalDecisions.AsNoTracking()
            .Where(d => requestIds.Contains(d.Quotation.BookingRequestId))
            .Select(d => new
            {
                d.QuotationId,
                Summary = new BookingDecisionSummaryResponse
                {
                    Decision = d.Decision,
                    Comments = d.Comments,
                    DecidedAt = d.DecidedAt,
                },
            })
            .ToListAsync(ct);

        foreach (var requestId in requestIds)
        {
            var quotation = quotations
                .Where(q => q.BookingRequestId == requestId)
                .OrderByDescending(q => q.Summary.Version)
                .ThenByDescending(q => q.CreatedAt)
                .Select(q => q.Summary)
                .FirstOrDefault();
            var decision = quotation is null
                ? null
                : decisions
                    .Where(d => d.QuotationId == quotation.Id)
                    .OrderByDescending(d => d.Summary.DecidedAt)
                    .Select(d => d.Summary)
                    .FirstOrDefault();
            result[requestId] = (quotation, decision);
        }
        return result;
    }

    /// <summary>Each request's room bookings, oldest slot first. Callers pass only ids they have
    /// already authorised.</summary>
    private async Task<Dictionary<Guid, List<BookingRoomBookingResponse>>> LoadRoomBookingsAsync(
        IReadOnlyCollection<Guid> requestIds, CancellationToken ct)
    {
        if (requestIds.Count == 0) return [];

        var rows = await db.RoomBookings.AsNoTracking()
            .Where(b => requestIds.Contains(b.BookingRequestId))
            .OrderBy(b => b.StartsAt)
            .Select(b => new
            {
                b.BookingRequestId,
                Booking = new BookingRoomBookingResponse
                {
                    Id = b.Id,
                    RoomId = b.RoomId,
                    RoomName = b.Room.Name,
                    StartsAt = b.StartsAt,
                    EndsAt = b.EndsAt,
                    Status = b.Status,
                    CheckedInAt = b.CheckedInAt,
                },
            })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.BookingRequestId).ToDictionary(g => g.Key, g => g.Select(r => r.Booking).ToList());
    }

    private async Task<StudentProfile?> GetOwnStudentProfileAsync(CancellationToken ct)
    {
        var userId = User.GetUserId();
        return await db.StudentProfiles.SingleOrDefaultAsync(p => p.UserId == userId, ct);
    }

    private ObjectResult NoStudentProfileProblem() => Problem(
        type: "https://studyhive.dev/errors/validation",
        title: "No student profile",
        statusCode: StatusCodes.Status422UnprocessableEntity,
        detail: "Create a student profile (POST /api/student-profiles) before creating booking requests.");

    /// <summary>Resolves the booking request's owning user id and checks it against an explicit
    /// Librarian allow-list — deliberately not the shared "any staff role" ResourceOwner
    /// policy, which would also let StoreOfficer read student booking data outside DOCS §11's
    /// documented "Student (own), Librarian" scope (Codex security review, P1). When
    /// <paramref name="staffAllowed"/> is false, only the owning student may act — used for write
    /// operations the API table restricts to "Student (own)".</summary>
    private async Task<bool> AuthorizeOwnerAsync(Guid studentProfileId, CancellationToken ct, bool staffAllowed = true)
    {
        if (staffAllowed && IsStaffReader(User)) return true;

        var ownerUserId = await db.StudentProfiles.AsNoTracking()
            .Where(p => p.Id == studentProfileId)
            .Select(p => p.UserId)
            .SingleOrDefaultAsync(ct);

        return User.TryGetUserId(out var callerId) && callerId == ownerUserId;
    }

    /// <summary>Every S1 endpoint's staff-read scope, per DOCS §11: Librarian only. Deliberately
    /// excludes StoreOfficer and Admin because neither has a documented need to read student booking
    /// data; Admin-only profile updates are separately protected by the AdminOnly policy.</summary>
    private static bool IsStaffReader(System.Security.Claims.ClaimsPrincipal user) =>
        user.IsInRole(Roles.Librarian);

    /// <summary>Rejects duplicate or nonexistent consumable ids as a controlled 422 instead of
    /// letting the DB's unique index / FK constraint turn bad client input into a 500 (Codex
    /// security review, P2).</summary>
    private async Task<IActionResult?> ValidateItemsOrProblemAsync(IReadOnlyList<BookingRequestItemRequest> items, CancellationToken ct)
    {
        if (items.Count == 0) return null;

        var ids = items.Select(i => i.ConsumableId).ToList();
        var duplicateIds = ids.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateIds.Count > 0)
        {
            return Problem(
                type: "https://studyhive.dev/errors/validation",
                title: "Duplicate consumable in request items",
                statusCode: StatusCodes.Status422UnprocessableEntity,
                detail: $"Each consumable can only appear once per request: {string.Join(", ", duplicateIds)}.");
        }

        var existingIds = await db.Consumables.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync(ct);
        var missingIds = ids.Except(existingIds).ToList();
        if (missingIds.Count > 0)
        {
            return Problem(
                type: "https://studyhive.dev/errors/validation",
                title: "Unknown consumable",
                statusCode: StatusCodes.Status422UnprocessableEntity,
                detail: $"These consumable ids do not exist: {string.Join(", ", missingIds)}.");
        }

        return null;
    }
}
