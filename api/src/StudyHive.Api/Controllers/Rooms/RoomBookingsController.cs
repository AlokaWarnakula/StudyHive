using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
    IRoomBookingService roomBookingService,
    IOptions<CheckInOptions> checkInOptions) : ControllerBase
{
    /// <summary>Asia/Colombo, a fixed UTC+05:30 with no daylight saving; used only for the times
    /// shown in check-in messages.</summary>
    private static readonly TimeSpan Colombo = TimeSpan.FromMinutes(330);

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

        if (booking is null)
        {
            // AUDIT C-12: the id is the caller's own booking request with a confirmed room booking,
            // so the only thing wrong is the code — say so instead of a bare 404.
            var ownerUserId = await db.BookingRequests.AsNoTracking()
                .Where(r => r.Id == id && db.RoomBookings.Any(b => b.BookingRequestId == r.Id && b.Status == RoomBookingStatus.Confirmed))
                .Select(r => (Guid?)r.Student.UserId)
                .SingleOrDefaultAsync(ct);
            if (ownerUserId is null) return NotFound();
            if (ownerUserId != callerId) return Forbid();
            return InvalidQrCode();
        }

        if (booking.BookingRequest.Student.UserId != callerId)
        {
            return Forbid();
        }

        if (!string.Equals(booking.Room.QrCode, qrCode, StringComparison.Ordinal))
        {
            return InvalidQrCode();
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
            // AUDIT C-04: only from OpensMinutesBefore before the start until the end.
            var now = DateTimeOffset.UtcNow;
            var opensAt = booking.StartsAt.AddMinutes(-checkInOptions.Value.OpensMinutesBefore);
            if (now < opensAt || now > booking.EndsAt)
            {
                return Problem(
                    type: "https://studyhive.dev/errors/outside-check-in-window",
                    title: "Check-in is not open",
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    detail: now < opensAt
                        ? $"Check-in opens at {FormatColombo(opensAt)}, {checkInOptions.Value.OpensMinutesBefore} minutes before the booking starts."
                        : $"This booking ended at {FormatColombo(booking.EndsAt)}.");
            }

            booking.CheckedInAt = DateTimeOffset.UtcNow;
            booking.UpdatedAt = booking.CheckedInAt.Value;
            await db.SaveChangesAsync(ct);
        }

        return Ok(RoomCheckInResponse.From(booking));
    }

    private ObjectResult InvalidQrCode() => Problem(
        type: "https://studyhive.dev/errors/validation",
        title: "Invalid room QR code",
        statusCode: StatusCodes.Status422UnprocessableEntity,
        detail: "The scanned code does not match the room assigned to this booking.");

    private static string FormatColombo(DateTimeOffset instant) =>
        instant.ToOffset(Colombo).ToString("HH:mm 'on' ddd d MMM", CultureInfo.InvariantCulture);
}
