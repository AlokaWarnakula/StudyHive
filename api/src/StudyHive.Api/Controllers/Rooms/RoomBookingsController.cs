using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHive.Api.Common;

namespace StudyHive.Api.Controllers.Rooms;

/// <summary>
/// S2: confirmed room bookings, created by the system after a librarian approves. The no_double_booking exclusion constraint on this table is the last line of defence inside S4's approval transaction.
///
/// SCAFFOLD ONLY - owned by S2 (Rooms and Availability), not implemented yet. Every action below returns 501 so the
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
[Route("api/room-bookings")]
[Authorize]
public sealed class RoomBookingsController : ControllerBase
{
    /// <summary>The single place this scaffold refuses. Replace the call, not this helper.</summary>
    private ObjectResult NotImplemented(string what) => Problem(
        type: "https://studyhive.dev/errors/not-implemented",
        title: "Not implemented yet",
        statusCode: StatusCodes.Status501NotImplemented,
        detail: $"{what} is owned by S2 (Rooms and Availability) and has not been built yet.");

    /// <summary>Create a booking after approval. Called by the approval transaction, not by a client.</summary>
    [HttpPost]
    [Authorize(Policy = "StaffOnly")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Create() => NotImplemented("Creating a room booking");

    /// <summary>QR check-in from the mobile app. Backs M-14 and M-15.</summary>
    [HttpPost("{id:guid}/check-in")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult CheckIn(Guid id) => NotImplemented("QR check-in");
}
