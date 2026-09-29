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
/// Runs one booking request's agentic workflow end to end: the Planner Agent (S1), the Scheduling
/// Agent (S2), the Resource Agent (S3 — availability/pricing, plus one Pending stock_reservations row
/// per line so the librarian's approval screen can see what would be reserved), then the Validation
/// Agent (S4), whose quotation is persisted as a Proposed <see cref="Quotation"/> with its line items
/// as the request moves to PendingApproval. Every failure path (ineligible, an agent unreachable,
/// validation failed, workflow timeout) ends in a terminal Failed/Rejected status with an error code —
/// never a half-updated request.
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
    ISchedulingAgentClient schedulingAgentClient,
    IResourceClient resourceClient,
    IValidationClient validationClient,
    IConsumableStockService stockService,
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
            .Include(w => w.BookingRequest).ThenInclude(b => b.RequiredEquipment)
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

            // Step 2 is S2's real Scheduling Agent. StudyHive.Api supplies the trusted room,
            // equipment, booking and maintenance snapshot; the agent never reads the database.
            var schedulingRequest = await BuildSchedulingRequestAsync(bookingRequest, ct);
            var (schedulingOutput, schedulingDurationMs, schedulingError) =
                await CallSchedulingWithRetriesAsync(schedulingRequest, limits, ct);

            var schedulingSucceeded = schedulingOutput is not null &&
                schedulingOutput.Slots.Count >= bookingRequest.SessionsRequired;
            var noAvailabilityError = schedulingOutput is not null && !schedulingSucceeded
                ? string.Join(" ", schedulingOutput.Conflicts)
                : null;

            await LogStepAsync(execution.Id, 2, "Scheduling", "propose_slots",
                input: schedulingRequest,
                output: schedulingOutput is null ? new { error = schedulingError } : schedulingOutput,
                validationResult: schedulingSucceeded ? StepValidationResult.Pass : StepValidationResult.Fail,
                errorMessage: schedulingError ?? noAvailabilityError,
                durationMs: schedulingDurationMs,
                ct);

            if (schedulingOutput is null)
            {
                await FailAsync(execution, bookingRequest, "STEP_RETRY_EXHAUSTED",
                    schedulingError ?? "Scheduling Agent did not respond after retries.", ct);
                return;
            }

            if (!schedulingSucceeded)
            {
                await FailAsync(execution, bookingRequest, "NO_ROOM_AVAILABLE",
                    string.IsNullOrWhiteSpace(noAvailabilityError)
                        ? "The Scheduling Agent could not find all requested sessions."
                        : noAvailabilityError,
                    ct);
                return;
            }

            // Step 3: the real Resource Agent (S3). The agent has no database access, so — exactly
            // how `plannerRequest` above carries eligibility already computed — this API reads each
            // requested consumable's current availability/price itself and hands both down on the
            // wire. A consumable that no longer exists (or was deactivated) falls back to
            // Available=0, which correctly makes that line `sufficient: false` rather than throwing.
            var consumableIds = bookingRequest.Items.Select(i => i.ConsumableId).Distinct().ToList();
            var consumablesById = await db.Consumables.AsNoTracking()
                .Where(c => consumableIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, ct);

            var resourceRequest = new ResourceRequest
            {
                RequestedItems = bookingRequest.Items.Select(i =>
                {
                    consumablesById.TryGetValue(i.ConsumableId, out var consumable);
                    return new ResourceRequestItem
                    {
                        ConsumableId = i.ConsumableId,
                        Name = consumable?.Name ?? "Unknown consumable",
                        Requested = i.Quantity,
                        Available = consumable?.AvailableQuantity ?? 0,
                        UnitPrice = consumable?.UnitPrice ?? 0m,
                    };
                }).ToList(),
            };

            var (resourceResponse, resourceDurationMs, resourceError) = await CallResourceWithRetriesAsync(resourceRequest, limits, ct);

            await LogStepAsync(
                execution.Id, stepNumber: 3, agentName: "Resource", toolName: "prepare_reservation",
                input: resourceRequest,
                output: resourceResponse is null ? new { error = resourceError } : resourceResponse,
                validationResult: resourceResponse is not null ? StepValidationResult.Pass : StepValidationResult.Fail,
                errorMessage: resourceError, durationMs: resourceDurationMs, ct);

            if (resourceResponse is null)
            {
                await FailAsync(execution, bookingRequest, "STEP_RETRY_EXHAUSTED", resourceError ?? "Resource agent did not respond after retries.", ct);
                return;
            }

            // DOCS §11: the Resource agent "creates Pending reservation records but does not
            // actually reserve stock" — one per booking-request line, regardless of sufficiency, so
            // the librarian's approval screen shows exactly what would be reserved (and what's
            // short) before they decide. Real reservation (the guarded, no-oversell transition to
            // Reserved) happens later, at approval time, via IConsumableStockService.ReserveAsync.
            foreach (var item in bookingRequest.Items)
            {
                var pendingResult = await stockService.CreatePendingReservationAsync(item.Id, ct);
                if (!pendingResult.Succeeded && pendingResult.Outcome != StockOperationOutcome.AlreadyReserved)
                {
                    await FailAsync(execution, bookingRequest, "RESOURCE_PENDING_RESERVATION_FAILED",
                        pendingResult.Detail ?? "Could not create a pending stock reservation.", ct);
                    return;
                }
            }

            // Step 4: the real Validation Agent (S4) — the last deterministic gate before a librarian
            // sees the proposal. It re-checks the whole proposal from the same trusted snapshots
            // steps 2 and 3 used and prices it; the agent has no database access.
            var validationRequest = BuildValidationRequest(bookingRequest, schedulingRequest, schedulingOutput, resourceRequest);
            var (validationResponse, validationDurationMs, validationError) =
                await CallValidationWithRetriesAsync(validationRequest, limits, ct);

            await LogStepAsync(
                execution.Id, stepNumber: 4, agentName: "Validation", toolName: "calculate_quotation",
                input: validationRequest,
                output: validationResponse is null ? new { error = validationError } : validationResponse,
                validationResult: validationResponse is { Valid: true } ? StepValidationResult.Pass : StepValidationResult.Fail,
                errorMessage: validationError ?? (validationResponse is { Valid: false }
                    ? string.Join(" ", validationResponse.Failures)
                    : null),
                durationMs: validationDurationMs, ct);

            if (validationResponse is null)
            {
                await FailAsync(execution, bookingRequest, "STEP_RETRY_EXHAUSTED",
                    validationError ?? "Validation agent did not respond after retries.", ct);
                return;
            }

            // PLAN.md Day 2 decision: a proposal that fails a hard rule never reaches the librarian.
            // No quotation is written (so nothing invalid is ever approvable), the Pending stock
            // notes are released, and the agent's revision note is what the student sees.
            if (!validationResponse.Valid)
            {
                await ReleasePendingReservationsAsync(bookingRequest, ct);
                await FailAsync(execution, bookingRequest, "VALIDATION_FAILED",
                    validationResponse.RevisionNote ?? string.Join(" ", validationResponse.Failures), ct);
                return;
            }

            // Added to the change tracker here and saved by the SaveChangesAsync below, together with
            // the status change — one implicit transaction, so PendingApproval never exists without
            // its quotation.
            await AddProposedQuotationAsync(bookingRequest, validationResponse.Quotation, ct);

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

    private async Task<SchedulingRequest> BuildSchedulingRequestAsync(
        BookingRequest bookingRequest,
        CancellationToken ct)
    {
        var colomboOffset = TimeSpan.FromMinutes(330);
        var rangeStart = new DateTimeOffset(
            bookingRequest.PreferredDateFrom.ToDateTime(TimeOnly.MinValue),
            colomboOffset).ToUniversalTime();
        var rangeEnd = new DateTimeOffset(
            bookingRequest.PreferredDateTo.ToDateTime(TimeOnly.MaxValue),
            colomboOffset).ToUniversalTime();

        var rooms = await db.StudyRooms
            .AsNoTracking()
            .AsSplitQuery()
            .Include(r => r.Equipment.Where(e => e.Quantity > 0))
            .Include(r => r.Bookings.Where(b =>
                b.Status == RoomBookingStatus.Confirmed &&
                b.StartsAt < rangeEnd &&
                b.EndsAt > rangeStart))
            .Include(r => r.MaintenanceWindows.Where(w =>
                w.StartsAt < rangeEnd &&
                w.EndsAt > rangeStart))
            .ToListAsync(ct);

        return new SchedulingRequest
        {
            GroupSize = bookingRequest.GroupSize,
            PreferredDateFrom = bookingRequest.PreferredDateFrom,
            PreferredDateTo = bookingRequest.PreferredDateTo,
            PreferredTimeFrom = bookingRequest.PreferredTimeFrom,
            PreferredTimeTo = bookingRequest.PreferredTimeTo,
            SessionsRequired = bookingRequest.SessionsRequired,
            SessionDurationMinutes = bookingRequest.SessionDurationMinutes,
            RequiredEquipmentTypeIds = bookingRequest.RequiredEquipment
                .Select(e => e.EquipmentTypeId)
                .Distinct()
                .ToList(),
            Rooms = rooms.Select(room => new SchedulingRoom
            {
                RoomId = room.Id,
                RoomName = room.Name,
                Capacity = room.Capacity,
                HourlyRate = room.HourlyRate,
                IsActive = room.IsActive,
                EquipmentTypeIds = room.Equipment
                    .Select(e => e.EquipmentTypeId)
                    .Distinct()
                    .ToList(),
                Bookings = room.Bookings.Select(b => new SchedulingTimeBlock
                {
                    StartsAt = b.StartsAt,
                    EndsAt = b.EndsAt,
                }).ToList(),
                MaintenanceWindows = room.MaintenanceWindows.Select(w => new SchedulingTimeBlock
                {
                    StartsAt = w.StartsAt,
                    EndsAt = w.EndsAt,
                }).ToList(),
            }).ToList(),
        };
    }

    private async Task<(SchedulingResponse? Response, int DurationMs, string? Error)>
        CallSchedulingWithRetriesAsync(
            SchedulingRequest request,
            WorkflowLimitsOptions limits,
            CancellationToken ct)
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
                var response = await schedulingAgentClient.ProposeAsync(request, attemptCts.Token);
                var validationErrors = ValidateSchedulingResponse(request, response);
                if (validationErrors.Count == 0)
                {
                    stopwatch.Stop();
                    return (response, (int)stopwatch.ElapsedMilliseconds, null);
                }

                lastError = $"Scheduling validation failed: {string.Join(" ", validationErrors)} " +
                    $"(attempt {attempt}/{maxAttempts}).";
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                lastError = $"Scheduling call timed out after {limits.ToolCallTimeoutSeconds}s " +
                    $"(attempt {attempt}/{maxAttempts}).";
            }
            catch (HttpRequestException ex)
            {
                lastError = $"Scheduling call failed: {ex.Message} (attempt {attempt}/{maxAttempts}).";
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                lastError = $"Scheduling Agent returned an invalid response: {ex.Message} " +
                    $"(attempt {attempt}/{maxAttempts}).";
            }
        }

        stopwatch.Stop();
        return (null, (int)stopwatch.ElapsedMilliseconds, lastError);
    }

    private static IReadOnlyList<string> ValidateSchedulingResponse(
        SchedulingRequest request,
        SchedulingResponse response)
    {
        var errors = new List<string>();
        var requiredEquipment = request.RequiredEquipmentTypeIds.ToHashSet();
        var duration = TimeSpan.FromMinutes(request.SessionDurationMinutes);
        var colomboOffset = TimeSpan.FromMinutes(330);

        foreach (var slot in response.Slots)
        {
            var room = request.Rooms.SingleOrDefault(r => r.RoomId == slot.RoomId);
            if (room is null)
            {
                errors.Add($"Unknown room {slot.RoomId}.");
                continue;
            }

            if (!room.IsActive || room.Capacity < request.GroupSize ||
                !requiredEquipment.IsSubsetOf(room.EquipmentTypeIds.ToHashSet()))
            {
                errors.Add($"Room {slot.RoomId} does not satisfy the request.");
            }

            if (slot.EndsAt - slot.StartsAt != duration)
            {
                errors.Add($"Room {slot.RoomId} has an invalid slot duration.");
            }

            var localStart = slot.StartsAt.ToOffset(colomboOffset);
            var localEnd = slot.EndsAt.ToOffset(colomboOffset);
            if (DateOnly.FromDateTime(localStart.DateTime) < request.PreferredDateFrom ||
                DateOnly.FromDateTime(localEnd.DateTime) > request.PreferredDateTo ||
                TimeOnly.FromDateTime(localStart.DateTime) < request.PreferredTimeFrom ||
                TimeOnly.FromDateTime(localEnd.DateTime) > request.PreferredTimeTo)
            {
                errors.Add($"Room {slot.RoomId} has a slot outside the preferred window.");
            }

            if (room.Bookings.Any(b => b.StartsAt < slot.EndsAt && b.EndsAt > slot.StartsAt) ||
                room.MaintenanceWindows.Any(w => w.StartsAt < slot.EndsAt && w.EndsAt > slot.StartsAt))
            {
                errors.Add($"Room {slot.RoomId} has a conflicting slot.");
            }
        }

        for (var i = 0; i < response.Slots.Count; i++)
        {
            for (var j = i + 1; j < response.Slots.Count; j++)
            {
                if (response.Slots[i].StartsAt < response.Slots[j].EndsAt &&
                    response.Slots[i].EndsAt > response.Slots[j].StartsAt)
                {
                    errors.Add("Proposed sessions overlap each other.");
                }
            }
        }

        return errors;
    }

    private async Task<(ResourceResponse? Response, int DurationMs, string? Error)> CallResourceWithRetriesAsync(
        ResourceRequest request, WorkflowLimitsOptions limits, CancellationToken ct)
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
                var response = await resourceClient.PrepareReservationAsync(request, attemptCts.Token);
                stopwatch.Stop();
                return (response, (int)stopwatch.ElapsedMilliseconds, null);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                lastError = $"Resource call timed out after {limits.ToolCallTimeoutSeconds}s (attempt {attempt}/{maxAttempts}).";
            }
            catch (HttpRequestException ex)
            {
                lastError = $"Resource call failed: {ex.Message} (attempt {attempt}/{maxAttempts}).";
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                lastError = $"Resource returned an invalid response: {ex.Message} (attempt {attempt}/{maxAttempts}).";
            }
        }

        stopwatch.Stop();
        return (null, (int)stopwatch.ElapsedMilliseconds, lastError);
    }

    private static ValidationRequest BuildValidationRequest(
        BookingRequest bookingRequest,
        SchedulingRequest schedulingRequest,
        SchedulingResponse schedulingResponse,
        ResourceRequest resourceRequest)
    {
        var slotRoomIds = schedulingResponse.Slots.Select(s => s.RoomId).ToHashSet();
        return new ValidationRequest
        {
            Objective = bookingRequest.Objective,
            GroupSize = bookingRequest.GroupSize,
            Budget = bookingRequest.Budget,
            SessionsRequired = bookingRequest.SessionsRequired,
            SessionDurationMinutes = bookingRequest.SessionDurationMinutes,
            ProposedSlots = schedulingResponse.Slots,
            Rooms = schedulingRequest.Rooms.Where(r => slotRoomIds.Contains(r.RoomId)).ToList(),
            Items = resourceRequest.RequestedItems,
        };
    }

    private async Task<(ValidationResponse? Response, int DurationMs, string? Error)> CallValidationWithRetriesAsync(
        ValidationRequest request, WorkflowLimitsOptions limits, CancellationToken ct)
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
                var response = await validationClient.ValidateAsync(request, attemptCts.Token);
                var contractErrors = ValidateValidationResponse(request, response);
                if (contractErrors.Count == 0)
                {
                    stopwatch.Stop();
                    return (response, (int)stopwatch.ElapsedMilliseconds, null);
                }

                lastError = $"Validation response failed checks: {string.Join(" ", contractErrors)} " +
                    $"(attempt {attempt}/{maxAttempts}).";
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                lastError = $"Validation call timed out after {limits.ToolCallTimeoutSeconds}s (attempt {attempt}/{maxAttempts}).";
            }
            catch (HttpRequestException ex)
            {
                lastError = $"Validation call failed: {ex.Message} (attempt {attempt}/{maxAttempts}).";
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                lastError = $"Validation returned an invalid response: {ex.Message} (attempt {attempt}/{maxAttempts}).";
            }
        }

        stopwatch.Stop();
        return (null, (int)stopwatch.ElapsedMilliseconds, lastError);
    }

    /// <summary>
    /// The API's own check that the agent's answer is something it may persist: every line prices a
    /// room or consumable this request actually proposed, each line total is quantity x unit price
    /// to the cent (the same arithmetic as the generated <c>quotation_line_items.line_total</c>), and
    /// the fees and total add up. <c>valid=true</c> must also agree with the rule results. A response
    /// that fails is retried like a transport error.
    /// </summary>
    private static IReadOnlyList<string> ValidateValidationResponse(ValidationRequest request, ValidationResponse response)
    {
        var errors = new List<string>();
        var roomIds = request.ProposedSlots.Select(s => s.RoomId).ToHashSet();
        var consumableIds = request.Items.Select(i => i.ConsumableId).ToHashSet();
        var quotation = response.Quotation;

        if (response.Valid && response.Results.Any(r => !r.Passed))
        {
            errors.Add("valid=true contradicts a failed rule.");
        }

        foreach (var line in quotation.LineItems)
        {
            var shapeOk = line.ItemType switch
            {
                nameof(QuotationLineItemType.Room) =>
                    line.RoomId is { } roomId && roomIds.Contains(roomId) && line.ConsumableId is null,
                nameof(QuotationLineItemType.Consumable) =>
                    line.ConsumableId is { } consumableId && consumableIds.Contains(consumableId) && line.RoomId is null,
                _ => false,
            };
            if (!shapeOk)
            {
                errors.Add($"Line '{line.ItemName}' does not price a proposed room or requested item.");
            }

            if (line.Quantity <= 0 || line.UnitPrice < 0 ||
                Math.Round(line.Quantity * line.UnitPrice, 2, MidpointRounding.AwayFromZero) != line.LineTotal)
            {
                errors.Add($"Line '{line.ItemName}' total is not quantity x unit price.");
            }
        }

        var roomFee = quotation.LineItems.Where(l => l.ItemType == nameof(QuotationLineItemType.Room)).Sum(l => l.LineTotal);
        var consumableCost = quotation.LineItems.Where(l => l.ItemType == nameof(QuotationLineItemType.Consumable)).Sum(l => l.LineTotal);
        if (roomFee != quotation.RoomFee || consumableCost != quotation.ConsumableCost ||
            quotation.RoomFee + quotation.ConsumableCost != quotation.Total)
        {
            errors.Add("Quotation fees and total do not match its line items.");
        }

        return errors;
    }

    private async Task AddProposedQuotationAsync(BookingRequest bookingRequest, ValidationQuotation quotation, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var latestVersion = await db.Quotations
            .Where(q => q.BookingRequestId == bookingRequest.Id)
            .MaxAsync(q => (int?)q.Version, ct) ?? 0;

        db.Quotations.Add(new Quotation
        {
            Id = Guid.NewGuid(),
            BookingRequestId = bookingRequest.Id,
            Version = latestVersion + 1,
            RoomFee = quotation.RoomFee,
            ConsumableCost = quotation.ConsumableCost,
            BudgetSnapshot = bookingRequest.Budget,
            Status = QuotationStatus.Proposed,
            CreatedAt = now,
            UpdatedAt = now,
            LineItems = quotation.LineItems.Select(line => new QuotationLineItem
            {
                Id = Guid.NewGuid(),
                ItemType = Enum.Parse<QuotationLineItemType>(line.ItemType),
                RoomId = line.RoomId,
                ConsumableId = line.ConsumableId,
                ItemName = line.ItemName.Length > 150 ? line.ItemName[..150] : line.ItemName,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                CreatedAt = now,
            }).ToList(),
        });
    }

    /// <summary>Pending reservations never held stock (see CreatePendingReservationAsync), so
    /// releasing them is a status change only — no reserved_quantity or ledger movement. Saved by
    /// the FailAsync that follows, in the same SaveChangesAsync as the Failed status.</summary>
    private async Task ReleasePendingReservationsAsync(BookingRequest bookingRequest, CancellationToken ct)
    {
        var itemIds = bookingRequest.Items.Select(i => i.Id).ToList();
        var pending = await db.StockReservations
            .Where(r => itemIds.Contains(r.BookingRequestItemId) && r.Status == StockReservationStatus.Pending)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        foreach (var reservation in pending)
        {
            reservation.Status = StockReservationStatus.Released;
            reservation.ReleasedAt = now;
            reservation.UpdatedAt = now;
        }
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
}
