using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHive.Api.Common;

namespace StudyHive.Api.Controllers.Store;

/// <summary>
/// S3: suppliers and which consumables they supply.
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
[Route("api/suppliers")]
[Authorize]
public sealed class SuppliersController : ControllerBase
{
    /// <summary>The single place this scaffold refuses. Replace the call, not this helper.</summary>
    private ObjectResult NotImplemented(string what) => Problem(
        type: "https://studyhive.dev/errors/not-implemented",
        title: "Not implemented yet",
        statusCode: StatusCodes.Status501NotImplemented,
        detail: $"{what} is owned by S3 (Consumables and Stock) and has not been built yet.");

    /// <summary>Add a supplier.</summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.StoreOfficer},{Roles.Admin}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Create() => NotImplemented("Adding a supplier");

    /// <summary>List suppliers. Backs W-23.</summary>
    [HttpGet]
    [Authorize(Roles = $"{Roles.StoreOfficer}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult List([FromQuery] PageQuery query) => NotImplemented("Listing suppliers");

    /// <summary>Update a supplier.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.StoreOfficer}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Update(Guid id) => NotImplemented("Updating a supplier");
}
