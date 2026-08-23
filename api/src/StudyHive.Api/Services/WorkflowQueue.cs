using System.Threading.Channels;

namespace StudyHive.Api.Services;

/// <summary>
/// No Redis, no Hangfire (DOCS §11 "How the background job works") — an in-process channel handed
/// off to <see cref="WorkflowBackgroundService"/>. The submit endpoint enqueues and returns
/// immediately; nothing about the HTTP request ever waits on the workflow run.
/// </summary>
public interface IWorkflowQueue
{
    ValueTask EnqueueAsync(Guid workflowExecutionId, CancellationToken ct = default);
    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct);
}

public sealed class WorkflowQueue : IWorkflowQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    public ValueTask EnqueueAsync(Guid workflowExecutionId, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(workflowExecutionId, ct);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}
