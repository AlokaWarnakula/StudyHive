using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyHive.Api.Common;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Controllers.Approvals;

/// <summary>
/// S4: the staff-facing read model over the workflow runs S1 writes (W-06 viewer, W-07 history).
/// S1 owns the rows and the orchestration; these are read-only views of them.
///
/// Step logs carry each agent's own tool input, output, validation result, timing and error —
/// never chain-of-thought or raw prompts (the plan's "what we do not store").
///
/// House rules (DOCS/S2_S3_S4_UI_Interface_Map.md): lists take [FromQuery] PageQuery and return
/// PagedResult&lt;T&gt; with filtering, sorting, counting and paging in SQL; unknown sortBy is a 400;
/// errors are RFC 7807 from the global handler.
/// </summary>
[ApiController]
[Route("api/workflow-executions")]
[Authorize]
public sealed class WorkflowExecutionsController(StudyHiveDbContext db) : ControllerBase
{
    /// <summary>List executions, failed runs first. `status` is a workflow status; `errorCode`
    /// matches exactly (e.g. VALIDATION_FAILED); `search` matches the objective. Backs W-07.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Librarian)]
    [ProducesResponseType(typeof(PagedResult<WorkflowExecutionSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] PageQuery query,
        [FromQuery] string? status,
        [FromQuery] string? errorCode,
        CancellationToken ct)
    {
        IQueryable<WorkflowExecution> executions = db.WorkflowExecutions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<WorkflowStatus>(status, ignoreCase: true, out var parsed))
            {
                ModelState.AddModelError(nameof(status), $"Unknown status value '{status}'.");
                return ValidationProblem(ModelState);
            }
            executions = executions.Where(w => w.Status == parsed);
        }
        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            var code = errorCode.Trim();
            executions = executions.Where(w => w.ErrorCode == code);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{query.Search.Trim()}%";
            executions = executions.Where(w => EF.Functions.ILike(w.Objective, term));
        }

        // Failed runs first always; sortBy orders within each group.
        var failedFirst = executions.OrderBy(w => w.Status == WorkflowStatus.Failed ? 0 : 1);
        var descending = !string.Equals(query.SortDir, "asc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<WorkflowExecution>? sorted = query.SortBy?.ToLowerInvariant() switch
        {
            null or "" or "startedat" => descending ? failedFirst.ThenByDescending(w => w.StartedAt) : failedFirst.ThenBy(w => w.StartedAt),
            "completedat" => descending ? failedFirst.ThenByDescending(w => w.CompletedAt) : failedFirst.ThenBy(w => w.CompletedAt),
            "status" => descending ? failedFirst.ThenByDescending(w => w.Status) : failedFirst.ThenBy(w => w.Status),
            _ => null,
        };
        if (sorted is null)
        {
            ModelState.AddModelError(nameof(query.SortBy), $"Unknown sortBy value '{query.SortBy}'.");
            return ValidationProblem(ModelState);
        }

        var totalItems = await sorted.CountAsync(ct);
        var items = await sorted.ThenBy(w => w.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(w => new WorkflowExecutionSummaryResponse
            {
                Id = w.Id,
                BookingRequestId = w.BookingRequestId,
                Objective = w.Objective,
                Status = w.Status,
                CurrentStep = w.CurrentStep,
                TotalSteps = w.TotalSteps,
                ErrorCode = w.ErrorCode,
                ErrorMessage = w.ErrorMessage,
                StartedAt = w.StartedAt,
                CompletedAt = w.CompletedAt,
                UpdatedAt = w.UpdatedAt,
            })
            .ToListAsync(ct);

        return Ok(PagedResult<WorkflowExecutionSummaryResponse>.Create(items, query.Page, query.PageSize, totalItems));
    }

    /// <summary>Get one execution with its plan and step logs. Backs W-06.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = Roles.Librarian)]
    [ProducesResponseType(typeof(WorkflowExecutionDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var execution = await db.WorkflowExecutions.AsNoTracking()
            .Where(w => w.Id == id)
            .Select(w => new
            {
                Summary = new WorkflowExecutionSummaryResponse
                {
                    Id = w.Id,
                    BookingRequestId = w.BookingRequestId,
                    Objective = w.Objective,
                    Status = w.Status,
                    CurrentStep = w.CurrentStep,
                    TotalSteps = w.TotalSteps,
                    ErrorCode = w.ErrorCode,
                    ErrorMessage = w.ErrorMessage,
                    StartedAt = w.StartedAt,
                    CompletedAt = w.CompletedAt,
                    UpdatedAt = w.UpdatedAt,
                },
                w.PlanJson,
            })
            .SingleOrDefaultAsync(ct);
        if (execution is null)
        {
            return NotFound();
        }

        return Ok(new WorkflowExecutionDetailResponse
        {
            Execution = execution.Summary,
            Plan = ParseJson(execution.PlanJson),
            Steps = await LoadStepsAsync(id, ct),
        });
    }

    /// <summary>Step-by-step logs with each agent's own input and output. Never chain-of-thought - see the plan's 'what we do not store'.</summary>
    [HttpGet("{id:guid}/steps")]
    [Authorize(Roles = Roles.Librarian)]
    [ProducesResponseType(typeof(IReadOnlyList<WorkflowStepResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Steps(Guid id, CancellationToken ct)
    {
        if (!await db.WorkflowExecutions.AnyAsync(w => w.Id == id, ct))
        {
            return NotFound();
        }
        return Ok(await LoadStepsAsync(id, ct));
    }

    private async Task<IReadOnlyList<WorkflowStepResponse>> LoadStepsAsync(Guid executionId, CancellationToken ct)
    {
        var rows = await db.WorkflowStepLogs.AsNoTracking()
            .Where(s => s.WorkflowExecutionId == executionId)
            .OrderBy(s => s.StepNumber).ThenBy(s => s.Attempt).ThenBy(s => s.CreatedAt)
            .ToListAsync(ct);

        // The logged JSON is returned as JSON, not as an escaped string, so the viewer can render it.
        return rows.Select(s => new WorkflowStepResponse
        {
            Id = s.Id,
            StepNumber = s.StepNumber,
            Attempt = s.Attempt,
            AgentName = s.AgentName,
            ToolName = s.ToolName,
            Input = ParseJson(s.InputJson),
            Output = ParseJson(s.OutputJson),
            ValidationResult = s.ValidationResult,
            ValidationDetails = s.ValidationDetails,
            ErrorMessage = s.ErrorMessage,
            DurationMs = s.DurationMs,
            CreatedAt = s.CreatedAt,
        }).ToList();
    }

    private static JsonElement? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            // Not JSON after all: hand it back as a string rather than failing the whole view.
            return JsonSerializer.SerializeToElement(json);
        }
    }
}

/// <summary>Mirrors <c>workflow_executions</c> (web/src/api/approvals.ts WorkflowExecutionSummary).</summary>
public sealed class WorkflowExecutionSummaryResponse
{
    public required Guid Id { get; init; }
    public required Guid BookingRequestId { get; init; }
    public required string Objective { get; init; }
    public required WorkflowStatus Status { get; init; }
    public required int CurrentStep { get; init; }
    public int? TotalSteps { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}

public sealed class WorkflowExecutionDetailResponse
{
    public required WorkflowExecutionSummaryResponse Execution { get; init; }
    public JsonElement? Plan { get; init; }
    public required IReadOnlyList<WorkflowStepResponse> Steps { get; init; }
}

public sealed class WorkflowStepResponse
{
    public required Guid Id { get; init; }
    public required int StepNumber { get; init; }
    public required int Attempt { get; init; }
    public required string AgentName { get; init; }
    public string? ToolName { get; init; }
    public JsonElement? Input { get; init; }
    public JsonElement? Output { get; init; }
    public StepValidationResult? ValidationResult { get; init; }
    public string? ValidationDetails { get; init; }
    public string? ErrorMessage { get; init; }
    public int? DurationMs { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
