using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHive.Api.Common;

namespace StudyHive.Api.Controllers.Approvals;

/// <summary>
/// S4: the append-only audit log.
///
/// SCAFFOLD ONLY - owned by S4 (Costing, Validation, Approval and Audit), not implemented yet. Every action below returns 501 so the
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
[Route("api/audit-logs")]
[Authorize]
public sealed class AuditLogsController : ControllerBase
{
    /// <summary>The single place this scaffold refuses. Replace the call, not this helper.</summary>
    private ObjectResult NotImplemented(string what) => Problem(
        type: "https://studyhive.dev/errors/not-implemented",
        title: "Not implemented yet",
        statusCode: StatusCodes.Status501NotImplemented,
        detail: $"{what} is owned by S4 (Costing, Validation, Approval and Audit) and has not been built yet.");

    /// <summary>List audit entries with search and filter. Backs W-08. Append-only: there is deliberately no write endpoint here.</summary>
    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult List([FromQuery] PageQuery query) => NotImplemented("Listing audit logs");
}
