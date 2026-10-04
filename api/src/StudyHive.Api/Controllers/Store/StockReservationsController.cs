using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyHive.Api.Common;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;
using StudyHive.Api.Security;
using StudyHive.Api.Services;

namespace StudyHive.Api.Controllers.Store;

/// <summary>
/// S3: stock reservations. Status only ever moves Pending/Reserved -> Released or -> Used
/// (<c>stock_reservations</c> CHECK constraint — DOCS/S2_S3_S4_UI_Interface_Map.md "Reservation
/// status" note). Creating a reservation is S3's business operation beyond CRUD: it must not
/// oversell under concurrent callers, and that guarantee lives in <see cref="IConsumableStockService"/>,
/// not here — this controller only translates its result into HTTP.
/// </summary>
[ApiController]
[Route("api/stock-reservations")]
[Authorize]
public sealed class StockReservationsController(StudyHiveDbContext db, IConsumableStockService stockService) : ControllerBase
{
    /// <summary>Create a reservation transactionally. Must not oversell under concurrent callers.</summary>
    [HttpPost]
    [Authorize(Policy = "StaffOnly")]
    [ProducesResponseType(typeof(StockReservationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateStockReservationRequest request, CancellationToken ct)
    {
        var result = await stockService.ReserveAsync(request.BookingRequestItemId, User.GetUserId(), ct);

        return result.Outcome switch
        {
            StockOperationOutcome.Success => CreatedAtAction(nameof(List), null, StockReservationResponse.From(result.Reservation!)),
            StockOperationOutcome.BookingRequestItemNotFound => NotFound(),
            StockOperationOutcome.AlreadyReserved => Problem(
                type: "https://studyhive.dev/errors/conflict",
                title: "Already reserved",
                statusCode: StatusCodes.Status409Conflict,
                detail: result.Detail),
            StockOperationOutcome.InsufficientStock => Problem(
                type: "https://studyhive.dev/errors/insufficient-stock",
                title: "Insufficient stock",
                statusCode: StatusCodes.Status409Conflict,
                detail: result.Detail),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError, detail: result.Detail),
        };
    }

    /// <summary>Backs W-22. `status` filters on the database's own four values — see the reservation
    /// status note in DOCS/S2_S3_S4_UI_Interface_Map.md before adding a fifth.</summary>
    [HttpGet]
    [Authorize(Roles = $"{Roles.StoreOfficer},{Roles.Librarian},{Roles.Admin}")] // CW-05
    [ProducesResponseType(typeof(PagedResult<StockReservationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] PageQuery query, [FromQuery] string? status, [FromQuery] DateOnly? dueOn, CancellationToken ct)
    {
        IQueryable<StockReservation> reservations = db.StockReservations.AsNoTracking();

        // CW-07 "Due today": reservations whose request has a booked room slot starting on that
        // Asia/Colombo day (fixed UTC+05:30).
        if (dueOn is { } day)
        {
            var dayStart = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.FromMinutes(330)).ToUniversalTime();
            var dayEnd = dayStart.AddDays(1);
            reservations = reservations.Where(r => db.RoomBookings.Any(b =>
                b.BookingRequestId == r.BookingRequestItem.BookingRequestId &&
                b.Status != RoomBookingStatus.Cancelled &&
                b.StartsAt >= dayStart && b.StartsAt < dayEnd));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<StockReservationStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                ModelState.AddModelError(nameof(status), $"Unknown status value '{status}'.");
                return ValidationProblem(ModelState);
            }
            reservations = reservations.Where(r => r.Status == parsedStatus);
        }

        var sortDescending = !string.Equals(query.SortDir, "asc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<StockReservation>? sorted = query.SortBy?.ToLowerInvariant() switch
        {
            null or "" or "createdat" => sortDescending ? reservations.OrderByDescending(r => r.CreatedAt) : reservations.OrderBy(r => r.CreatedAt),
            "status" => sortDescending ? reservations.OrderByDescending(r => r.Status) : reservations.OrderBy(r => r.Status),
            "consumable" => sortDescending ? reservations.OrderByDescending(r => r.Consumable.Name) : reservations.OrderBy(r => r.Consumable.Name),
            _ => null,
        };
        if (sorted is null)
        {
            ModelState.AddModelError(nameof(query.SortBy), $"Unknown sortBy value '{query.SortBy}'.");
            return ValidationProblem(ModelState);
        }
        reservations = sorted;

        var totalItems = await reservations.CountAsync(ct);
        var items = await reservations
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(r => new
            {
                Reservation = r,
                ConsumableName = r.Consumable.Name,
                r.BookingRequestItem.BookingRequestId,
                r.BookingRequestItem.BookingRequest.Objective,
                StudentName = r.BookingRequestItem.BookingRequest.Student.User.FullName,
                Slot = db.RoomBookings
                    .Where(b => b.BookingRequestId == r.BookingRequestItem.BookingRequestId && b.Status != RoomBookingStatus.Cancelled)
                    .OrderBy(b => b.StartsAt)
                    .Select(b => new { RoomName = b.Room.Name, b.StartsAt, b.EndsAt })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        var responses = items.Select(x => new StockReservationResponse
        {
            Id = x.Reservation.Id,
            BookingRequestItemId = x.Reservation.BookingRequestItemId,
            ConsumableId = x.Reservation.ConsumableId,
            ConsumableName = x.ConsumableName,
            Quantity = x.Reservation.Quantity,
            Status = x.Reservation.Status,
            ReservedAt = x.Reservation.ReservedAt,
            ReleasedAt = x.Reservation.ReleasedAt,
            UsedAt = x.Reservation.UsedAt,
            CreatedAt = x.Reservation.CreatedAt,
            BookingRequestId = x.BookingRequestId,
            RequestObjective = x.Objective,
            StudentName = x.StudentName,
            RoomName = x.Slot?.RoomName,
            SlotStartsAt = x.Slot?.StartsAt,
            SlotEndsAt = x.Slot?.EndsAt,
        }).ToList();

        return Ok(PagedResult<StockReservationResponse>.Create(responses, query.Page, query.PageSize, totalItems));
    }

    /// <summary>Release a reservation and return the stock. Only a 'Reserved' reservation can be released.</summary>
    [HttpPut("{id:guid}/release")]
    [Authorize(Roles = Roles.StoreOfficer)]
    [ProducesResponseType(typeof(StockReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Release(Guid id, CancellationToken ct)
    {
        var result = await stockService.ReleaseAsync(id, User.GetUserId(), ct);

        return result.Outcome switch
        {
            StockOperationOutcome.Success => Ok(StockReservationResponse.From(result.Reservation!)),
            StockOperationOutcome.ReservationNotFound => NotFound(),
            StockOperationOutcome.InvalidState => Problem(
                type: "https://studyhive.dev/errors/conflict",
                title: "Reservation cannot be released",
                statusCode: StatusCodes.Status409Conflict,
                detail: result.Detail),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError, detail: result.Detail),
        };
    }

    /// <summary>Mark a reservation as issued/used — this is when the consumable actually leaves the store.</summary>
    [HttpPut("{id:guid}/use")]
    [Authorize(Roles = Roles.StoreOfficer)]
    [ProducesResponseType(typeof(StockReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkUsed(Guid id, CancellationToken ct)
    {
        var result = await stockService.MarkUsedAsync(id, User.GetUserId(), ct);

        return result.Outcome switch
        {
            StockOperationOutcome.Success => Ok(StockReservationResponse.From(result.Reservation!)),
            StockOperationOutcome.ReservationNotFound => NotFound(),
            StockOperationOutcome.InvalidState => Problem(
                type: "https://studyhive.dev/errors/conflict",
                title: "Reservation cannot be marked used",
                statusCode: StatusCodes.Status409Conflict,
                detail: result.Detail),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError, detail: result.Detail),
        };
    }
}
