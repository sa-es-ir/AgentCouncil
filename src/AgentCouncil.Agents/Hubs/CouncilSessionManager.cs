using System.Collections.Concurrent;
using System.Text;
using AgentCouncil.Agents.Agents;
using AgentCouncil.Agents.Workflow;
using Microsoft.Agents.AI;
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
/// each user turn opens a fresh run seeded with the accumulated <see cref="History"/>. (Hosting it as an
/// agent doesn't help: a second run on the same workflow session starts with no history.)
///
/// Alongside the debate, <see cref="HarnessSession"/> is the Pragmatist harness agent's own session.
/// </summary>
public sealed class CouncilSession
{
    public StreamingRun? HandoffRun { get; init; }

    public Microsoft.Agents.AI.Workflows.Workflow? GroupChatWorkflow { get; init; }

    public List<ChatMessage> History { get; } = [];

    public bool IsGroupChat => GroupChatWorkflow is not null;

    /// <summary>Mode, todos, "always approve" rules and background tasks for the harness live here.</summary>
    public required AgentSession HarnessSession { get; init; }

    /// <summary>Debate transcript the harness agent has not been shown yet.</summary>
    public StringBuilder UnseenDebate { get; } = new();

    public string? LastSpeaker { get; set; }

    /// <summary>The write the harness is waiting on; <see cref="ToolApprovalAgent"/> surfaces one at a time.</summary>
    public ToolApprovalRequestContent? PendingApproval { get; set; }
}

/// <summary>Holds one <see cref="CouncilSession"/> per SignalR connection.</summary>
public sealed class CouncilSessionManager(CouncilAgents agents, CouncilWorkflow council)
{
    public const string GroupChatMode = "groupchat";

    private readonly ConcurrentDictionary<string, CouncilSession> _sessions = new();

    /// <summary>Opens a fresh session for the connection, replacing any existing one.</summary>
    /// <param name="agentNames">The invited debaters. Handoff always seats the Moderator.</param>
    /// <exception cref="ArgumentException">The line-up can't hold a debate.</exception>
    public async Task<CouncilSession> StartAsync(
        string connectionId, string mode, int roundsPerAgent, IReadOnlyCollection<string> agentNames, CancellationToken cancellationToken)
    {
        bool groupChat = string.Equals(mode, GroupChatMode, StringComparison.OrdinalIgnoreCase);
        List<AIAgent> personas = agents.Personas
            .Where(a => agentNames.Contains(a.Name!, StringComparer.OrdinalIgnoreCase))
            .ToList();
        bool withModerator = !groupChat || agentNames.Contains(CouncilAgents.ModeratorName, StringComparer.OrdinalIgnoreCase);

        if (personas.Count == 0 || personas.Count + (withModerator ? 1 : 0) < 2)
        {
            throw new ArgumentException("Invite at least one persona, and at least two agents in total.");
        }

        await EndAsync(connectionId);

        AgentSession harnessSession = await agents.Harness.CreateSessionAsync(cancellationToken);
        CouncilSession session = groupChat
            ? new CouncilSession
            {
                GroupChatWorkflow = council.BuildGroupChat(
                    withModerator ? [agents.Moderator, .. personas] : personas, Math.Clamp(roundsPerAgent, 1, 10)),
                HarnessSession = harnessSession,
            }
            : new CouncilSession
            {
                HandoffRun = await InProcessExecution.OpenStreamingAsync(
                    council.BuildHandoff(personas), cancellationToken: cancellationToken),
                HarnessSession = harnessSession,
            };

        _sessions[connectionId] = session;
        return session;
    }

    public bool TryGet(string connectionId, out CouncilSession session) => _sessions.TryGetValue(connectionId, out session!);

    public async Task EndAsync(string connectionId)
    {
        if (!_sessions.TryRemove(connectionId, out CouncilSession? session))
        {
            return;
        }

        // Background tasks keep calling the model after the tab closes unless the session is released.
        await agents.HarnessBackground.ReleaseSessionAsync(session.HarnessSession, cancelRunning: true);

        if (session.HandoffRun is not null)
        {
            await session.HandoffRun.DisposeAsync();
        }
    }
}
