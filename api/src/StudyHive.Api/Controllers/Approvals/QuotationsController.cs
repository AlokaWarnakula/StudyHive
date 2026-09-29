using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyHive.Api.Common;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;
using StudyHive.Api.Security;

namespace StudyHive.Api.Controllers.Approvals;

/// <summary>
/// S4: quotations and their line items (W-05, mobile M-08). Read-only over HTTP: a quotation is
/// written by the workflow's Validation step (WorkflowOrchestrationService) and moved on only by an
/// approval decision (ApprovalsController), so there is deliberately no POST here.
///
/// House rules (DOCS/S2_S3_S4_UI_Interface_Map.md): lists take [FromQuery] PageQuery and return
/// PagedResult&lt;T&gt; with filtering, sorting, counting and paging in SQL; unknown sortBy is a 400;
/// errors are RFC 7807 from the global handler.
/// </summary>
[ApiController]
[Route("api/quotations")]
[Authorize]
public sealed class QuotationsController(StudyHiveDbContext db) : ControllerBase
{
    /// <summary>List quotations. `status` is Draft|Proposed|Approved|Rejected|Superseded; `bookingRequestId` narrows to one request.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Librarian)]
    [ProducesResponseType(typeof(PagedResult<QuotationSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] PageQuery query,
        [FromQuery] string? status,
        [FromQuery] Guid? bookingRequestId,
        CancellationToken ct)
    {
        IQueryable<Quotation> quotations = db.Quotations.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<QuotationStatus>(status, ignoreCase: true, out var parsed))
            {
                ModelState.AddModelError(nameof(status), $"Unknown status value '{status}'.");
                return ValidationProblem(ModelState);
            }
            quotations = quotations.Where(q => q.Status == parsed);
        }
        if (bookingRequestId is { } requestId)
        {
            quotations = quotations.Where(q => q.BookingRequestId == requestId);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{query.Search.Trim()}%";
            quotations = quotations.Where(q => EF.Functions.ILike(q.BookingRequest.Objective, term));
        }

        var descending = !string.Equals(query.SortDir, "asc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<Quotation>? sorted = query.SortBy?.ToLowerInvariant() switch
        {
            null or "" or "createdat" => descending ? quotations.OrderByDescending(q => q.CreatedAt) : quotations.OrderBy(q => q.CreatedAt),
            "totalamount" => descending ? quotations.OrderByDescending(q => q.TotalAmount) : quotations.OrderBy(q => q.TotalAmount),
            "status" => descending ? quotations.OrderByDescending(q => q.Status) : quotations.OrderBy(q => q.Status),
            _ => null,
        };
        if (sorted is null)
        {
            ModelState.AddModelError(nameof(query.SortBy), $"Unknown sortBy value '{query.SortBy}'.");
            return ValidationProblem(ModelState);
        }

        var totalItems = await sorted.CountAsync(ct);
        var items = await sorted.ThenBy(q => q.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(q => new QuotationSummaryResponse
            {
                Id = q.Id,
                BookingRequestId = q.BookingRequestId,
                Objective = q.BookingRequest.Objective,
                Version = q.Version,
                RoomFee = q.RoomFee,
                ConsumableCost = q.ConsumableCost,
                TotalAmount = q.TotalAmount,
                BudgetSnapshot = q.BudgetSnapshot,
                WithinBudget = q.WithinBudget,
                Currency = q.Currency,
                Status = q.Status,
                LineItemCount = q.LineItems.Count,
                CreatedAt = q.CreatedAt,
                UpdatedAt = q.UpdatedAt,
            })
            .ToListAsync(ct);

        return Ok(PagedResult<QuotationSummaryResponse>.Create(items, query.Page, query.PageSize, totalItems));
    }

    /// <summary>Get a quotation with its line items. Backs W-05 and M-08. A librarian may read any;
    /// a student only one for their own booking request (403 otherwise).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{Roles.Librarian},{Roles.Student}")]
    [ProducesResponseType(typeof(QuotationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var owner = await db.Quotations.AsNoTracking()
            .Where(q => q.Id == id)
            .Select(q => new { OwnerUserId = q.BookingRequest.Student.UserId })
            .SingleOrDefaultAsync(ct);
        if (owner is null)
        {
            return NotFound();
        }
        if (!User.IsInRole(Roles.Librarian) &&
            !(User.TryGetUserId(out var callerId) && callerId == owner.OwnerUserId))
        {
            return Forbid();
        }

        var quotation = await db.Quotations.AsNoTracking()
            .Where(q => q.Id == id)
            .Select(q => new QuotationResponse
            {
                Id = q.Id,
                BookingRequestId = q.BookingRequestId,
                Version = q.Version,
                RoomFee = q.RoomFee,
                ConsumableCost = q.ConsumableCost,
                TotalAmount = q.TotalAmount,
                BudgetSnapshot = q.BudgetSnapshot,
                WithinBudget = q.WithinBudget,
                Currency = q.Currency,
                Status = q.Status,
                CreatedAt = q.CreatedAt,
                UpdatedAt = q.UpdatedAt,
                LineItems = q.LineItems
                    .OrderBy(l => l.ItemType).ThenBy(l => l.ItemName)
                    .Select(l => new QuotationLineItemResponse
                    {
                        Id = l.Id,
                        QuotationId = l.QuotationId,
                        ItemType = l.ItemType,
                        RoomId = l.RoomId,
                        RoomBookingId = l.RoomBookingId,
                        ConsumableId = l.ConsumableId,
                        ItemName = l.ItemName,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        LineTotal = l.LineTotal,
                        CreatedAt = l.CreatedAt,
                    })
                    .ToList(),
            })
            .SingleAsync(ct);

        return Ok(quotation);
    }
}

public sealed class QuotationSummaryResponse
{
    public required Guid Id { get; init; }
    public required Guid BookingRequestId { get; init; }
    public required string Objective { get; init; }
    public required int Version { get; init; }
    public required decimal RoomFee { get; init; }
    public required decimal ConsumableCost { get; init; }
    public required decimal TotalAmount { get; init; }
    public required decimal BudgetSnapshot { get; init; }
    public required bool WithinBudget { get; init; }
    public required string Currency { get; init; }
    public required QuotationStatus Status { get; init; }
    public required int LineItemCount { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>Mirrors <c>quotations</c> with its lines (web/src/api/approvals.ts Quotation).</summary>
public sealed class QuotationResponse
{
    public required Guid Id { get; init; }
    public required Guid BookingRequestId { get; init; }
    public required int Version { get; init; }
    public required decimal RoomFee { get; init; }
    public required decimal ConsumableCost { get; init; }
    public required decimal TotalAmount { get; init; }
    public required decimal BudgetSnapshot { get; init; }
    public required bool WithinBudget { get; init; }
    public required string Currency { get; init; }
    public required QuotationStatus Status { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public required IReadOnlyList<QuotationLineItemResponse> LineItems { get; init; }
}

/// <summary>A Room line carries <c>roomId</c> (and <c>roomBookingId</c> once approved); a Consumable line carries <c>consumableId</c>.</summary>
public sealed class QuotationLineItemResponse
{
    public required Guid Id { get; init; }
    public required Guid QuotationId { get; init; }
    public required QuotationLineItemType ItemType { get; init; }
    public Guid? RoomId { get; init; }
    public Guid? RoomBookingId { get; init; }
    public Guid? ConsumableId { get; init; }
    public required string ItemName { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal LineTotal { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
