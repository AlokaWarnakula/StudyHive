using System.ComponentModel.DataAnnotations;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Controllers.BookingRequests;

public sealed class BookingRequestItemRequest
{
    public required Guid ConsumableId { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}

/// <summary>Shared field set for create and update — both are edits to a still-Draft request.</summary>
public abstract class BookingRequestFields : IValidatableObject
{
    [Required, MaxLength(2000)]
    public required string Objective { get; init; }

    [Range(1, 50)]
    public int GroupSize { get; init; }

    public DateOnly PreferredDateFrom { get; init; }
    public DateOnly PreferredDateTo { get; init; }
    public TimeOnly PreferredTimeFrom { get; init; }
    public TimeOnly PreferredTimeTo { get; init; }

    [Range(1, 7)]
    public int SessionsRequired { get; init; } = 1;

    [Range(30, 480)]
    public int SessionDurationMinutes { get; init; }

    // numeric(12,2) in Postgres — 10 integer digits max (S1Configurations.cs).
    [Range(typeof(decimal), "0.01", "9999999999.99")]
    public decimal Budget { get; init; }

    [MaxLength(2000)]
    public string? Notes { get; init; }

    public IReadOnlyList<BookingRequestItemRequest> Items { get; init; } = [];

    // Mirrors the DB-level chk_date_order / chk_time_order CHECK constraints (S1Configurations.cs)
    // so a bad range is a 400 with a field-level message instead of a 500 from the database.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PreferredDateTo < PreferredDateFrom)
        {
            yield return new ValidationResult(
                "Preferred end date must be on or after the start date.",
                [nameof(PreferredDateTo)]);
        }

        if (PreferredTimeTo <= PreferredTimeFrom)
        {
            yield return new ValidationResult(
                "Preferred end time must be after the start time.",
                [nameof(PreferredTimeTo)]);
        }
    }
}

public sealed class CreateBookingRequestRequest : BookingRequestFields;

public sealed class UpdateBookingRequestRequest : BookingRequestFields;

public sealed class BookingRequestItemResponse
{
    public required Guid ConsumableId { get; init; }
    public required int Quantity { get; init; }

    public static BookingRequestItemResponse From(BookingRequestItem item) => new()
    {
        ConsumableId = item.ConsumableId,
        Quantity = item.Quantity,
    };
}

public sealed class BookingRequestResponse
{
    public required Guid Id { get; init; }
    public required Guid StudentId { get; init; }
    public required string Objective { get; init; }
    public required int GroupSize { get; init; }
    public required DateOnly PreferredDateFrom { get; init; }
    public required DateOnly PreferredDateTo { get; init; }
    public required TimeOnly PreferredTimeFrom { get; init; }
    public required TimeOnly PreferredTimeTo { get; init; }
    public required int SessionsRequired { get; init; }
    public required int SessionDurationMinutes { get; init; }
    public required decimal Budget { get; init; }
    public required string? Notes { get; init; }
    public required BookingRequestStatus Status { get; init; }
    public required IReadOnlyList<BookingRequestItemResponse> Items { get; init; }
    public Guid? LatestWorkflowId { get; init; }
    /// <summary>The request's newest non-Draft quotation, or null before one is proposed. Lets a
    /// student reach GET /api/quotations/{id} for their own request (M-08).</summary>
    public BookingQuotationSummaryResponse? LatestQuotation { get; init; }
    /// <summary>The librarian's decision on <see cref="LatestQuotation"/> itself, or null while that
    /// quotation is undecided (an older version's decision is never carried over). Carries the
    /// outcome and comments only, never who decided.</summary>
    public BookingDecisionSummaryResponse? LatestDecision { get; init; }
    /// <summary>The rooms actually booked for this request (after approval), oldest slot first,
    /// with their lifecycle status and check-in time. Empty before approval (AUDIT C-06, C-14).</summary>
    public IReadOnlyList<BookingRoomBookingResponse> RoomBookings { get; init; } = [];
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }

    public static BookingRequestResponse From(
        BookingRequest request,
        Guid? latestWorkflowId = null,
        BookingQuotationSummaryResponse? latestQuotation = null,
        BookingDecisionSummaryResponse? latestDecision = null,
        IReadOnlyList<BookingRoomBookingResponse>? roomBookings = null) => new()
    {
        Id = request.Id,
        StudentId = request.StudentId,
        Objective = request.Objective,
        GroupSize = request.GroupSize,
        PreferredDateFrom = request.PreferredDateFrom,
        PreferredDateTo = request.PreferredDateTo,
        PreferredTimeFrom = request.PreferredTimeFrom,
        PreferredTimeTo = request.PreferredTimeTo,
        SessionsRequired = request.SessionsRequired,
        SessionDurationMinutes = request.SessionDurationMinutes,
        Budget = request.Budget,
        Notes = request.Notes,
        Status = request.Status,
        Items = request.Items.Select(BookingRequestItemResponse.From).ToList(),
        LatestWorkflowId = latestWorkflowId,
        LatestQuotation = latestQuotation,
        LatestDecision = latestDecision,
        RoomBookings = roomBookings ?? [],
        CreatedAt = request.CreatedAt,
        UpdatedAt = request.UpdatedAt,
    };
}

