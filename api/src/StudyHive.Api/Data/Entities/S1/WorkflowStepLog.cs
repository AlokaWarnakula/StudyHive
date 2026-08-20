namespace StudyHive.Api.Data.Entities;

public class WorkflowStepLog
{
    public Guid Id { get; set; }
    public Guid WorkflowExecutionId { get; set; }
    public int StepNumber { get; set; }
    public int Attempt { get; set; } = 1;
    public required string AgentName { get; set; }
    public string? ToolName { get; set; }
    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }
    public StepValidationResult? ValidationResult { get; set; }
    public string? ValidationDetails { get; set; }
    public int? DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public WorkflowExecution WorkflowExecution { get; set; } = null!;
}
