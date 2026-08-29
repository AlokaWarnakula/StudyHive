using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHive.Api.Common;

namespace StudyHive.Api.Controllers.Approvals;

/// <summary>
/// Reporting. Each report belongs to the owner of the data it reports on, so this controller is shared: bookings is S4's, room-usage is S2's, consumable-usage is S3's.
///
/// SCAFFOLD ONLY - owned by S2, S3 and S4 (one action each), not implemented yet. Every action below returns 501 so the
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
[Route("api/reports")]
[Authorize]
public sealed class ReportsController : ControllerBase
{
    /// <summary>The single place this scaffold refuses. Replace the call, not this helper.</summary>
    private ObjectResult NotImplemented(string what) => Problem(
        type: "https://studyhive.dev/errors/not-implemented",
        title: "Not implemented yet",
        statusCode: StatusCodes.Status501NotImplemented,
        detail: $"{what} is owned by S2, S3 and S4 (one action each) and has not been built yet.");

    /// <summary>Booking analytics. Backs W-09. Owned by S4.</summary>
    [HttpGet("bookings")]
    [Authorize(Roles = $"{Roles.Librarian},{Roles.Admin}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Bookings() => NotImplemented("The bookings report");

    /// <summary>Room utilisation, peak hours and no-shows. Backs W-18. Owned by S2.</summary>
    [HttpGet("room-usage")]
    [Authorize(Roles = $"{Roles.Librarian}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult RoomUsage() => NotImplemented("The room usage report");

    /// <summary>Usage per item, cost and wastage. Backs W-24. Owned by S3.</summary>
    [HttpGet("consumable-usage")]
    [Authorize(Roles = $"{Roles.StoreOfficer}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult ConsumableUsage() => NotImplemented("The consumable usage report");
}
