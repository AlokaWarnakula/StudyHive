namespace StudyHive.Api.Data.Entities;

public class WorkflowExecution
{
    public Guid Id { get; set; }
    public Guid BookingRequestId { get; set; }
    public required string Objective { get; set; }
    public string? PlanJson { get; set; }
    public int CurrentStep { get; set; }
    public int? TotalSteps { get; set; }
    public int Attempt { get; set; } = 1;
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Started;
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public BookingRequest BookingRequest { get; set; } = null!;
    public ICollection<WorkflowStepLog> StepLogs { get; set; } = new List<WorkflowStepLog>();
}
