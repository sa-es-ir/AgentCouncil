using System.Collections.Concurrent;
using AgentCouncil.Agents.Workflow;
using Microsoft.Agents.AI.Workflows;

namespace AgentCouncil.Agents.Hubs;

/// <summary>
/// Holds one streaming workflow run per SignalR connection. The run preserves the full council
/// conversation across user turns, so a session is opened once (on the first topic) and reused
/// for every subsequent user reply until the connection drops.
/// </summary>
public sealed class CouncilSessionManager(CouncilWorkflow council)
{
    private readonly ConcurrentDictionary<string, StreamingRun> _runs = new();

    /// <summary>Opens a fresh run for the connection, replacing any existing one.</summary>
    public async Task<StreamingRun> StartAsync(string connectionId, CancellationToken cancellationToken)
    {
        await EndAsync(connectionId);

        StreamingRun run = await InProcessExecution.OpenStreamingAsync(council.Workflow, cancellationToken: cancellationToken);
        _runs[connectionId] = run;
        return run;
    }

    public bool TryGet(string connectionId, out StreamingRun run) => _runs.TryGetValue(connectionId, out run!);

    public async Task EndAsync(string connectionId)
    {
        if (_runs.TryRemove(connectionId, out StreamingRun? run))
        {
            await run.DisposeAsync();
        }
    }
}
