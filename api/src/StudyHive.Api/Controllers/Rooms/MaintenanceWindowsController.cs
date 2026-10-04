using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyHive.Api.Common;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;
using StudyHive.Api.Services;

namespace StudyHive.Api.Controllers.Rooms;

/// <summary>
/// S2 maintenance windows. Availability queries use these persisted periods to
/// exclude rooms that cannot be booked.
///
/// AUDIT CW-06: a window that overlaps Confirmed bookings is refused with 409
/// <c>maintenance-overlaps-bookings</c> (listing them) unless the librarian confirms with
/// <c>force=true</c>, which emails each affected student. Future windows can be edited (PUT) and
/// removed (DELETE); a window that has started is fixed. There is no status column, so removal is a
/// hard delete and the audit row keeps the record.
/// </summary>
[ApiController]
[Route("api/maintenance-windows")]
[Authorize]
public sealed class MaintenanceWindowsController(StudyHiveDbContext db, IAuditWriter audit) : ControllerBase
{
    /// <summary>Create a maintenance window. Overlapping Confirmed bookings need <c>force=true</c>.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Librarian)]
    [ProducesResponseType(typeof(MaintenanceWindowResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateMaintenanceWindowRequest request,
        [FromQuery] bool force,
        CancellationToken ct)
    {
        if (!Validate(request.Reason, request.StartsAt, request.EndsAt)) return ValidationProblem(ModelState);

        var room = await db.StudyRooms.AsNoTracking().SingleOrDefaultAsync(r => r.Id == request.RoomId, ct);
        if (room is null) return NotFound();

        var overlapping = await OverlappingBookingsAsync(request.RoomId, request.StartsAt, request.EndsAt, ct);
        if (overlapping.Count > 0 && !force) return OverlapConflict(overlapping);

        var now = DateTimeOffset.UtcNow;
        var window = new MaintenanceWindow
        {
            Id = Guid.NewGuid(),
            RoomId = request.RoomId,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            Reason = request.Reason.Trim(),
            CreatedAt = now,
        };
        db.MaintenanceWindows.Add(window);
        QueueConflictEmails(overlapping, now);
        audit.Write("MaintenanceScheduled", "MaintenanceWindow", window.Id, new
        {
            window.RoomId, room = room.Name, window.StartsAt, window.EndsAt, window.Reason,
            forcedOverBookings = overlapping.Select(b => b.BookingId).ToList(),
        });
        await db.SaveChangesAsync(ct);

        return StatusCode(StatusCodes.Status201Created, ToResponse(window, room.Name, overlapping.Count));
    }

    /// <summary>Change a window that has not started. Overlaps follow the same rule as create.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Librarian)]
    [ProducesResponseType(typeof(MaintenanceWindowResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateMaintenanceWindowRequest request,
        [FromQuery] bool force,
        CancellationToken ct)
    {
        var window = await db.MaintenanceWindows.Include(w => w.Room).SingleOrDefaultAsync(w => w.Id == id, ct);
        if (window is null) return NotFound();

        var now = DateTimeOffset.UtcNow;
        if (window.StartsAt <= now) return AlreadyStarted();
        if (!Validate(request.Reason, request.StartsAt, request.EndsAt)) return ValidationProblem(ModelState);

        var overlapping = await OverlappingBookingsAsync(window.RoomId, request.StartsAt, request.EndsAt, ct);
        if (overlapping.Count > 0 && !force) return OverlapConflict(overlapping);

        // Only students whose booking was not already inside the old window hear about it again.
        var alreadyAffected = (await OverlappingBookingsAsync(window.RoomId, window.StartsAt, window.EndsAt, ct))
            .Select(b => b.BookingId)
            .ToHashSet();

        window.StartsAt = request.StartsAt;
        window.EndsAt = request.EndsAt;
        window.Reason = request.Reason.Trim();
        QueueConflictEmails(overlapping.Where(b => !alreadyAffected.Contains(b.BookingId)).ToList(), now);
        audit.Write("MaintenanceUpdated", "MaintenanceWindow", window.Id, new
        {
            window.RoomId, window.StartsAt, window.EndsAt, window.Reason,
            forcedOverBookings = overlapping.Select(b => b.BookingId).ToList(),
        });
        await db.SaveChangesAsync(ct);

        return Ok(ToResponse(window, window.Room.Name, overlapping.Count));
    }

    /// <summary>Cancel a window that has not started (hard delete; the audit row keeps the record).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Librarian)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var window = await db.MaintenanceWindows.SingleOrDefaultAsync(w => w.Id == id, ct);
        if (window is null) return NotFound();
        if (window.StartsAt <= DateTimeOffset.UtcNow) return AlreadyStarted();

        db.MaintenanceWindows.Remove(window);
        audit.Write("MaintenanceCancelled", "MaintenanceWindow", window.Id,
            new { window.RoomId, window.StartsAt, window.EndsAt, window.Reason });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>List maintenance windows. Backs W-17.</summary>
    [HttpGet]
    [Authorize(Roles = $"{Roles.Librarian},{Roles.Admin}")] // CW-05: Admin reads; writing stays Librarian-only
    [ProducesResponseType(typeof(PagedResult<MaintenanceWindowResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List([FromQuery] PageQuery query, CancellationToken ct)
    {
        IQueryable<MaintenanceWindow> windows = db.MaintenanceWindows.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            windows = windows.Where(w =>
                EF.Functions.ILike(w.Reason, pattern) ||
                EF.Functions.ILike(w.Room.Name, pattern));
        }

        var direction = query.SortDir.ToLowerInvariant();
        if (direction is not ("asc" or "desc"))
        {
            ModelState.AddModelError(nameof(query.SortDir), "sortDir must be either 'asc' or 'desc'.");
            return ValidationProblem(ModelState);
        }

        var descending = direction == "desc";
        windows = query.SortBy?.ToLowerInvariant() switch
        {
            null or "" or "createdat" => descending
                ? windows.OrderByDescending(w => w.CreatedAt)
                : windows.OrderBy(w => w.CreatedAt),
            "startsat" => descending
                ? windows.OrderByDescending(w => w.StartsAt)
                : windows.OrderBy(w => w.StartsAt),
            "endsat" => descending
                ? windows.OrderByDescending(w => w.EndsAt)
                : windows.OrderBy(w => w.EndsAt),
            "room" => descending
                ? windows.OrderByDescending(w => w.Room.Name)
                : windows.OrderBy(w => w.Room.Name),
            "reason" => descending
                ? windows.OrderByDescending(w => w.Reason)
                : windows.OrderBy(w => w.Reason),
            _ => null!,
        };

        if (windows is null)
        {
            ModelState.AddModelError(nameof(query.SortBy), $"Unknown sortBy value '{query.SortBy}'.");
            return ValidationProblem(ModelState);
        }

        var totalItems = await windows.CountAsync(ct);
        var items = await windows
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(w => new MaintenanceWindowResponse(
                w.Id,
                w.RoomId,
                w.Room.Name,
                w.StartsAt,
                w.EndsAt,
                w.Reason,
                db.RoomBookings.Count(b =>
                    b.RoomId == w.RoomId &&
                    b.Status == RoomBookingStatus.Confirmed &&
                    b.StartsAt < w.EndsAt &&
                    b.EndsAt > w.StartsAt),
                w.CreatedAt))
            .ToListAsync(ct);

        return Ok(PagedResult<MaintenanceWindowResponse>.Create(
            items, query.Page, query.PageSize, totalItems));
    }

    private bool Validate(string? reason, DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            ModelState.AddModelError("Reason", "Reason is required.");
        }

        if (endsAt <= startsAt)
        {
            ModelState.AddModelError("EndsAt", "End time must be later than start time.");
        }

        return ModelState.IsValid;
    }

    private Task<List<OverlappingBooking>> OverlappingBookingsAsync(
        Guid roomId, DateTimeOffset startsAt, DateTimeOffset endsAt, CancellationToken ct) =>
        db.RoomBookings.AsNoTracking()
            .Where(b => b.RoomId == roomId && b.Status == RoomBookingStatus.Confirmed &&
                        b.StartsAt < endsAt && b.EndsAt > startsAt)
            .OrderBy(b => b.StartsAt)
            .Select(b => new OverlappingBooking(
                b.Id,
                b.BookingRequestId,
                b.BookingRequest.Student.User.FullName,
                b.BookingRequest.Student.User.Email,
                b.StartsAt,
                b.EndsAt))
            .ToListAsync(ct);

    /// <summary>One email per affected request, queued in the caller's SaveChanges (outbox).</summary>
    private void QueueConflictEmails(IReadOnlyCollection<OverlappingBooking> bookings, DateTimeOffset now)
    {
        foreach (var request in bookings.GroupBy(b => b.BookingRequestId))
        {
            db.EmailNotifications.Add(EmailNotification.ForBookingRequest(
                request.First().StudentEmail, EmailTemplates.MaintenanceConflict, request.Key, now));
        }
    }

    private ObjectResult OverlapConflict(IReadOnlyList<OverlappingBooking> bookings)
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(
            HttpContext,
            statusCode: StatusCodes.Status409Conflict,
            title: "Maintenance overlaps confirmed bookings",
            type: "https://studyhive.dev/errors/maintenance-overlaps-bookings",
            detail: $"{bookings.Count} confirmed booking(s) fall inside this window. Send force=true to schedule it anyway; the students are emailed.");
        problem.Extensions["bookings"] = bookings
            .Select(b => new { b.BookingId, b.BookingRequestId, b.StudentName, b.StartsAt, b.EndsAt })
            .ToList();
        return new ObjectResult(problem) { StatusCode = StatusCodes.Status409Conflict };
    }

    private ObjectResult AlreadyStarted() => Problem(
        type: "https://studyhive.dev/errors/maintenance-started",
        title: "Maintenance has already started",
        statusCode: StatusCodes.Status409Conflict,
        detail: "Only a maintenance window that has not started can be changed or cancelled.");

    private static MaintenanceWindowResponse ToResponse(MaintenanceWindow w, string roomName, int affected) => new(
        w.Id, w.RoomId, roomName, w.StartsAt, w.EndsAt, w.Reason, affected, w.CreatedAt);

    private sealed record OverlappingBooking(
        Guid BookingId,
        Guid BookingRequestId,
        string StudentName,
        string StudentEmail,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt);
}

public sealed record CreateMaintenanceWindowRequest(
    Guid RoomId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Reason);

public sealed record UpdateMaintenanceWindowRequest(
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Reason);

public sealed record MaintenanceWindowResponse(
    Guid Id,
    Guid RoomId,
    string RoomName,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Reason,
    int AffectedBookings,
    DateTimeOffset CreatedAt);
