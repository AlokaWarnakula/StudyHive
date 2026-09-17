using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyHive.Api.Common;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;
using StudyHive.Api.Security;
using StudyHive.Api.Services;

namespace StudyHive.Api.Controllers.Rooms;

/// <summary>
/// S2: confirmed room bookings, created by the system after a librarian approves. The no_double_booking exclusion constraint on this table is the last line of defence inside S4's approval transaction.
///
/// House rules that already apply here (see DOCS/S2_S3_S4_UI_Interface_Map.md):
///   - Lists take [FromQuery] PageQuery and return PagedResult&lt;T&gt;. Unknown sortBy is a 400.
///   - Errors are RFC 7807 from the global handler. Never hand-roll an error body.
///   - Deletes are deactivations, not physical deletes.
/// </summary>
[ApiController]
[Route("api/room-bookings")]
[Authorize]
public sealed class RoomBookingsController(
    StudyHiveDbContext db,
    IRoomBookingService roomBookingService) : ControllerBase
{
    /// <summary>Create a booking after approval. Called by the approval transaction, not by a client.</summary>
    [HttpPost]
    [Authorize(Policy = "StaffOnly")]
    [ProducesResponseType(typeof(RoomBookingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateRoomBookingRequest request, CancellationToken ct)
    {
        if (request.RoomId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(request.RoomId), "Room id is required.");
        }

        if (request.BookingRequestId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(request.BookingRequestId), "Booking request id is required.");
        }

        if (request.EndsAt <= request.StartsAt)
        {
            ModelState.AddModelError(nameof(request.EndsAt), "End time must be later than start time.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await roomBookingService.CreateAsync(
            request.RoomId,
            request.BookingRequestId,
            request.StartsAt,
            request.EndsAt,
            ct);

        if (result.Succeeded)
        {
            var booking = result.Booking!;
            return StatusCode(StatusCodes.Status201Created, new RoomBookingResponse(
                booking.Id,
                booking.RoomId,
                result.RoomName!,
                booking.BookingRequestId,
                booking.StartsAt,
                booking.EndsAt,
                booking.CheckedInAt,
                booking.Status,
                booking.CreatedAt,
                booking.UpdatedAt));
        }

        return result.Failure switch
        {
            RoomBookingCreationFailure.RoomNotFound or
            RoomBookingCreationFailure.BookingRequestNotFound => NotFound(),

            RoomBookingCreationFailure.CapacityExceeded or
            RoomBookingCreationFailure.RequiredEquipmentUnavailable => Problem(
                type: "https://studyhive.dev/errors/validation",
                title: "Room does not satisfy the booking request",
                statusCode: StatusCodes.Status422UnprocessableEntity,
                detail: result.Detail),

            RoomBookingCreationFailure.InvalidTimeRange => ValidationProblem(),

            _ => Problem(
                type: "https://studyhive.dev/errors/conflict",
                title: "Room booking conflict",
                statusCode: StatusCodes.Status409Conflict,
                detail: result.Detail),
        };
    }

    /// <summary>QR check-in from the mobile app. Backs M-14 and M-15.</summary>
    [HttpPost("{id:guid}/check-in")]
    [ProducesResponseType(typeof(RoomCheckInResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CheckIn(Guid id, RoomCheckInRequest request, CancellationToken ct)
    {
        if (!User.TryGetUserId(out var callerId))
        {
            return Forbid();
        }

        var qrCode = request.QrCode.Trim();
        var bookings = db.RoomBookings
            .Include(b => b.Room)
            .Include(b => b.BookingRequest)
                .ThenInclude(r => r.Student);

        var booking = await bookings.SingleOrDefaultAsync(b => b.Id == id, ct);

        // The mobile booking screens are backed by BookingRequest records, so their navigation
        // carries that id. Resolve its confirmed room booking using the scanned room code while
        // continuing to accept the canonical RoomBooking id used by direct API clients.
        booking ??= await bookings
            .Where(b => b.BookingRequestId == id &&
                        b.Status == RoomBookingStatus.Confirmed &&
                        b.Room.QrCode == qrCode)
            .OrderBy(b => b.CheckedInAt != null)
            .ThenBy(b => b.StartsAt)
            .FirstOrDefaultAsync(ct);

        if (booking is null) return NotFound();

        if (booking.BookingRequest.Student.UserId != callerId)
        {
            return Forbid();
        }

        if (!string.Equals(booking.Room.QrCode, qrCode, StringComparison.Ordinal))
        {
            return Problem(
                type: "https://studyhive.dev/errors/validation",
                title: "Invalid room QR code",
                statusCode: StatusCodes.Status422UnprocessableEntity,
                detail: "The scanned code does not match the room assigned to this booking.");
        }

        if (booking.Status != RoomBookingStatus.Confirmed)
        {
            return Problem(
                type: "https://studyhive.dev/errors/conflict",
                title: "Booking cannot be checked in",
                statusCode: StatusCodes.Status409Conflict,
                detail: $"This booking is '{booking.Status}' and cannot be checked in.");
        }

        // A repeated mobile submission is safe: preserve the original check-in time and return it.
        if (booking.CheckedInAt is null)
        {
            booking.CheckedInAt = DateTimeOffset.UtcNow;
            booking.UpdatedAt = booking.CheckedInAt.Value;
            await db.SaveChangesAsync(ct);
        }

        return Ok(RoomCheckInResponse.From(booking));
    }
}
