using System.Collections.Concurrent;
using AgentCouncil.Agents.Workflow;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AgentCouncil.Agents.Hubs;

/// <summary>
/// One council session per SignalR connection, in one of two orchestration modes:
///
/// Handoff — a single persistent <see cref="StreamingRun"/> preserves the conversation across
/// user turns (the handoff workflow keeps accepting input on the same run).
///
/// Group chat — the round-robin workflow terminates after the configured number of rounds, so
/// each user turn opens a fresh run seeded with the accumulated <see cref="History"/>.
/// </summary>
public sealed class CouncilSession
{
    public StreamingRun? HandoffRun { get; init; }
    public Microsoft.Agents.AI.Workflows.Workflow? GroupChatWorkflow { get; init; }
    public List<ChatMessage> History { get; } = [];

    public bool IsGroupChat => GroupChatWorkflow is not null;
}

/// <summary>Holds one <see cref="CouncilSession"/> per SignalR connection.</summary>
public sealed class CouncilSessionManager(CouncilWorkflow council)
{
    public const string GroupChatMode = "groupchat";

    private readonly ConcurrentDictionary<string, CouncilSession> _sessions = new();

    /// <summary>Opens a fresh session for the connection, replacing any existing one.</summary>
    public async Task<CouncilSession> StartAsync(
        string connectionId, string mode, int roundsPerAgent, CancellationToken cancellationToken)
    {
        await EndAsync(connectionId);

        CouncilSession session = string.Equals(mode, GroupChatMode, StringComparison.OrdinalIgnoreCase)
            ? new CouncilSession { GroupChatWorkflow = council.BuildGroupChat(Math.Clamp(roundsPerAgent, 1, 10)) }
            : new CouncilSession
            {
                HandoffRun = await InProcessExecution.OpenStreamingAsync(council.Handoff, cancellationToken: cancellationToken),
            };

        _sessions[connectionId] = session;
        return session;
    }

    public bool TryGet(string connectionId, out CouncilSession session) => _sessions.TryGetValue(connectionId, out session!);

    public async Task EndAsync(string connectionId)
    {
        if (_sessions.TryRemove(connectionId, out CouncilSession? session) && session.HandoffRun is not null)
        {
            await session.HandoffRun.DisposeAsync();
        }
    }
}
