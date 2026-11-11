using System.Collections.Concurrent;
using System.Text;
using AgentCouncil.Agents.Agents;
using AgentCouncil.Agents.Workflow;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AgentCouncil.Agents.Hubs;

/// <summary>
/// One council session per SignalR connection, in one of three orchestration modes:
///
/// Handoff and group chat — a single persistent <see cref="StreamingRun"/> holds the conversation across
/// user turns. (Group chat used to need a fresh run per turn reseeded with history; fixed in 1.27.)
///
/// Magentic One — one Magentic workflow runs one task, so every user turn opens a fresh run seeded with
/// the accumulated <see cref="History"/>.
///
/// Alongside the debate, <see cref="HarnessSession"/> is the Pragmatic harness agent's own session.
/// </summary>
public sealed class CouncilSession
{
    public required string Mode { get; init; }

    /// <summary>The debate run: persistent for handoff and group chat, the current turn's run for Magentic.</summary>
    public StreamingRun? Run { get; set; }

    /// <summary>Magentic only: builds the next turn's workflow.</summary>
    public Func<Microsoft.Agents.AI.Workflows.Workflow>? MagenticFactory { get; init; }

    /// <summary>Magentic only: the conversation so far, the seed of each turn's run.</summary>
    public List<ChatMessage> History { get; } = [];

    public bool IsGroupChat => Mode == CouncilSessionManager.GroupChatMode;

    public bool IsMagentic => Mode == CouncilSessionManager.MagenticMode;

    /// <summary>Mode, todos, "always approve" rules and background tasks for the harness live here.</summary>
    public required AgentSession HarnessSession { get; init; }

    /// <summary>Debate transcript the harness agent has not been shown yet.</summary>
    public StringBuilder UnseenDebate { get; } = new();

    public string? LastSpeaker { get; set; }

    /// <summary>The write the harness is waiting on; <see cref="ToolApprovalAgent"/> surfaces one at a time.</summary>
    public ToolApprovalRequestContent? PendingApproval { get; set; }

    /// <summary>Approvals a debate agent raised inside the workflow, with who raised them, answered in order.</summary>
    public Queue<(ExternalRequest Request, string Agent)> PendingWorkflowApprovals { get; } = new();

    /// <summary>Tools the user chose "Always approve" for inside the debate workflow.</summary>
    public HashSet<string> AlwaysApprovedTools { get; } = new(StringComparer.Ordinal);

    /// <summary>Magentic: replaces the finished run with a fresh one for the next task.</summary>
    public async Task<StreamingRun> StartMagenticTurnAsync(CancellationToken cancellationToken)
    {
        if (Run is not null)
        {
            await Run.DisposeAsync();
        }

        Run = await InProcessExecution.OpenStreamingAsync(MagenticFactory!(), cancellationToken: cancellationToken);
        return Run;
    }
}

/// <summary>Holds one <see cref="CouncilSession"/> per SignalR connection.</summary>
public sealed class CouncilSessionManager(CouncilAgents agents, CouncilWorkflow council)
{
    public const string HandoffMode = "handoff";
    public const string GroupChatMode = "groupchat";
    public const string MagenticMode = "magentic";

    private readonly ConcurrentDictionary<string, CouncilSession> _sessions = new();

    /// <summary>Opens a fresh session for the connection, replacing any existing one.</summary>
    /// <param name="mode">"handoff" (default), "groupchat" or "magentic".</param>
    /// <param name="lineup">The invited debaters and their per-turn tuning. Handoff and Magentic always seat the Moderator.</param>
    /// <exception cref="ArgumentException">The line-up can't hold a debate, or names an unknown agent.</exception>
    public async Task<CouncilSession> StartAsync(
        string connectionId, string mode, int roundsPerAgent, IReadOnlyCollection<AgentSetup> lineup, CancellationToken cancellationToken)
    {
        mode = mode?.ToLowerInvariant() switch
        {
            GroupChatMode => GroupChatMode,
            MagenticMode => MagenticMode,
            _ => HandoffMode,
        };
        int rounds = Math.Clamp(roundsPerAgent, 1, 5);
        AgentSetup? moderatorSetup = lineup.FirstOrDefault(IsModerator);
        bool withModerator = mode != GroupChatMode || moderatorSetup is not null;
        List<AgentSetup> personaSetups = lineup.Where(s => !IsModerator(s)).ToList();

        if (personaSetups.Count == 0 || personaSetups.Count + (withModerator ? 1 : 0) < 2)
        {
            throw new ArgumentException("Invite at least one persona, and at least two agents in total.");
        }

        // Each session gets its own agents: token budget, effort and who is seated are chosen per line-up.
        IEnumerable<string> personaNames = personaSetups.Select(s => s.Name);
        string[] seated = withModerator ? [CouncilAgents.ModeratorName, .. personaNames] : [.. personaNames];
        bool handoff = mode == HandoffMode;
        List<AIAgent> personas = personaSetups.Select(s => agents.CreateDebater(s, seated, handoff)).ToList();

        await EndAsync(connectionId);

        AgentSession harnessSession = await agents.Harness.CreateSessionAsync(cancellationToken);
        CouncilSession session;
        if (mode == MagenticMode)
        {
            AIAgent manager = agents.CreateMagenticManager(seated);
            session = new CouncilSession
            {
                Mode = mode,
                MagenticFactory = () => council.BuildMagentic(manager, personas, rounds),
                HarnessSession = harnessSession,
            };
        }
        else
        {
            AIAgent? moderator = withModerator
                ? agents.CreateDebater(moderatorSetup ?? new AgentSetup(CouncilAgents.ModeratorName), seated, handoff)
                : null;
            Microsoft.Agents.AI.Workflows.Workflow workflow = handoff
                ? council.BuildHandoff(moderator!, personas)
                : council.BuildGroupChat(moderator is null ? personas : [moderator, .. personas], rounds);
            session = new CouncilSession
            {
                Mode = mode,
                Run = await InProcessExecution.OpenStreamingAsync(workflow, cancellationToken: cancellationToken),
                HarnessSession = harnessSession,
            };
        }

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

        if (session.Run is not null)
        {
            await session.Run.DisposeAsync();
        }
    }

    private static bool IsModerator(AgentSetup setup) =>
        string.Equals(setup.Name, CouncilAgents.ModeratorName, StringComparison.OrdinalIgnoreCase);
}
