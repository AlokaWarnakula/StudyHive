namespace StudyHive.Api.Common;

/// <summary>Agentic workflow timeout/retry limits — see DOCS Master Plan sec. 11. Bound from the "WorkflowLimits" config section.</summary>
public sealed class WorkflowLimitsOptions
{
    public const string SectionName = "WorkflowLimits";

    public int ToolCallTimeoutSeconds { get; init; } = 15;
    public int SingleAgentTimeoutSeconds { get; init; } = 45;
    public int WholeWorkflowTimeoutSeconds { get; init; } = 180;
    public int MaxRetriesPerStep { get; init; } = 2;
    public int MaxLlmTokensPerRun { get; init; } = 8000;
}
