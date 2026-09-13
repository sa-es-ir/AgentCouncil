using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;

namespace AgentCouncil.Agents.Agents;

/// <summary>
/// Builds and holds the council agents. All are singletons: every piece of per-conversation state
/// (history, mode, todos, approval rules, background tasks) lives in an <see cref="AgentSession"/>.
///
/// The four debaters are reused by the orchestration workflows (driven from the SignalR hub) and by DevUI.
/// <see cref="Harness"/> is the Pragmatist "with hands": a standalone agent the hub runs on its own
/// session, because tool approvals cannot round-trip through a workflow in Agent Framework 1.19
/// (see ai-plans/option-b-implementation-phases.md, Phase 1 fallback).
/// </summary>
public sealed class CouncilAgents
{
    // Agent names are used as DevUI identifiers and as the labels the Blazor UI shows per turn.
    public const string ModeratorName = "Moderator";
    public const string OptimistName = "Optimist";
    public const string SkepticName = "Skeptic";
    public const string PragmatistName = "Pragmatist";
    public const string ResearcherName = "Researcher";

    public CouncilAgents(
        IChatClient chatClient,
        [FromKeyedServices(AzureOpenAIChatClient.CheapClientKey)] IChatClient cheapClient,
        IHostEnvironment environment)
    {
        WorkspaceRoot = Path.Combine(environment.ContentRootPath, "workspace");
        Directory.CreateDirectory(WorkspaceRoot);

        // ponytail: one workspace shared by every connection (the agents are singletons); switch to an
        // InMemoryAgentFileStore per session if two browser tabs writing the same files becomes a problem.
        Store = new FileSystemAgentFileStore(WorkspaceRoot);

        // All debaters are hard-capped at 2 sentences: keeps the debate sharp and demo-friendly.
        Moderator = new ChatClientAgent(chatClient, new ChatClientAgentOptions
        {
            Name = ModeratorName,
            Description = "Opens and orchestrates the council debate and decides when to ask the user for input.",
            ChatOptions = new()
            {
                Instructions =
                    """
                    You are the Moderator of a council debating the user's topic. Frame or sharpen the
                    debate in at most 2 short sentences, then hand the floor to one of the personas you can
                    hand off to; once a few distinct angles have been heard, instead ask the user one focused
                    question. Never exceed 2 sentences.
                    """,
            },
            AIContextProviders = [Compaction(cheapClient)],
        });

        Optimist = new ChatClientAgent(cheapClient, new ChatClientAgentOptions
        {
            Name = OptimistName,
            Description = "Argues the upside, opportunities and benefits.",
            ChatOptions = new()
            {
                Instructions =
                    """
                    You are the Optimist on the council: argue the upside with concrete, specific
                    benefits, engaging the Skeptic's latest point head-on. Maximum 2 sentences per
                    turn — sharp and punchy — then hand the floor back to the Moderator.
                    """,
            },
            AIContextProviders = [Compaction(cheapClient)],
        });

        Skeptic = new ChatClientAgent(cheapClient, new ChatClientAgentOptions
        {
            Name = SkepticName,
            Description = "Surfaces risks and counter-arguments.",
            ChatOptions = new()
            {
                Instructions =
                    """
                    You are the Skeptic on the council: surface the sharpest risk, hidden cost, or
                    failure mode, countering the Optimist with specifics, not vague doubt. When a workspace
                    file such as decision-record.md has been written or mentioned, grep or read it first and
                    rebut its weakest specific line. Maximum 2 sentences per turn, then hand the floor back
                    to the Moderator.
                    """,
            },
            AIContextProviders =
            [
                // Read-only: the Skeptic can grep what the Pragmatist wrote, never change it. No approval
                // needed, which matters because approvals cannot surface from inside the workflow.
                new FileAccessProvider(Store, new FileAccessProviderOptions
                {
                    DisableWriteTools = true,
                    DisableReadOnlyToolApproval = true,
                }),

                // SKILL.md files under skills/ become capabilities with no code change.
                new AgentSkillsProvider(
                    Path.Combine(environment.ContentRootPath, "skills"),
                    options: new AgentSkillsProviderOptions
                    {
                        DisableLoadSkillApproval = true,
                        DisableReadSkillResourceApproval = true,
                    }),
                Compaction(cheapClient),
            ],
        });

        Pragmatist = new ChatClientAgent(cheapClient, new ChatClientAgentOptions
        {
            Name = PragmatistName,
            Description = "Grounds the debate in trade-offs and next steps.",
            ChatOptions = new()
            {
                Instructions =
                    """
                    You are the Pragmatist on the council: weigh the trade-off on the table and
                    propose one concrete, realistic next step. Maximum 2 sentences per turn, then
                    hand the floor back to the Moderator.
                    """,
            },
            AIContextProviders = [Compaction(cheapClient)],
        });

        HarnessTodos = new TodoProvider();
        HarnessModes = new AgentModeProvider();
        HarnessBackground = new BackgroundAgentsProvider(
            [
                new ChatClientAgent(
                    cheapClient,
                    instructions:
                        """
                        You are the council's Researcher. Answer the question you are given with at most
                        3 concise bullet points of facts, precedents or figures, flagging anything uncertain.
                        """,
                    name: ResearcherName,
                    description: "Gathers facts, precedents and figures on a focused question."),
            ],
            new BackgroundAgentsProviderOptions());

        AIAgent pragmatistWithHands = new ChatClientAgent(chatClient, new ChatClientAgentOptions
        {
            Name = PragmatistName,
            Description = "Turns the council's debate into files in the shared workspace.",
            ChatOptions = new()
            {
                Instructions =
                    """
                    You are the Pragmatist, and you act for the council: you turn its debate into files in
                    the shared workspace.
                    - In plan mode: propose a short todo list with todos_add (2-4 items, each producing or
                      updating one file), then ask the user in one sentence to confirm. Never write files
                      in plan mode.
                    - When the user confirms, switch to execute mode with mode_set and work through the
                      todos: write each file with file_access_write, then complete its todo.
                    - If a figure or precedent would strengthen a file, start a background task on the
                      Researcher and keep working while it runs.
                    - When every todo is complete, switch back to plan mode and say in one sentence what
                      you wrote.
                    If the user denies a write, do not retry it; ask what to change. Keep chat replies to
                    1-2 sentences; the files carry the detail.
                    """,
            },
            AIContextProviders =
            [
                // Reads flow freely; every write surfaces a ToolApprovalRequestContent.
                new FileAccessProvider(Store, new FileAccessProviderOptions { DisableReadOnlyToolApproval = true }),
                HarnessTodos,
                HarnessModes,
                HarnessBackground,

                // No CompactionProvider here: combined with TodoProvider it breaks approval matching in 1.19 —
                // the approved call never runs and the model replays its last tool call.
            ],
        });

        // Order: approval middleware wraps the agent (queues requests, persists "always approve" rules in
        // the session), and the loop wraps that — it keeps re-invoking while execute-mode todos remain and
        // stops as soon as an iteration surfaces a pending approval.
        Harness = new LoopAgent(
            new AIAgentBuilder(pragmatistWithHands).UseToolApproval().Build(),
            new TodoCompletionLoopEvaluator(new TodoCompletionLoopEvaluatorOptions { Modes = ["execute"] }),
            new LoopAgentOptions { MaxIterations = 12, ExcludeOnBehalfOfMessages = true });
    }

