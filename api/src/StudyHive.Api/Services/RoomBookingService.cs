using Microsoft.EntityFrameworkCore;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Services;

public enum RoomBookingCreationFailure
{
    None,
    InvalidTimeRange,
    RoomNotFound,
    BookingRequestNotFound,
    BookingRequestNotApproved,
    RoomInactive,
    CapacityExceeded,
    RequiredEquipmentUnavailable,
    MaintenanceConflict,
    BookingConflict,
}

public sealed record RoomBookingCreationResult(
    RoomBooking? Booking,
    string? RoomName,
    RoomBookingCreationFailure Failure,
    string? Detail)
{
    public bool Succeeded => Booking is not null;

    public static RoomBookingCreationResult Success(RoomBooking booking, string roomName) =>
        new(booking, roomName, RoomBookingCreationFailure.None, null);

    public static RoomBookingCreationResult Failed(RoomBookingCreationFailure failure, string detail) =>
        new(null, null, failure, detail);
}

/// <summary>
/// S2's boundary for creating confirmed room bookings. S4 calls this service from its approval
/// transaction so room conflict rules remain owned and implemented in one place.
/// </summary>
public interface IRoomBookingService
{
    Task<RoomBookingCreationResult> CreateAsync(
        Guid roomId,
        Guid bookingRequestId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        CancellationToken ct);
}

public sealed class RoomBookingService(StudyHiveDbContext db) : IRoomBookingService
{
    public async Task<RoomBookingCreationResult> CreateAsync(
        Guid roomId,
        Guid bookingRequestId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        CancellationToken ct)
    {
        if (endsAt <= startsAt)
        {
            return RoomBookingCreationResult.Failed(
                RoomBookingCreationFailure.InvalidTimeRange,
                "End time must be later than start time.");
        }

        // Npgsql requires UTC DateTimeOffset values for PostgreSQL timestamptz parameters.
        var startsAtUtc = startsAt.ToUniversalTime();
        var endsAtUtc = endsAt.ToUniversalTime();

        var room = await db.StudyRooms
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == roomId, ct);
        if (room is null)
        {
            return RoomBookingCreationResult.Failed(
                RoomBookingCreationFailure.RoomNotFound,
                "The selected room was not found.");
        }

        var bookingRequest = await db.BookingRequests
            .AsNoTracking()
            .Include(r => r.RequiredEquipment)
            .SingleOrDefaultAsync(r => r.Id == bookingRequestId, ct);
        if (bookingRequest is null)
        {
            return RoomBookingCreationResult.Failed(
                RoomBookingCreationFailure.BookingRequestNotFound,
                "The booking request was not found.");
        }

        if (bookingRequest.Status != BookingRequestStatus.Approved)
        {
            return RoomBookingCreationResult.Failed(
                RoomBookingCreationFailure.BookingRequestNotApproved,
                "Only an approved booking request can create a confirmed room booking.");
        }

        if (!room.IsActive)
        {
            return RoomBookingCreationResult.Failed(
                RoomBookingCreationFailure.RoomInactive,
                "The selected room is inactive.");
        }

        if (bookingRequest.GroupSize > room.Capacity)
        {
            return RoomBookingCreationResult.Failed(
                RoomBookingCreationFailure.CapacityExceeded,
                "The selected room does not have enough capacity for this booking request.");
        }

        if (bookingRequest.RequiredEquipment.Count > 0)
        {
            var installedEquipment = await db.RoomEquipment
                .AsNoTracking()
                .Where(e => e.RoomId == roomId)
                .ToDictionaryAsync(e => e.EquipmentTypeId, e => e.Quantity, ct);

            if (bookingRequest.RequiredEquipment.Any(required =>
                    !installedEquipment.TryGetValue(required.EquipmentTypeId, out var quantity) ||
                    quantity < required.QuantityRequired))
            {
                return RoomBookingCreationResult.Failed(
                    RoomBookingCreationFailure.RequiredEquipmentUnavailable,
                    "The selected room does not satisfy the request's equipment requirements.");
            }
        }

        var maintenanceConflict = await db.MaintenanceWindows.AsNoTracking().AnyAsync(w =>
            w.RoomId == roomId &&
            w.StartsAt < endsAtUtc &&
            w.EndsAt > startsAtUtc,
            ct);
        if (maintenanceConflict)
        {
            return RoomBookingCreationResult.Failed(
                RoomBookingCreationFailure.MaintenanceConflict,
                "The requested time overlaps a maintenance window for this room.");
        }

        var bookingConflict = await db.RoomBookings.AsNoTracking().AnyAsync(b =>
            b.RoomId == roomId &&
            b.Status == RoomBookingStatus.Confirmed &&
            b.StartsAt < endsAtUtc &&
            b.EndsAt > startsAtUtc,
            ct);
        if (bookingConflict)
        {
            return RoomBookingCreationResult.Failed(
                RoomBookingCreationFailure.BookingConflict,
                "The room already has a confirmed booking that overlaps this time.");
        }

        var now = DateTimeOffset.UtcNow;
        var booking = new RoomBooking
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            BookingRequestId = bookingRequestId,
            StartsAt = startsAtUtc,
            EndsAt = endsAtUtc,
            Status = RoomBookingStatus.Confirmed,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.RoomBookings.Add(booking);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ContainsPostgresExclusionViolation(ex))
        {
            // The database exclusion constraint closes the race between the conflict query above
            // and this insert when two approvals attempt the same room and slot concurrently.
            db.Entry(booking).State = EntityState.Detached;
            return RoomBookingCreationResult.Failed(
                RoomBookingCreationFailure.BookingConflict,
                "The room already has a confirmed booking that overlaps this time.");
        }

        return RoomBookingCreationResult.Success(booking, room.Name);
    }

    private static bool ContainsPostgresExclusionViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is Npgsql.PostgresException { SqlState: "23P01" })
            {
                return true;
            }
        }

        return false;
    }
}
