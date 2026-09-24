using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;

namespace AgentCouncil.Agents.Agents;

/// <summary>
/// How one debater is tuned for a session: how many output tokens it may spend per turn and how
/// hard the model should think. <paramref name="Effort"/> is "auto" (send nothing, the model's own
/// default) or a <see cref="ReasoningEffort"/> name — only reasoning deployments accept the latter.
/// </summary>
public sealed record AgentSetup(string Name, int MaxOutputTokens = 400, string Effort = "auto")
{
    /// <summary>The soft instruction that pairs with the hard <see cref="MaxOutputTokens"/> cap.</summary>
    public string Style => MaxOutputTokens switch
    {
        <= 250 => "exactly one punchy sentence",
        <= 700 => "at most 2 sentences",
        _ => "up to 4 sentences, and you may spend one of them on a concrete example",
    };
}

/// <summary>
/// Builds and holds the council agents. The four debaters exist twice over: as singletons for DevUI,
/// and as per-session clones built by <see cref="CreateDebater"/> with the line-up's chosen token
/// budget and reasoning effort. Every piece of per-conversation state (history, mode, todos, approval
/// rules, background tasks) lives in an <see cref="AgentSession"/>, never on the agent.
///
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

    private readonly IChatClient _chatClient;
    private readonly IChatClient _cheapClient;
    private readonly string _skillsRoot;

    public CouncilAgents(
        IChatClient chatClient,
        [FromKeyedServices(AzureOpenAIChatClient.CheapClientKey)] IChatClient cheapClient,
        IHostEnvironment environment)
    {
        _chatClient = chatClient;
        _cheapClient = cheapClient;
        _skillsRoot = Path.Combine(environment.ContentRootPath, "skills");
        WorkspaceRoot = Path.Combine(environment.ContentRootPath, "workspace");
        Directory.CreateDirectory(WorkspaceRoot);

        // ponytail: one workspace shared by every connection (the agents are singletons); switch to an
        // InMemoryAgentFileStore per session if two browser tabs writing the same files becomes a problem.
        Store = new FileSystemAgentFileStore(WorkspaceRoot);

        Moderator = CreateDebater(new AgentSetup(ModeratorName));
        Optimist = CreateDebater(new AgentSetup(OptimistName));
        Skeptic = CreateDebater(new AgentSetup(SkepticName));
        Pragmatist = CreateDebater(new AgentSetup(PragmatistName));

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

    /// <summary>Builds one debater tuned for a session. Agents are stateless, so a fresh one per session is free.</summary>
    /// <exception cref="ArgumentException"><paramref name="setup"/> names an agent that isn't on the council.</exception>
    public AIAgent CreateDebater(AgentSetup setup)
    {
        (IChatClient client, string description, string persona) = setup.Name switch
        {
            ModeratorName => (_chatClient,
                "Opens and orchestrates the council debate and decides when to ask the user for input.",
                """
                You are the Moderator of a council debating the user's topic. Frame or sharpen the debate,
                then hand the floor to one of the personas you can hand off to; once a few distinct angles
                have been heard, instead ask the user one focused question.
                """),
            OptimistName => (_cheapClient,
                "Argues the upside, opportunities and benefits.",
                """
                You are the Optimist on the council: argue the upside with concrete, specific benefits,
                engaging the Skeptic's latest point head-on. Then hand the floor back to the Moderator.
                """),
            SkepticName => (_cheapClient,
                "Surfaces risks and counter-arguments.",
                """
                You are the Skeptic on the council: surface the sharpest risk, hidden cost, or failure
                mode, countering the Optimist with specifics, not vague doubt. When a workspace file such
                as decision-record.md has been written or mentioned, grep or read it first and rebut its
                weakest specific line. Then hand the floor back to the Moderator.
                """),
            PragmatistName => (_cheapClient,
                "Grounds the debate in trade-offs and next steps.",
                """
                You are the Pragmatist on the council: weigh the trade-off on the table and propose one
                concrete, realistic next step. Then hand the floor back to the Moderator.
                """),
            _ => throw new ArgumentException($"'{setup.Name}' is not a council debater.", nameof(setup)),
        };

        return new ChatClientAgent(client, new ChatClientAgentOptions
        {
            Name = setup.Name,
            Description = description,
            ChatOptions = new()
            {
                // The budget is enforced twice: as a prompt rule (so the turn *ends*) and as a hard cap
                // (so a runaway turn is truncated rather than billed).
                Instructions = $"{persona}\nTurn length: {setup.Style}. Never exceed {setup.MaxOutputTokens} output tokens.",
                MaxOutputTokens = setup.MaxOutputTokens,
                Reasoning = Enum.TryParse(setup.Effort, ignoreCase: true, out ReasoningEffort effort)
                    ? new ReasoningOptions { Effort = effort }
                    : null,
            },
            AIContextProviders = setup.Name == SkepticName
                ?
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
                        _skillsRoot,
                        options: new AgentSkillsProviderOptions
                        {
                            DisableLoadSkillApproval = true,
                            DisableReadSkillResourceApproval = true,
                        }),
                    Compaction(_cheapClient),
                ]
                : [Compaction(_cheapClient)],
        });
    }

    public string[] ListWorkspaceFiles() => Directory
        .EnumerateFiles(WorkspaceRoot, "*", SearchOption.AllDirectories)
        .Select(path => Path.GetRelativePath(WorkspaceRoot, path).Replace('\\', '/'))
        .Order(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    // Each agent gets its own provider; they never share a session, so the default state key is fine.
    private static CompactionProvider Compaction(IChatClient summarizer) =>
        new(new SummarizationCompactionStrategy(summarizer, CompactionTriggers.TokensExceed(6000)));
}
