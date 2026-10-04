using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyHive.Api.Common;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Controllers.Approvals;

/// <summary>
/// Reporting. Each report belongs to the owner of the data it reports on, so this controller is shared: bookings is S4's, room-usage is S2's, consumable-usage is S3's.
///
/// All three are implemented. Keep each route and its [Authorize] attribute as they are - the web
/// and mobile clients are written against them.
///
/// House rules that already apply here (see DOCS/S2_S3_S4_UI_Interface_Map.md):
///   - Lists take [FromQuery] PageQuery and return PagedResult&lt;T&gt;. Unknown sortBy is a 400.
///   - Errors are RFC 7807 from the global handler. Never hand-roll an error body.
///   - Deletes are deactivations, not physical deletes.
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize]
public sealed class ReportsController(StudyHiveDbContext db) : ControllerBase
{
    /// <summary>Asia/Colombo is a fixed UTC+05:30 with no daylight saving.</summary>
    private static readonly TimeSpan ColomboOffset = TimeSpan.FromMinutes(330);

    /// <summary>Booking requests created in the <c>from</c>/<c>to</c> window (default: the last 30
    /// days): counts by status (every status, zeros included), counts and approved spend per week
    /// (weeks start Monday, Asia/Colombo), and approved spend against the budgets it was quoted
    /// against. Every figure is aggregated in SQL. Backs W-09.</summary>
    [HttpGet("bookings")]
    [Authorize(Roles = $"{Roles.Librarian},{Roles.Admin}")]
    [ProducesResponseType(typeof(BookingsReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Bookings(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var rangeEnd = (to ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var rangeStart = (from ?? rangeEnd.AddDays(-30)).ToUniversalTime();
        if (rangeEnd <= rangeStart)
        {
            ModelState.AddModelError(nameof(to), "to must be later than from.");
            return ValidationProblem(ModelState);
        }

        // The three aggregates below read one snapshot, so the per-status, per-week and spend figures
        // always agree with each other even while requests are being created or removed.
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

        var requests = db.BookingRequests.AsNoTracking()
            .Where(r => r.CreatedAt >= rangeStart && r.CreatedAt < rangeEnd);

        var statusCounts = await requests
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var byStatus = Enum.GetValues<BookingRequestStatus>()
            .Select(status => new BookingStatusCountResponse(status, statusCounts.SingleOrDefault(c => c.Status == status)?.Count ?? 0))
            .ToList();

        // At most one Approved quotation per request (ux_quote_active), so this is one row per
        // approved request in the window.
        var approved = db.Quotations.AsNoTracking()
            .Where(q => q.Status == QuotationStatus.Approved && requests.Any(r => r.Id == q.BookingRequestId));
        var spend = await approved
            .GroupBy(_ => 1)
            .Select(g => new BookingsSpendResponse(
                g.Count(),
                g.Sum(q => q.TotalAmount),
                g.Sum(q => q.BudgetSnapshot),
                g.Count(q => q.WithinBudget),
                g.Count(q => !q.WithinBudget)))
            .SingleOrDefaultAsync(ct) ?? new BookingsSpendResponse(0, 0m, 0m, 0, 0);

        var byWeek = await db.Database.SqlQuery<BookingsWeekRow>($"""
            SELECT (date_trunc('week', br.created_at AT TIME ZONE 'Asia/Colombo'))::date AS week_start,
                   count(*)::int AS requests,
                   count(q.id)::int AS approved,
                   coalesce(sum(q.total_amount), 0) AS approved_spend,
                   coalesce(sum(q.budget_snapshot), 0) AS approved_budget
            FROM booking_requests br
            LEFT JOIN quotations q ON q.booking_request_id = br.id AND q.status = 'Approved'
            WHERE br.created_at >= {rangeStart} AND br.created_at < {rangeEnd}
            GROUP BY 1
            ORDER BY 1
            """).ToListAsync(ct);
        await snapshot.CommitAsync(ct);

        return Ok(new BookingsReportResponse(
            rangeStart,
            rangeEnd,
            byStatus.Sum(s => s.Count),
            byStatus,
            byWeek.Select(w => new BookingsWeekResponse(w.WeekStart, w.Requests, w.Approved, w.ApprovedSpend, w.ApprovedBudget)).ToList(),
            spend));
    }

    /// <summary>Room utilisation, peak hours and no-shows. Backs W-18. Owned by S2.</summary>
    [HttpGet("room-usage")]
    [Authorize(Roles = $"{Roles.Librarian}")]
    [ProducesResponseType(typeof(RoomUsageReportResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RoomUsage(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var rangeEnd = (to ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var rangeStart = (from ?? rangeEnd.AddDays(-30)).ToUniversalTime();
        if (rangeEnd <= rangeStart)
        {
            ModelState.AddModelError(nameof(to), "to must be later than from.");
            return ValidationProblem(ModelState);
        }

        var rooms = await db.StudyRooms.AsNoTracking()
            .Select(r => new { r.Id, r.Name })
            .ToListAsync(ct);
        var bookings = await db.RoomBookings.AsNoTracking()
            .Where(b => b.StartsAt < rangeEnd && b.EndsAt > rangeStart && b.Status != RoomBookingStatus.Cancelled)
            .Select(b => new { b.RoomId, b.StartsAt, b.EndsAt, b.Status, CheckedIn = b.CheckedInAt != null })
            .ToListAsync(ct);

        var rangeHours = (rangeEnd - rangeStart).TotalHours;
        var byRoom = rooms.Select(room =>
        {
            var roomBookings = bookings.Where(b => b.RoomId == room.Id).ToList();
            var bookedHours = roomBookings.Sum(b =>
                ((b.EndsAt < rangeEnd ? b.EndsAt : rangeEnd) -
                 (b.StartsAt > rangeStart ? b.StartsAt : rangeStart)).TotalHours);
            var percent = rangeHours <= 0 ? 0 : Math.Min(100, bookedHours / rangeHours * 100);
            return new RoomUsageRowResponse(
                room.Id,
                room.Name,
                roomBookings.Count,
                Round(bookedHours),
                Round(percent),
                roomBookings.Count(b => b.Status == RoomBookingStatus.NoShow),
                roomBookings.Count(b => b.CheckedIn));
        }).OrderByDescending(r => r.UtilisationPercent).ThenBy(r => r.RoomName).ToList();

        var totalBookedHours = byRoom.Sum(r => r.BookedHours);
        var averagePercent = rooms.Count == 0
            ? 0
            : Math.Min(100, (double)totalBookedHours / (rooms.Count * rangeHours) * 100);
        // AUDIT C-15: hours are the library's own (Asia/Colombo, fixed UTC+05:30), so a 10:00 booking is in hour 10.
        var byHour = Enumerable.Range(0, 24)
            .Select(hour => new RoomUsageHourResponse(hour, bookings.Count(b => b.StartsAt.ToOffset(ColomboOffset).Hour == hour)))
            .ToList();

        return Ok(new RoomUsageReportResponse(
            rangeStart,
            rangeEnd,
            bookings.Count,
            totalBookedHours,
            Round(averagePercent),
            bookings.Count(b => b.Status == RoomBookingStatus.NoShow),
            bookings.Count(b => b.CheckedIn),
            byRoom.FirstOrDefault(r => r.BookingCount > 0)?.RoomName,
            byRoom,
            byHour));
    }

    /// <summary>Usage per item, cost and wastage. Backs W-24. Owned by S3.</summary>
    [HttpGet("consumable-usage")]
    [Authorize(Roles = $"{Roles.StoreOfficer}")]
    [ProducesResponseType(typeof(ConsumableUsageReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsumableUsage(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] PageQuery query,
        CancellationToken ct)
    {
        var rangeEnd = (to ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var rangeStart = (from ?? rangeEnd.AddDays(-30)).ToUniversalTime();
        if (rangeEnd <= rangeStart)
        {
            ModelState.AddModelError(nameof(to), "to must be later than from.");
            return ValidationProblem(ModelState);
        }

        var descending = !string.Equals(query.SortDir, "asc", StringComparison.OrdinalIgnoreCase);

        // Rows are built in SQL: the per-item sums are correlated subqueries on stock_transactions
        // (ix_tx_cons covers consumable_id + created_at), and sorting, counting and Skip/Take all run
        // in Postgres, so only the requested page is materialised.
        // The ledger is the record of what moved: StockOut = issued (a reservation marked Used),
        // Reserve / Release = held and handed back, StockIn = restocked. Quantities are signed, so
        // outgoing movements are negated back to magnitudes.
        var rows = db.Consumables.AsNoTracking()
            .Where(c => c.IsActive || c.Transactions.Any(t => t.CreatedAt >= rangeStart && t.CreatedAt < rangeEnd))
            .Select(c => new UsageRow
            {
                ConsumableId = c.Id,
                Name = c.Name,
                Unit = c.Unit,
                UnitPrice = c.UnitPrice,
                Issued = -(c.Transactions
                    .Where(t => t.TransactionType == StockTransactionType.StockOut && t.CreatedAt >= rangeStart && t.CreatedAt < rangeEnd)
                    .Sum(t => (int?)t.Quantity) ?? 0),
                Reserved = -(c.Transactions
                    .Where(t => t.TransactionType == StockTransactionType.Reserve && t.CreatedAt >= rangeStart && t.CreatedAt < rangeEnd)
                    .Sum(t => (int?)t.Quantity) ?? 0),
                // CW-07: releasing a Pending reservation (reject, ask-for-change, cancel) moves no stock and
                // writes no ledger row, so "released" counts released reservations, not Release transactions.
                Released = c.Reservations
                    .Where(r => r.Status == StockReservationStatus.Released && r.ReleasedAt >= rangeStart && r.ReleasedAt < rangeEnd)
                    .Sum(r => (int?)r.Quantity) ?? 0,
                StockedIn = c.Transactions
                    .Where(t => t.TransactionType == StockTransactionType.StockIn && t.CreatedAt >= rangeStart && t.CreatedAt < rangeEnd)
                    .Sum(t => (int?)t.Quantity) ?? 0,
                StockQuantity = c.StockQuantity,
                ReservedNow = c.ReservedQuantity,
                AvailableQuantity = c.AvailableQuantity,
                IsLowStock = c.IsActive && c.StockQuantity <= c.MinStockLevel,
            });

        IOrderedQueryable<UsageRow>? sorted = query.SortBy?.ToLowerInvariant() switch
        {
            null or "" or "issued" => descending ? rows.OrderByDescending(r => r.Issued) : rows.OrderBy(r => r.Issued),
            "cost" => descending ? rows.OrderByDescending(r => r.Issued * r.UnitPrice) : rows.OrderBy(r => r.Issued * r.UnitPrice),
            "reserved" => descending ? rows.OrderByDescending(r => r.Reserved) : rows.OrderBy(r => r.Reserved),
            "released" => descending ? rows.OrderByDescending(r => r.Released) : rows.OrderBy(r => r.Released),
            "name" => descending ? rows.OrderByDescending(r => r.Name) : rows.OrderBy(r => r.Name),
            _ => null,
        };
        if (sorted is null)
        {
            ModelState.AddModelError(nameof(query.SortBy), $"Unknown sortBy value '{query.SortBy}'.");
            return ValidationProblem(ModelState);
        }

        var totalItems = await rows.CountAsync(ct);
        var page = (await sorted.ThenBy(r => r.Name).ThenBy(r => r.ConsumableId)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(ct))
            .Select(r => new ConsumableUsageRowResponse(
                r.ConsumableId, r.Name, r.Unit, r.UnitPrice, r.Issued, r.Issued * r.UnitPrice, r.Reserved, r.Released,
                r.StockedIn, r.StockQuantity, r.ReservedNow, r.AvailableQuantity, r.IsLowStock))
            .ToList();

        // Range totals across every consumable, not just this page: one grouped aggregate.
        var totals = await db.StockTransactions.AsNoTracking()
            .Where(t => t.CreatedAt >= rangeStart && t.CreatedAt < rangeEnd)
            .GroupBy(t => t.TransactionType)
            .Select(g => new { Type = g.Key, Units = g.Sum(t => t.Quantity) })
            .ToListAsync(ct);
        int Total(StockTransactionType type) => Math.Abs(totals.Where(t => t.Type == type).Sum(t => t.Units));
        var totalReleased = await db.StockReservations.AsNoTracking()
            .Where(r => r.Status == StockReservationStatus.Released && r.ReleasedAt >= rangeStart && r.ReleasedAt < rangeEnd)
            .SumAsync(r => (int?)r.Quantity, ct) ?? 0;
        var totalCost = await db.StockTransactions.AsNoTracking()
            .Where(t => t.TransactionType == StockTransactionType.StockOut && t.CreatedAt >= rangeStart && t.CreatedAt < rangeEnd)
            .SumAsync(t => (decimal?)(-t.Quantity * t.Consumable.UnitPrice), ct) ?? 0m;

        // Same predicate as GET /api/consumables/low-stock (and the ix_cons_low partial index).
        var lowStock = await db.Consumables.AsNoTracking()
            .Where(c => c.IsActive && c.StockQuantity <= c.MinStockLevel)
            .OrderBy(c => c.StockQuantity).ThenBy(c => c.Name)
            .Select(c => new ConsumableLowStockResponse(c.Id, c.Name, c.Unit, c.StockQuantity, c.ReservedQuantity, c.AvailableQuantity, c.MinStockLevel))
            .ToListAsync(ct);

        return Ok(new ConsumableUsageReportResponse(
            rangeStart,
            rangeEnd,
            Total(StockTransactionType.StockOut),
            totalCost,
            Total(StockTransactionType.Reserve),
            totalReleased,
            Total(StockTransactionType.StockIn),
            PagedResult<ConsumableUsageRowResponse>.Create(page, query.Page, query.PageSize, totalItems),
            lowStock));
    }

    /// <summary>SQL-side projection for the consumable-usage report, so sorting and paging translate.</summary>
    private sealed class UsageRow
    {
        public Guid ConsumableId { get; init; }
        public string Name { get; init; } = "";
        public string Unit { get; init; } = "";
        public decimal UnitPrice { get; init; }
        public int Issued { get; init; }
        public int Reserved { get; init; }
        public int Released { get; init; }
        public int StockedIn { get; init; }
        public int StockQuantity { get; init; }
        public int ReservedNow { get; init; }
        public int AvailableQuantity { get; init; }
        public bool IsLowStock { get; init; }
    }

    /// <summary>One row of the per-week SQL aggregate in <see cref="Bookings"/>.</summary>
    private sealed class BookingsWeekRow
    {
        public DateOnly WeekStart { get; init; }
        public int Requests { get; init; }
        public int Approved { get; init; }
        public decimal ApprovedSpend { get; init; }
        public decimal ApprovedBudget { get; init; }
    }

    private static decimal Round(double value) => decimal.Round((decimal)value, 2, MidpointRounding.AwayFromZero);
}

public sealed record BookingsReportResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    int TotalRequests,
    IReadOnlyList<BookingStatusCountResponse> ByStatus,
    IReadOnlyList<BookingsWeekResponse> ByWeek,
    BookingsSpendResponse Spend);

public sealed record BookingStatusCountResponse(BookingRequestStatus Status, int Count);

/// <summary>A Monday-starting week (Asia/Colombo) of requests created, how many were approved, and
/// the approved quotations' total against their budgets.</summary>
public sealed record BookingsWeekResponse(DateOnly WeekStart, int Requests, int Approved, decimal ApprovedSpend, decimal ApprovedBudget);

/// <summary>Approved quotations for the window's requests: their total spend against the budgets
/// they were quoted against (quotations.budget_snapshot).</summary>
public sealed record BookingsSpendResponse(
    int ApprovedQuotations,
    decimal TotalSpend,
    decimal TotalBudget,
    int WithinBudget,
    int OverBudget);

public sealed record RoomUsageReportResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    int TotalBookings,
    decimal TotalBookedHours,
    decimal AverageUtilisationPercent,
    int NoShows,
    int CheckedIn,
    string? BusiestRoom,
    IReadOnlyList<RoomUsageRowResponse> ByRoom,
    IReadOnlyList<RoomUsageHourResponse> BookingsByHour);

public sealed record RoomUsageRowResponse(
    Guid RoomId,
    string RoomName,
    int BookingCount,
    decimal BookedHours,
    decimal UtilisationPercent,
    int NoShows,
    int CheckedIn);

public sealed record RoomUsageHourResponse(int Hour, int BookingCount);

public sealed record ConsumableUsageReportResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    int TotalIssued,
    decimal TotalCost,
    int TotalReserved,
    int TotalReleased,
    int TotalStockedIn,
    PagedResult<ConsumableUsageRowResponse> ByItem,
    IReadOnlyList<ConsumableLowStockResponse> LowStock);

/// <summary>One consumable's movements inside the range. Cost is issued units at the current unit
/// price — the ledger records quantities, not prices, so historic price changes are not reflected.</summary>
public sealed record ConsumableUsageRowResponse(
    Guid ConsumableId,
    string Name,
    string Unit,
    decimal UnitPrice,
    int Issued,
    decimal Cost,
    int Reserved,
    int Released,
    int StockedIn,
    int StockQuantity,
    int ReservedNow,
    int AvailableQuantity,
    bool IsLowStock);

public sealed record ConsumableLowStockResponse(
    Guid ConsumableId,
    string Name,
    string Unit,
    int StockQuantity,
    int ReservedQuantity,
    int AvailableQuantity,
    int MinStockLevel);
