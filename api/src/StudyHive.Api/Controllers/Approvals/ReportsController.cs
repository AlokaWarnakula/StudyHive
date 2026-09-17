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
/// SCAFFOLD ONLY - owned by S2, S3 and S4 (one action each), not implemented yet. Every action below returns 501 so the
/// route, its role gate and its shape are pinned by the plan's DOCS section 11 API table before
/// anyone writes a line of logic. Nothing here fabricates data: an unimplemented endpoint must
/// never answer as though it worked.
///
/// To implement one: inject StudyHiveDbContext, delete the NotImplemented() call, and return the
/// real result. Keep the route and the [Authorize] attribute exactly as they are - the web and
/// mobile clients are already written against them.
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
    /// <summary>The single place this scaffold refuses. Replace the call, not this helper.</summary>
    private ObjectResult NotImplemented(string what) => Problem(
        type: "https://studyhive.dev/errors/not-implemented",
        title: "Not implemented yet",
        statusCode: StatusCodes.Status501NotImplemented,
        detail: $"{what} is owned by S2, S3 and S4 (one action each) and has not been built yet.");

    /// <summary>Booking analytics. Backs W-09. Owned by S4.</summary>
    [HttpGet("bookings")]
    [Authorize(Roles = $"{Roles.Librarian},{Roles.Admin}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Bookings() => NotImplemented("The bookings report");

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
            .Select(b => new { b.RoomId, b.StartsAt, b.EndsAt, b.Status })
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
                roomBookings.Count(b => b.Status == RoomBookingStatus.NoShow));
        }).OrderByDescending(r => r.UtilisationPercent).ThenBy(r => r.RoomName).ToList();

        var totalBookedHours = byRoom.Sum(r => r.BookedHours);
        var averagePercent = rooms.Count == 0
            ? 0
            : Math.Min(100, (double)totalBookedHours / (rooms.Count * rangeHours) * 100);
        var byHour = Enumerable.Range(0, 24)
            .Select(hour => new RoomUsageHourResponse(hour, bookings.Count(b => b.StartsAt.UtcDateTime.Hour == hour)))
            .ToList();

        return Ok(new RoomUsageReportResponse(
            rangeStart,
            rangeEnd,
            bookings.Count,
            totalBookedHours,
            Round(averagePercent),
            bookings.Count(b => b.Status == RoomBookingStatus.NoShow),
            byRoom.FirstOrDefault(r => r.BookingCount > 0)?.RoomName,
            byRoom,
            byHour));
    }

    /// <summary>Usage per item, cost and wastage. Backs W-24. Owned by S3.</summary>
    [HttpGet("consumable-usage")]
    [Authorize(Roles = $"{Roles.StoreOfficer}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult ConsumableUsage() => NotImplemented("The consumable usage report");

    private static decimal Round(double value) => decimal.Round((decimal)value, 2, MidpointRounding.AwayFromZero);
}

public sealed record RoomUsageReportResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    int TotalBookings,
    decimal TotalBookedHours,
    decimal AverageUtilisationPercent,
    int NoShows,
    string? BusiestRoom,
    IReadOnlyList<RoomUsageRowResponse> ByRoom,
    IReadOnlyList<RoomUsageHourResponse> BookingsByHour);

public sealed record RoomUsageRowResponse(
    Guid RoomId,
    string RoomName,
    int BookingCount,
    decimal BookedHours,
    decimal UtilisationPercent,
    int NoShows);

public sealed record RoomUsageHourResponse(int Hour, int BookingCount);