    public AIAgent Moderator { get; }

    public AIAgent Optimist { get; }

    public AIAgent Skeptic { get; }

    public AIAgent Pragmatist { get; }

    /// <summary>All debaters, with the Moderator first (the handoff start agent).</summary>
    public IReadOnlyList<AIAgent> All => [Moderator, Optimist, Skeptic, Pragmatist];

    public IReadOnlyList<AIAgent> Personas => [Optimist, Skeptic, Pragmatist];

    /// <summary>The Pragmatist with file tools, plan/execute modes, todos, a Researcher, approvals and a todo loop.</summary>
    public AIAgent Harness { get; }

    public TodoProvider HarnessTodos { get; }

    public AgentModeProvider HarnessModes { get; }

    public BackgroundAgentsProvider HarnessBackground { get; }

    public string WorkspaceRoot { get; }

    public AgentFileStore Store { get; }

    public string[] ListWorkspaceFiles() => Directory
        .EnumerateFiles(WorkspaceRoot, "*", SearchOption.AllDirectories)
        .Select(path => Path.GetRelativePath(WorkspaceRoot, path).Replace('\\', '/'))
        .Order(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    // Each agent gets its own provider; they never share a session, so the default state key is fine.
    private static CompactionProvider Compaction(IChatClient summarizer) =>
        new(new SummarizationCompactionStrategy(summarizer, CompactionTriggers.TokensExceed(6000)));
}
