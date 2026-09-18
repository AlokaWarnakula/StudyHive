using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyHive.Api.Common;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Controllers.Rooms;

/// <summary>
/// S2 maintenance windows. Availability queries use these persisted periods to
/// exclude rooms that cannot be booked.
/// </summary>
[ApiController]
[Route("api/maintenance-windows")]
[Authorize]
public sealed class MaintenanceWindowsController(StudyHiveDbContext db) : ControllerBase
{
    /// <summary>Create a maintenance window and report how many confirmed bookings it affects.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Librarian)]
    [ProducesResponseType(typeof(MaintenanceWindowResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromBody] CreateMaintenanceWindowRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            ModelState.AddModelError(nameof(request.Reason), "Reason is required.");
        }

        if (request.EndsAt <= request.StartsAt)
        {
            ModelState.AddModelError(nameof(request.EndsAt), "End time must be later than start time.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var room = await db.StudyRooms
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == request.RoomId, ct);

        if (room is null)
        {
            return NotFound();
        }

        var affectedBookings = await db.RoomBookings.CountAsync(b =>
            b.RoomId == request.RoomId &&
            b.Status == RoomBookingStatus.Confirmed &&
            b.StartsAt < request.EndsAt &&
            b.EndsAt > request.StartsAt,
            ct);

        var maintenanceWindow = new MaintenanceWindow
        {
            Id = Guid.NewGuid(),
            RoomId = request.RoomId,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            Reason = request.Reason.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.MaintenanceWindows.Add(maintenanceWindow);
        await db.SaveChangesAsync(ct);

        return StatusCode(StatusCodes.Status201Created, new MaintenanceWindowResponse(
            maintenanceWindow.Id,
            maintenanceWindow.RoomId,
            room.Name,
            maintenanceWindow.StartsAt,
            maintenanceWindow.EndsAt,
            maintenanceWindow.Reason,
            affectedBookings,
            maintenanceWindow.CreatedAt));
    }

    /// <summary>List maintenance windows. Backs W-17.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Librarian)]
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
}

public sealed record CreateMaintenanceWindowRequest(
    Guid RoomId,
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
