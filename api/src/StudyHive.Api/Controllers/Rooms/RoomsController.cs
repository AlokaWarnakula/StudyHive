using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHive.Api.Common;

namespace StudyHive.Api.Controllers.Rooms;

/// <summary>
/// S2: study rooms, their equipment and their schedule.
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
[Route("api/rooms")]
[Authorize]
public sealed class RoomsController : ControllerBase
{
    /// <summary>The single place this scaffold refuses. Replace the call, not this helper.</summary>
    private ObjectResult NotImplemented(string what) => Problem(
        type: "https://studyhive.dev/errors/not-implemented",
        title: "Not implemented yet",
        statusCode: StatusCodes.Status501NotImplemented,
        detail: $"{what} is owned by S2 (Rooms and Availability) and has not been built yet.");

    /// <summary>Create a room. W-13's add dialog posts here.</summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.Librarian},{Roles.Admin}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Create() => NotImplemented("Creating a room");

    /// <summary>List rooms with filter, sort and pagination. Backs W-13 and M-09.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult List([FromQuery] PageQuery query) => NotImplemented("Listing rooms");

    /// <summary>Search available rooms by criteria. The hardest query in the system: capacity, equipment, existing bookings and maintenance windows all at once.</summary>
    [HttpGet("available")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Available([FromQuery] PageQuery query) => NotImplemented("Room availability search");

    /// <summary>Get one room with its equipment. Backs W-14 and M-10.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult GetById(Guid id) => NotImplemented("Room detail");

    /// <summary>Update a room.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.Librarian},{Roles.Admin}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Update(Guid id) => NotImplemented("Updating a room");

    /// <summary>Deactivate a room. Rooms are never physically deleted.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Deactivate(Guid id) => NotImplemented("Deactivating a room");

    /// <summary>Room schedule for a date range. Backs W-15 and M-11.</summary>
    [HttpGet("{id:guid}/schedule")]
    [Authorize(Roles = $"{Roles.Librarian}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Schedule(Guid id) => NotImplemented("Room schedule");

    /// <summary>Assign a quantity of an equipment type to this room.</summary>
    [HttpPost("{id:guid}/equipment")]
    [Authorize(Roles = $"{Roles.Librarian}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult AssignEquipment(Guid id) => NotImplemented("Assigning equipment");

    /// <summary>Remove an equipment type from this room.</summary>
    [HttpDelete("{roomId:guid}/equipment/{equipmentTypeId:guid}")]
    [Authorize(Roles = $"{Roles.Librarian}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult RemoveEquipment(Guid roomId, Guid equipmentTypeId) => NotImplemented("Removing equipment");
}
