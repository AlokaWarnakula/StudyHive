using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyHive.Api.Common;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Controllers.Rooms;

/// <summary>
/// S2: study rooms, their equipment and their schedule.
/// </summary>
[ApiController]
[Route("api/rooms")]
[Authorize]
public sealed class RoomsController(StudyHiveDbContext db) : ControllerBase
{
    /// <summary>
    /// Create a room.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.Librarian},{Roles.Admin}")]
    [ProducesResponseType(typeof(RoomResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateRoomRequest request,
        CancellationToken ct)
    {
        if (!ValidateRoom(request.Name, request.Building, request.Capacity, request.HourlyRate, request.QrCode))
        {
            return ValidationProblem(ModelState);
        }

        var name = request.Name.Trim();
        var building = request.Building.Trim();
        var qrCode = request.QrCode.Trim();

        var duplicateName = await db.StudyRooms
            .AnyAsync(r => r.Name == name, ct);

        if (duplicateName)
        {
            ModelState.AddModelError(
                nameof(request.Name),
                "A room with this name already exists.");

            return ValidationProblem(ModelState);
        }

        var duplicateQrCode = await db.StudyRooms
            .AnyAsync(r => r.QrCode == qrCode, ct);

        if (duplicateQrCode)
        {
            ModelState.AddModelError(
                nameof(request.QrCode),
                "A room with this QR code already exists.");

            return ValidationProblem(ModelState);
        }

        var now = DateTimeOffset.UtcNow;

        var room = new StudyRoom
        {
            Id = Guid.NewGuid(),
            Name = name,
            Building = building,
            Floor = request.Floor,
            Capacity = request.Capacity,
            HourlyRate = request.HourlyRate,
            QrCode = qrCode,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.StudyRooms.Add(room);

        await db.SaveChangesAsync(ct);

        var response = ToResponse(room);

        return CreatedAtAction(
            nameof(GetById),
            new { id = room.Id },
            response);
    }

    /// <summary>
    /// List rooms with search, sorting and pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<RoomResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] PageQuery query,
        [FromQuery] int? capacity,
        [FromQuery] Guid? equipmentTypeId,
        CancellationToken ct)
    {
        IQueryable<StudyRoom> rooms = db.StudyRooms
            .AsNoTracking();

        if (capacity is <= 0)
        {
            ModelState.AddModelError(nameof(capacity), "Capacity must be greater than 0.");
            return ValidationProblem(ModelState);
        }

        if (capacity is not null)
        {
            rooms = rooms.Where(r => r.Capacity >= capacity.Value);
        }

        if (equipmentTypeId is not null)
        {
            rooms = rooms.Where(r => r.Equipment.Any(e =>
                e.EquipmentTypeId == equipmentTypeId.Value && e.Quantity > 0));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var searchPattern = $"%{query.Search.Trim()}%";

            rooms = rooms.Where(r =>
                EF.Functions.ILike(r.Name, searchPattern) ||
                EF.Functions.ILike(r.Building, searchPattern));
        }

        var sortDir = query.SortDir.ToLowerInvariant();

        if (sortDir is not ("asc" or "desc"))
        {
            ModelState.AddModelError(
                nameof(query.SortDir),
                "sortDir must be either 'asc' or 'desc'.");

            return ValidationProblem(ModelState);
        }

        var descending = sortDir == "desc";

        rooms = query.SortBy?.ToLowerInvariant() switch
        {
            null or "" or "createdat" =>
                descending
                    ? rooms.OrderByDescending(r => r.CreatedAt)
                    : rooms.OrderBy(r => r.CreatedAt),

            "name" =>
                descending
                    ? rooms.OrderByDescending(r => r.Name)
                    : rooms.OrderBy(r => r.Name),

            "building" =>
                descending
                    ? rooms.OrderByDescending(r => r.Building)
                    : rooms.OrderBy(r => r.Building),

            "floor" =>
                descending
                    ? rooms.OrderByDescending(r => r.Floor)
                    : rooms.OrderBy(r => r.Floor),

            "capacity" =>
                descending
                    ? rooms.OrderByDescending(r => r.Capacity)
                    : rooms.OrderBy(r => r.Capacity),

            "hourlyrate" =>
                descending
                    ? rooms.OrderByDescending(r => r.HourlyRate)
                    : rooms.OrderBy(r => r.HourlyRate),

            _ => null!
        };

        if (rooms is null)
        {
            ModelState.AddModelError(
                nameof(query.SortBy),
                $"Unknown sortBy value '{query.SortBy}'.");

            return ValidationProblem(ModelState);
        }

        var totalItems = await rooms.CountAsync(ct);

        var items = await rooms
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(r => new RoomResponse(
                r.Id,
                r.Name,
                r.Building,
                r.Floor,
                r.Capacity,
                r.HourlyRate,
                r.QrCode,
                r.IsActive,
                r.CreatedAt,
                r.UpdatedAt))
            .ToListAsync(ct);

        var result = PagedResult<RoomResponse>.Create(
            items,
            query.Page,
            query.PageSize,
            totalItems);

        return Ok(result);
    }

    /// <summary>
    /// Search available rooms by criteria.
    /// </summary>
    [HttpGet("available")]
    [ProducesResponseType(typeof(PagedResult<RoomResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Available(
        [FromQuery] PageQuery query,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int? capacity,
        [FromQuery] Guid? equipmentTypeId,
        CancellationToken ct)
    {
        if (from is null)
        {
            ModelState.AddModelError(nameof(from), "Start time is required.");
        }

        if (to is null)
        {
            ModelState.AddModelError(nameof(to), "End time is required.");
        }

        if (from is not null && to is not null && to <= from)
        {
            ModelState.AddModelError(nameof(to), "End time must be later than start time.");
        }

        if (capacity is <= 0)
        {
            ModelState.AddModelError(nameof(capacity), "Capacity must be greater than 0.");
        }

        var direction = query.SortDir.ToLowerInvariant();
        if (direction is not ("asc" or "desc"))
        {
            ModelState.AddModelError(nameof(query.SortDir), "sortDir must be either 'asc' or 'desc'.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var startsAt = from!.Value;
        var endsAt = to!.Value;

        IQueryable<StudyRoom> rooms = db.StudyRooms
            .AsNoTracking()
            .Where(r =>
                r.IsActive &&
                !r.Bookings.Any(b =>
                    b.Status == RoomBookingStatus.Confirmed &&
                    b.StartsAt < endsAt &&
                    b.EndsAt > startsAt) &&
                !r.MaintenanceWindows.Any(w =>
                    w.StartsAt < endsAt &&
                    w.EndsAt > startsAt));

        if (capacity is not null)
        {
            rooms = rooms.Where(r => r.Capacity >= capacity.Value);
        }

        if (equipmentTypeId is not null)
        {
            rooms = rooms.Where(r => r.Equipment.Any(e =>
                e.EquipmentTypeId == equipmentTypeId.Value &&
                e.Quantity > 0 &&
                e.EquipmentType.IsActive));
        }

        var descending = direction == "desc";
        rooms = query.SortBy?.ToLowerInvariant() switch
        {
            null or "" or "createdat" => descending
                ? rooms.OrderByDescending(r => r.CreatedAt)
                : rooms.OrderBy(r => r.CreatedAt),
            "name" => descending
                ? rooms.OrderByDescending(r => r.Name)
                : rooms.OrderBy(r => r.Name),
            "building" => descending
                ? rooms.OrderByDescending(r => r.Building)
                : rooms.OrderBy(r => r.Building),
            "floor" => descending
                ? rooms.OrderByDescending(r => r.Floor)
                : rooms.OrderBy(r => r.Floor),
            "capacity" => descending
                ? rooms.OrderByDescending(r => r.Capacity)
                : rooms.OrderBy(r => r.Capacity),
            "hourlyrate" => descending
                ? rooms.OrderByDescending(r => r.HourlyRate)
                : rooms.OrderBy(r => r.HourlyRate),
            _ => null!,
        };

        if (rooms is null)
        {
            ModelState.AddModelError(nameof(query.SortBy), $"Unknown sortBy value '{query.SortBy}'.");
            return ValidationProblem(ModelState);
        }

        var totalItems = await rooms.CountAsync(ct);
        var items = await rooms
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(r => new RoomResponse(
                r.Id,
                r.Name,
                r.Building,
                r.Floor,
                r.Capacity,
                r.HourlyRate,
                r.QrCode,
                r.IsActive,
                r.CreatedAt,
                r.UpdatedAt))
            .ToListAsync(ct);

        return Ok(PagedResult<RoomResponse>.Create(
            items, query.Page, query.PageSize, totalItems));
    }

    /// <summary>
    /// Get one room with its equipment.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RoomDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var room = await db.StudyRooms
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new RoomDetailResponse(
                r.Id,
                r.Name,
                r.Building,
                r.Floor,
                r.Capacity,
                r.HourlyRate,
                r.QrCode,
                r.IsActive,
                r.CreatedAt,
                r.UpdatedAt,
                r.Equipment
                    .OrderBy(e => e.EquipmentType.Name)
                    .Select(e => new RoomEquipmentResponse(
                        e.EquipmentTypeId,
                        e.EquipmentType.Name,
                        e.Quantity))
                    .ToList()))
            .SingleOrDefaultAsync(ct);

        return room is null ? NotFound() : Ok(room);
    }

    /// <summary>
    /// Update a room.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.Librarian},{Roles.Admin}")]
    [ProducesResponseType(typeof(RoomResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateRoomRequest request,
        CancellationToken ct)
    {
        if (!ValidateRoom(request.Name, request.Building, request.Capacity, request.HourlyRate, request.QrCode))
        {
            return ValidationProblem(ModelState);
        }

        var room = await db.StudyRooms.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (room is null)
        {
            return NotFound();
        }

        var name = request.Name.Trim();
        var building = request.Building.Trim();
        var qrCode = request.QrCode.Trim();

        if (await db.StudyRooms.AnyAsync(r => r.Id != id && r.Name == name, ct))
        {
            ModelState.AddModelError(nameof(request.Name), "A room with this name already exists.");
        }

        if (await db.StudyRooms.AnyAsync(r => r.Id != id && r.QrCode == qrCode, ct))
        {
            ModelState.AddModelError(nameof(request.QrCode), "A room with this QR code already exists.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        room.Name = name;
        room.Building = building;
        room.Floor = request.Floor;
        room.Capacity = request.Capacity;
        room.HourlyRate = request.HourlyRate;
        room.QrCode = qrCode;
        room.IsActive = request.IsActive;
        room.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return Ok(ToResponse(room));
    }

    /// <summary>
    /// Deactivate a room.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var room = await db.StudyRooms.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (room is null)
        {
            return NotFound();
        }

        if (room.IsActive)
        {
            room.IsActive = false;
            room.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    /// <summary>
    /// Room schedule for a date range.
    /// </summary>
    [HttpGet("{id:guid}/schedule")]
    [ProducesResponseType(typeof(IReadOnlyList<RoomScheduleSlotResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Schedule(
        Guid id,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        if (from is null)
        {
            ModelState.AddModelError(nameof(from), "Start time is required.");
        }

        if (to is null)
        {
            ModelState.AddModelError(nameof(to), "End time is required.");
        }

        if (from is not null && to is not null && to <= from)
        {
            ModelState.AddModelError(nameof(to), "End time must be later than start time.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var roomName = await db.StudyRooms
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => r.Name)
            .SingleOrDefaultAsync(ct);

        if (roomName is null)
        {
            return NotFound();
        }

        var startsAt = from!.Value;
        var endsAt = to!.Value;

        var bookingSlots = await db.RoomBookings
            .AsNoTracking()
            .Where(b =>
                b.RoomId == id &&
                b.Status == RoomBookingStatus.Confirmed &&
                b.StartsAt < endsAt &&
                b.EndsAt > startsAt)
            .Select(b => new RoomScheduleSlotResponse(
                b.RoomId,
                roomName,
                b.StartsAt,
                b.EndsAt,
                "Booked"))
            .ToListAsync(ct);

        var maintenanceSlots = await db.MaintenanceWindows
            .AsNoTracking()
            .Where(w =>
                w.RoomId == id &&
                w.StartsAt < endsAt &&
                w.EndsAt > startsAt)
            .Select(w => new RoomScheduleSlotResponse(
                w.RoomId,
                roomName,
                w.StartsAt,
                w.EndsAt,
                "Maintenance"))
            .ToListAsync(ct);

        return Ok(bookingSlots
            .Concat(maintenanceSlots)
            .OrderBy(slot => slot.StartsAt)
            .ThenBy(slot => slot.EndsAt)
            .ToList());
    }

    /// <summary>
    /// Assign equipment to a room.
    /// </summary>
    [HttpPost("{id:guid}/equipment")]
    [Authorize(Roles = Roles.Librarian)]
    [ProducesResponseType(typeof(RoomEquipmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignEquipment(
        Guid id,
        [FromBody] AssignRoomEquipmentRequest request,
        CancellationToken ct)
    {
        if (request.Quantity <= 0)
        {
            ModelState.AddModelError(nameof(request.Quantity), "Quantity must be greater than 0.");
            return ValidationProblem(ModelState);
        }

        var roomExists = await db.StudyRooms.AnyAsync(r => r.Id == id, ct);
        var equipment = await db.EquipmentTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == request.EquipmentTypeId, ct);

        if (!roomExists || equipment is null)
        {
            return NotFound();
        }

        if (await db.RoomEquipment.AnyAsync(
                e => e.RoomId == id && e.EquipmentTypeId == request.EquipmentTypeId,
                ct))
        {
            return Problem(
                type: "https://studyhive.dev/errors/equipment-already-assigned",
                title: "Equipment already assigned",
                statusCode: StatusCodes.Status409Conflict,
                detail: "This equipment type is already assigned to the room.");
        }

        var assignment = new RoomEquipment
        {
            RoomId = id,
            EquipmentTypeId = request.EquipmentTypeId,
            Quantity = request.Quantity,
            InstalledAt = DateTimeOffset.UtcNow,
        };

        db.RoomEquipment.Add(assignment);
        await db.SaveChangesAsync(ct);

        return StatusCode(StatusCodes.Status201Created, new RoomEquipmentResponse(
            equipment.Id,
            equipment.Name,
            assignment.Quantity));
    }

    /// <summary>
    /// Remove equipment from a room.
    /// </summary>
    [HttpDelete("{roomId:guid}/equipment/{equipmentTypeId:guid}")]
    [Authorize(Roles = Roles.Librarian)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveEquipment(
        Guid roomId,
        Guid equipmentTypeId,
        CancellationToken ct)
    {
        var assignment = await db.RoomEquipment.SingleOrDefaultAsync(
            e => e.RoomId == roomId && e.EquipmentTypeId == equipmentTypeId,
            ct);

        if (assignment is null)
        {
            return NotFound();
        }

        db.RoomEquipment.Remove(assignment);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private bool ValidateRoom(
        string? name,
        string? building,
        int capacity,
        decimal hourlyRate,
        string? qrCode)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(nameof(name), "Room name is required.");
        }
        else if (name.Trim().Length > 60)
        {
            ModelState.AddModelError(nameof(name), "Room name cannot exceed 60 characters.");
        }

        if (string.IsNullOrWhiteSpace(building))
        {
            ModelState.AddModelError(nameof(building), "Building is required.");
        }
        else if (building.Trim().Length > 60)
        {
            ModelState.AddModelError(nameof(building), "Building cannot exceed 60 characters.");
        }

        if (string.IsNullOrWhiteSpace(qrCode))
        {
            ModelState.AddModelError(nameof(qrCode), "QR code is required.");
        }
        else if (qrCode.Trim().Length > 64)
        {
            ModelState.AddModelError(nameof(qrCode), "QR code cannot exceed 64 characters.");
        }

        if (capacity <= 0)
        {
            ModelState.AddModelError(nameof(capacity), "Capacity must be greater than 0.");
        }

        if (hourlyRate < 0)
        {
            ModelState.AddModelError(nameof(hourlyRate), "Hourly rate cannot be negative.");
        }

        return ModelState.IsValid;
    }

    private static RoomResponse ToResponse(StudyRoom room) => new(
        room.Id,
        room.Name,
        room.Building,
        room.Floor,
        room.Capacity,
        room.HourlyRate,
        room.QrCode,
        room.IsActive,
        room.CreatedAt,
        room.UpdatedAt);
}

/// <summary>
/// Request body used when creating a room.
/// </summary>
public sealed record CreateRoomRequest(
    string Name,
    string Building,
    int Floor,
    int Capacity,
    decimal HourlyRate,
    string QrCode);

/// <summary>
/// Request body used when editing a room. Setting IsActive to false is a reversible
/// deactivation; room records are never physically deleted.
/// </summary>
public sealed record UpdateRoomRequest(
    string Name,
    string Building,
    int Floor,
    int Capacity,
    decimal HourlyRate,
    string QrCode,
    bool IsActive);

/// <summary>Request body used to install an equipment type in a room.</summary>
public sealed record AssignRoomEquipmentRequest(
    Guid EquipmentTypeId,
    int Quantity);

/// <summary>
/// Room data returned by the API.
/// </summary>
public sealed record RoomResponse(
    Guid Id,
    string Name,
    string Building,
    int Floor,
    int Capacity,
    decimal HourlyRate,
    string QrCode,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>One installed equipment type in the room detail response.</summary>
public sealed record RoomEquipmentResponse(
    Guid EquipmentTypeId,
    string Name,
    int Quantity);

/// <summary>Room details plus the equipment currently assigned to it.</summary>
public sealed record RoomDetailResponse(
    Guid Id,
    string Name,
    string Building,
    int Floor,
    int Capacity,
    decimal HourlyRate,
    string QrCode,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<RoomEquipmentResponse> Equipment);

/// <summary>A booking or maintenance period displayed on a room schedule.</summary>
public sealed record RoomScheduleSlotResponse(
    Guid RoomId,
    string RoomName,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Kind);