/// <summary>One room booking made for a request: where and when to go, and whether the student
/// checked in.</summary>
public sealed class BookingRoomBookingResponse
{
    public required Guid Id { get; init; }
    public required Guid RoomId { get; init; }
    public required string RoomName { get; init; }
    public required DateTimeOffset StartsAt { get; init; }
    public required DateTimeOffset EndsAt { get; init; }
    public required RoomBookingStatus Status { get; init; }
    public DateTimeOffset? CheckedInAt { get; init; }
}

/// <summary>The minimum a student needs to find and judge their quotation (S4).</summary>
public sealed class BookingQuotationSummaryResponse
{
    public required Guid Id { get; init; }
    public required QuotationStatus Status { get; init; }
    public required int Version { get; init; }
    public required decimal TotalAmount { get; init; }
    public required string Currency { get; init; }
    public required decimal BudgetSnapshot { get; init; }
    public required bool WithinBudget { get; init; }
    /// <summary>PLAN.md 3.1c: when the student paid at the library desk; null while unpaid.</summary>
    public DateTimeOffset? PaidAt { get; init; }
    public string? PaymentReference { get; init; }
}

/// <summary>Librarian records a desk payment; the receipt number is optional.</summary>
public sealed class RecordPaymentRequest
{
    [MaxLength(60)]
    public string? PaymentReference { get; init; }
}

/// <summary>A librarian's decision as the student sees it (S4): outcome, comments and when.</summary>
public sealed class BookingDecisionSummaryResponse
{
    public required ApprovalDecisionType Decision { get; init; }
    public string? Comments { get; init; }
    public required DateTimeOffset DecidedAt { get; init; }
}

public sealed class SubmitBookingRequestResponse
{
    public required Guid WorkflowId { get; init; }
}

public sealed class WorkflowStepLogResponse
{
    public required int StepNumber { get; init; }
    public required string AgentName { get; init; }
    public required string? ToolName { get; init; }
    public required StepValidationResult? ValidationResult { get; init; }
    public required string? ErrorMessage { get; init; }
    public required int? DurationMs { get; init; }
    public required string? OutputJson { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }

    public static WorkflowStepLogResponse From(WorkflowStepLog log) => new()
    {
        StepNumber = log.StepNumber,
        AgentName = log.AgentName,
        ToolName = log.ToolName,
        ValidationResult = log.ValidationResult,
        ErrorMessage = log.ErrorMessage,
        DurationMs = log.DurationMs,
        OutputJson = log.OutputJson,
        CreatedAt = log.CreatedAt,
    };
}

public sealed class WorkflowStatusResponse
{
    public required Guid WorkflowId { get; init; }
    public required Guid BookingRequestId { get; init; }
    public required WorkflowStatus Status { get; init; }
    public required int CurrentStep { get; init; }
    public required int? TotalSteps { get; init; }
    public required string? ErrorCode { get; init; }
    public required string? ErrorMessage { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset? CompletedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public required IReadOnlyList<WorkflowStepLogResponse> Steps { get; init; }

    public static WorkflowStatusResponse From(WorkflowExecution execution) => new()
    {
        WorkflowId = execution.Id,
        BookingRequestId = execution.BookingRequestId,
        Status = execution.Status,
        CurrentStep = execution.CurrentStep,
        TotalSteps = execution.TotalSteps,
        ErrorCode = execution.ErrorCode,
        ErrorMessage = execution.ErrorMessage,
        StartedAt = execution.StartedAt,
        CompletedAt = execution.CompletedAt,
        UpdatedAt = execution.UpdatedAt,
        Steps = execution.StepLogs.OrderBy(s => s.StepNumber).Select(WorkflowStepLogResponse.From).ToList(),
    };
}
