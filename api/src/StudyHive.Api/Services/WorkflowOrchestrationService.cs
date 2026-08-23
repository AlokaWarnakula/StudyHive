using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudyHive.Api.Common;
using StudyHive.Api.Contracts;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Services;

/// <summary>
/// Runs one booking request's agentic workflow end to end: calls the Planner Agent, then persists
/// contract-shaped Scheduling/Resource/Validation stub steps (DOCS §04: "Use contract-correct fake
/// Scheduling/Resource/Validation outputs until later owners replace them") and moves the request to
/// PendingApproval. Every failure path (ineligible, planner unreachable, workflow timeout) ends in a
/// terminal Failed/Rejected status with an error code — never a half-updated request.
/// </summary>
public interface IWorkflowOrchestrationService
{
    Task<Guid> StartAsync(Guid bookingRequestId, CancellationToken ct);
    Task RunAsync(Guid workflowExecutionId, CancellationToken ct);
}

public sealed class WorkflowOrchestrationService(
    StudyHiveDbContext db,
    IBookingEligibilityService eligibilityService,
    IPlannerClient plannerClient,
    IOptions<WorkflowLimitsOptions> limitsOptions) : IWorkflowOrchestrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<Guid> StartAsync(Guid bookingRequestId, CancellationToken ct)
    {
        var bookingRequest = await db.BookingRequests.SingleAsync(b => b.Id == bookingRequestId, ct);

        var execution = new WorkflowExecution
        {
            Id = Guid.NewGuid(),
            BookingRequestId = bookingRequestId,
            Objective = bookingRequest.Objective,
            Status = WorkflowStatus.Started,
            StartedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.WorkflowExecutions.Add(execution);

        bookingRequest.Status = BookingRequestStatus.Submitted;
        bookingRequest.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return execution.Id;
    }

    public async Task RunAsync(Guid workflowExecutionId, CancellationToken outerCt)
    {
        var limits = limitsOptions.Value;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(limits.WholeWorkflowTimeoutSeconds));
        var ct = timeoutCts.Token;

        var execution = await db.WorkflowExecutions
            .Include(w => w.BookingRequest).ThenInclude(b => b.Items)
            .SingleOrDefaultAsync(w => w.Id == workflowExecutionId, ct);
        if (execution is null) return;

        var bookingRequest = execution.BookingRequest;

        execution.Status = WorkflowStatus.InProgress;
        execution.UpdatedAt = DateTimeOffset.UtcNow;
        bookingRequest.Status = BookingRequestStatus.Processing;
        bookingRequest.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        try
        {
            var eligibility = await eligibilityService.EvaluateAsync(bookingRequest.StudentId, ct);

            var plannerRequest = new PlannerRequest
            {
                Objective = bookingRequest.Objective,
                StudentId = bookingRequest.StudentId,
                GroupSize = bookingRequest.GroupSize,
                PreferredDateFrom = bookingRequest.PreferredDateFrom,
                PreferredDateTo = bookingRequest.PreferredDateTo,
                PreferredTimeFrom = bookingRequest.PreferredTimeFrom,
                PreferredTimeTo = bookingRequest.PreferredTimeTo,
                SessionsRequired = bookingRequest.SessionsRequired,
                SessionDurationMinutes = bookingRequest.SessionDurationMinutes,
                Budget = bookingRequest.Budget,
                StudentEligible = eligibility.IsEligible,
                EligibilityReasons = eligibility.Reasons,
                RequestedItems = bookingRequest.Items
                    .Select(i => new PlannerRequestItem { ConsumableId = i.ConsumableId, Quantity = i.Quantity })
                    .ToList(),
            };

            var (plannerResponse, stepDurationMs, stepError) = await CallPlannerWithRetriesAsync(plannerRequest, limits, ct);

            await LogStepAsync(
                execution.Id, stepNumber: 1, agentName: "Planner", toolName: "create_plan",
                input: plannerRequest,
                output: plannerResponse is null ? new { error = stepError } : plannerResponse,
                validationResult: plannerResponse is not null ? StepValidationResult.Pass : StepValidationResult.Fail,
                errorMessage: stepError, durationMs: stepDurationMs, ct);

            if (plannerResponse is null)
            {
                await FailAsync(execution, bookingRequest, "STEP_RETRY_EXHAUSTED", stepError ?? "Planner did not respond after retries.", ct);
                return;
            }

            execution.PlanJson = JsonSerializer.Serialize(plannerResponse, JsonOptions);

            if (!plannerResponse.Eligible)
            {
                execution.Status = WorkflowStatus.Rejected;
                execution.ErrorCode = "INELIGIBLE";
                execution.ErrorMessage = plannerResponse.Reasons.Count > 0
                    ? string.Join(" ", plannerResponse.Reasons)
                    : "Planner determined the student is not eligible.";
                execution.CompletedAt = DateTimeOffset.UtcNow;
                execution.UpdatedAt = DateTimeOffset.UtcNow;
                execution.CurrentStep = 1;
                execution.TotalSteps = 1;

                bookingRequest.Status = BookingRequestStatus.Rejected;
                bookingRequest.UpdatedAt = DateTimeOffset.UtcNow;

                await db.SaveChangesAsync(ct);
                return;
            }

            // Steps 2-4: contract-shaped Scheduling/Resource/Validation stubs — S2/S3/S4 replace
            // these in later relay handoffs (DOCS §04). Deterministic from the request's own data,
            // and explicitly flagged "stub": true so nobody mistakes it for a real proposal.
            var schedulingOutput = BuildSchedulingStub(bookingRequest);
            await LogStepAsync(execution.Id, 2, "Scheduling", "propose_slots",
                input: new { bookingRequest.GroupSize, bookingRequest.PreferredDateFrom, bookingRequest.PreferredDateTo },
                output: schedulingOutput, StepValidationResult.Pass, null, durationMs: 0, ct);

            var resourceOutput = BuildResourceStub(bookingRequest);
            await LogStepAsync(execution.Id, 3, "Resource", "prepare_reservation",
                input: new { items = plannerRequest.RequestedItems },
                output: resourceOutput, StepValidationResult.Pass, null, durationMs: 0, ct);

            var validationOutput = BuildValidationStub(bookingRequest);
            await LogStepAsync(execution.Id, 4, "Validation", "calculate_quotation",
                input: new { }, output: validationOutput, StepValidationResult.Pass, null, durationMs: 0, ct);

            execution.Status = WorkflowStatus.PendingApproval;
            execution.CurrentStep = 4;
            execution.TotalSteps = 4;
            execution.UpdatedAt = DateTimeOffset.UtcNow;

            bookingRequest.Status = BookingRequestStatus.PendingApproval;
            bookingRequest.UpdatedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (!outerCt.IsCancellationRequested)
        {
            await FailAsync(execution, bookingRequest, "WORKFLOW_TIMEOUT",
                $"Workflow exceeded the {limits.WholeWorkflowTimeoutSeconds}s limit.", CancellationToken.None);
        }
    }

    private async Task<(PlannerResponse? Response, int DurationMs, string? Error)> CallPlannerWithRetriesAsync(
        PlannerRequest request, WorkflowLimitsOptions limits, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        string? lastError = null;
        var maxAttempts = limits.MaxRetriesPerStep + 1;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            attemptCts.CancelAfter(TimeSpan.FromSeconds(limits.ToolCallTimeoutSeconds));
            try
            {
                var response = await plannerClient.PlanAsync(request, attemptCts.Token);
                stopwatch.Stop();
                return (response, (int)stopwatch.ElapsedMilliseconds, null);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                lastError = $"Planner call timed out after {limits.ToolCallTimeoutSeconds}s (attempt {attempt}/{maxAttempts}).";
            }
            catch (HttpRequestException ex)
            {
                lastError = $"Planner call failed: {ex.Message} (attempt {attempt}/{maxAttempts}).";
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                lastError = $"Planner returned an invalid response: {ex.Message} (attempt {attempt}/{maxAttempts}).";
            }
        }

        stopwatch.Stop();
        return (null, (int)stopwatch.ElapsedMilliseconds, lastError);
    }

    private async Task FailAsync(WorkflowExecution execution, BookingRequest bookingRequest, string errorCode, string errorMessage, CancellationToken ct)
    {
        execution.Status = WorkflowStatus.Failed;
        execution.ErrorCode = errorCode;
        execution.ErrorMessage = errorMessage;
        execution.CompletedAt = DateTimeOffset.UtcNow;
        execution.UpdatedAt = DateTimeOffset.UtcNow;

        bookingRequest.Status = BookingRequestStatus.Failed;
        bookingRequest.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    private async Task LogStepAsync(
        Guid workflowExecutionId, int stepNumber, string agentName, string toolName,
        object? input, object? output, StepValidationResult validationResult, string? errorMessage, int durationMs,
        CancellationToken ct)
    {
        db.WorkflowStepLogs.Add(new WorkflowStepLog
        {
            Id = Guid.NewGuid(),
            WorkflowExecutionId = workflowExecutionId,
            StepNumber = stepNumber,
            AgentName = agentName,
            ToolName = toolName,
            InputJson = input is null ? null : JsonSerializer.Serialize(input, JsonOptions),
            OutputJson = output is null ? null : JsonSerializer.Serialize(output, JsonOptions),
            ValidationResult = validationResult,
            ErrorMessage = errorMessage,
            DurationMs = durationMs,
        });
        await db.SaveChangesAsync(ct);
    }

    private static object BuildSchedulingStub(BookingRequest br)
    {
        var startsAt = br.PreferredDateFrom.ToDateTime(br.PreferredTimeFrom, DateTimeKind.Utc);
        var endsAt = startsAt.AddMinutes(br.SessionDurationMinutes);
        const decimal placeholderHourlyRate = 10m; // stub only — replaced by the real Scheduling agent (S2)

        return new
        {
            stub = true,
            slots = new[]
            {
                new
                {
                    roomId = (Guid?)null,
                    roomName = "TBD — pending Rooms & Availability (S2)",
                    startsAt,
                    endsAt,
                    hourlyRate = placeholderHourlyRate,
                },
            },
            conflicts = Array.Empty<object>(),
        };
    }

    private static object BuildResourceStub(BookingRequest br)
    {
        const decimal placeholderUnitPrice = 1m; // stub only — replaced by the real Resource agent (S3)
        var items = br.Items.Select(i => new
        {
            consumableId = i.ConsumableId,
            requested = i.Quantity,
            available = i.Quantity,
            sufficient = true,
            unitPrice = placeholderUnitPrice,
            lineTotal = placeholderUnitPrice * i.Quantity,
        }).ToArray();

        return new
        {
            stub = true,
            items,
            totalCost = items.Sum(i => i.lineTotal),
            allAvailable = true,
        };
    }

    private static object BuildValidationStub(BookingRequest br)
    {
        // Deliberately naive placeholder arithmetic — S4 (Costing & Approval) replaces this with the
        // real deterministic Validation agent. It exists only so PendingApproval carries a plausible,
        // contract-shaped quotation for a librarian to look at.
        var hours = br.SessionDurationMinutes / 60m;
        var roomFee = hours * 10m * br.SessionsRequired;
        var consumableCost = br.Items.Sum(i => i.Quantity * 1m);
        var total = roomFee + consumableCost;

        return new
        {
            stub = true,
            valid = true,
            results = new[]
            {
                new { rule = "validate_capacity", passed = true, detail = "Stub: capacity check deferred to S2." },
                new { rule = "validate_stock", passed = true, detail = "Stub: stock check deferred to S3." },
                new { rule = "validate_budget", passed = total <= br.Budget, detail = $"Stub estimate {total:0.00} vs budget {br.Budget:0.00}." },
            },
            quotation = new
            {
                roomFee,
                consumableCost,
                total,
                lineItems = Array.Empty<object>(),
            },
            failures = Array.Empty<object>(),
        };
    }
}
