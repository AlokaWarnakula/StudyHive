using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHive.Api.Common;

namespace StudyHive.Api.Controllers.Store;

/// <summary>
/// S3: stock reservations. The lifecycle is held to confirmed to issued to released, and the no-oversell rule has to hold under concurrency - that is S3's business operation, and its concurrency test must genuinely pass.
///
/// SCAFFOLD ONLY - owned by S3 (Consumables and Stock), not implemented yet. Every action below returns 501 so the
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
[Route("api/stock-reservations")]
[Authorize]
public sealed class StockReservationsController : ControllerBase
{
    /// <summary>The single place this scaffold refuses. Replace the call, not this helper.</summary>
    private ObjectResult NotImplemented(string what) => Problem(
        type: "https://studyhive.dev/errors/not-implemented",
        title: "Not implemented yet",
        statusCode: StatusCodes.Status501NotImplemented,
        detail: $"{what} is owned by S3 (Consumables and Stock) and has not been built yet.");

    /// <summary>Create a reservation transactionally. Must not oversell under concurrent callers.</summary>
    [HttpPost]
    [Authorize(Policy = "StaffOnly")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Create() => NotImplemented("Creating a stock reservation");

    /// <summary>List reservations. Backs W-22.</summary>
    [HttpGet]
    [Authorize(Roles = $"{Roles.StoreOfficer},{Roles.Librarian}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult List([FromQuery] PageQuery query) => NotImplemented("Listing stock reservations");

    /// <summary>Release a reservation and return the stock.</summary>
    [HttpPut("{id:guid}/release")]
    [Authorize(Roles = $"{Roles.StoreOfficer}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Release(Guid id) => NotImplemented("Releasing a reservation");

    /// <summary>Mark a reservation as issued/used.</summary>
    [HttpPut("{id:guid}/use")]
    [Authorize(Roles = $"{Roles.StoreOfficer}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult MarkUsed(Guid id) => NotImplemented("Marking a reservation used");
}
