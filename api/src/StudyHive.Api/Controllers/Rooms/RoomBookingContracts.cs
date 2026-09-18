using System.ComponentModel.DataAnnotations;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Controllers.Rooms;

public sealed record CreateRoomBookingRequest(
    Guid RoomId,
    Guid BookingRequestId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt);

public sealed record RoomBookingResponse(
    Guid Id,
    Guid RoomId,
    string RoomName,
    Guid BookingRequestId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    DateTimeOffset? CheckedInAt,
    RoomBookingStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class RoomCheckInRequest
{
    [Required, MaxLength(200)]
    public required string QrCode { get; init; }
}

public sealed record RoomCheckInResponse(
    Guid BookingId,
    Guid RoomId,
    string RoomName,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    DateTimeOffset CheckedInAt,
    RoomBookingStatus Status)
{
    public static RoomCheckInResponse From(RoomBooking booking) => new(
        booking.Id,
        booking.RoomId,
        booking.Room.Name,
        booking.StartsAt,
        booking.EndsAt,
        booking.CheckedInAt!.Value,
        booking.Status);
}
