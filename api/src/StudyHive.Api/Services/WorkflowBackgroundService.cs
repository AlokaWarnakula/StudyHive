namespace StudyHive.Api.Services;

/// <summary>Drains <see cref="IWorkflowQueue"/> and runs each workflow in its own DI scope (the queued
/// item outlives the HTTP request that enqueued it, so it can never reuse that request's scoped DbContext).</summary>
public sealed class WorkflowBackgroundService(
    IWorkflowQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<WorkflowBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var workflowExecutionId in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<IWorkflowOrchestrationService>();
                await orchestrator.RunAsync(workflowExecutionId, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The orchestrator itself catches and records business/timeout failures as a Failed
                // workflow row — reaching here means something unexpected escaped that, so log it
                // rather than let it kill the whole background service loop.
                logger.LogError(ex, "Unhandled error running workflow {WorkflowExecutionId}", workflowExecutionId);
            }
        }
    }
}
