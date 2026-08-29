using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHive.Api.Common;

namespace StudyHive.Api.Controllers.Approvals;

/// <summary>
/// S4: the staff-facing read model over the workflow runs S1 writes. S1 owns the rows and the orchestration; S4 owns these staff views of them.
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
[Route("api/workflow-executions")]
[Authorize]
public sealed class WorkflowExecutionsController : ControllerBase
{
    /// <summary>The single place this scaffold refuses. Replace the call, not this helper.</summary>
    private ObjectResult NotImplemented(string what) => Problem(
        type: "https://studyhive.dev/errors/not-implemented",
        title: "Not implemented yet",
        statusCode: StatusCodes.Status501NotImplemented,
        detail: $"{what} is owned by S4 (Costing, Validation, Approval and Audit) and has not been built yet.");

    /// <summary>List executions, failed runs first. Backs W-07.</summary>
    [HttpGet]
    [Authorize(Roles = $"{Roles.Librarian}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult List([FromQuery] PageQuery query) => NotImplemented("Listing workflow executions");

    /// <summary>Get one execution with its step logs. Backs W-06.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{Roles.Librarian}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult GetById(Guid id) => NotImplemented("Workflow execution detail");

    /// <summary>Step-by-step logs with each agent's own input and output. Never chain-of-thought - see the plan's 'what we do not store'.</summary>
    [HttpGet("{id:guid}/steps")]
    [Authorize(Roles = $"{Roles.Librarian}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Steps(Guid id) => NotImplemented("Workflow step logs");
}
