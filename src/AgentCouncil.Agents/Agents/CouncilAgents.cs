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
/// Builds and holds the council agents. The five debaters exist twice over: as singletons for DevUI,
/// and as per-session clones built by <see cref="CreateDebater"/> with the line-up's chosen token
/// budget, reasoning effort and seating. Every piece of per-conversation state (history, mode, todos,
/// approval rules, background tasks) lives in an <see cref="AgentSession"/>, never on the agent.
///
/// <see cref="Harness"/> is the Pragmatic "with hands": a standalone agent with plan/execute modes, a
/// todo loop and a background Researcher, which the hub runs on its own session. Since 1.27, tool
/// approvals also round-trip through the debate workflows, so the in-debate Pragmatic can write a file
/// on request too (the hub answers the workflow's <c>RequestInfoEvent</c>).
/// </summary>
public sealed class CouncilAgents
{
    // Agent names are used as DevUI identifiers and as the labels the Blazor UI shows per turn.
    public const string ModeratorName = "Moderator";
    public const string OptimisticName = "Optimistic";
    public const string SkepticName = "Skeptic";
    public const string PragmaticName = "Pragmatic";
    public const string PessimisticName = "Pessimistic";
    public const string ResearcherName = "Researcher";

    /// <summary>Every debater, Moderator first (the handoff start agent).</summary>
    public static readonly string[] DebaterNames = [ModeratorName, OptimisticName, SkepticName, PragmaticName, PessimisticName];

    // What makes the debate sound like people talking rather than a panel of reports.
    private const string Conversation =
        """
        This is a live, spoken conversation, not a report. Look at who spoke just before you and react to
        them by name first: agree, tease or push back ("Good one, Skeptic, but...", "Oh, come on,
        Optimistic..."). When that was Your Majesty, answer Your Majesty directly and do what they asked.
        Sound like a person in a lively meeting: use contractions, no bullet points, no headings, and
        never announce your own role. When a fact, figure or recent event would settle a point, you may
        search the web first; never search for opinions.
        """;

    private readonly IChatClient _chatClient;
    private readonly IChatClient _cheapClient;
    private readonly string _skillsRoot;

    public CouncilAgents(
        IChatClient chatClient,
        [FromKeyedServices(AzureOpenAIChatClient.CheapClientKey)] IChatClient cheapClient,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        _chatClient = chatClient;
        _cheapClient = cheapClient;
        _skillsRoot = Path.Combine(environment.ContentRootPath, "skills");
        // The container runs as a non-root user that can't write under /app, so the AppHost points this elsewhere.
        WorkspaceRoot = configuration["WorkspaceRoot"] ?? Path.Combine(environment.ContentRootPath, "workspace");
        Directory.CreateDirectory(WorkspaceRoot);

        // ponytail: one workspace shared by every connection (the agents are singletons); switch to an
        // InMemoryAgentFileStore per session if two browser tabs writing the same files becomes a problem.
        Store = new FileSystemAgentFileStore(WorkspaceRoot);

        Moderator = CreateDebater(new AgentSetup(ModeratorName));
        Optimistic = CreateDebater(new AgentSetup(OptimisticName));
        Skeptic = CreateDebater(new AgentSetup(SkepticName));
        Pragmatic = CreateDebater(new AgentSetup(PragmaticName));
        Pessimistic = CreateDebater(new AgentSetup(PessimisticName));

        HarnessTodos = new TodoProvider();
        HarnessModes = new AgentModeProvider();
        HarnessBackground = new BackgroundAgentsProvider(
            [
                new ChatClientAgent(
                    cheapClient,
                    instructions:
                        """
                        You are the council's Researcher. Search the web, then answer the question you are
                        given with at most 3 concise bullet points of facts, precedents or figures, citing
                        sources and flagging anything uncertain.
                        """,
                    name: ResearcherName,
                    description: "Gathers facts, precedents and figures on a focused question.",
                    tools: [new HostedWebSearchTool()]),
            ],
            new BackgroundAgentsProviderOptions());

        // Compaction sits on the harness's chat client, beneath function invocation, so it compacts per
        // service call. On the agent (AIContextProviders) it still breaks approvals in 1.27 together with
        // TodoProvider: the approved call never runs and the model replays its last tool calls.
        IChatClient harnessClient = chatClient.AsBuilder().UseAIContextProviders(Compaction(cheapClient)).Build();

        AIAgent pragmaticWithHands = new ChatClientAgent(harnessClient, new ChatClientAgentOptions
        {
            Name = PragmaticName,
            Description = "Turns the council's debate into files in the shared workspace.",
            ChatOptions = new()
            {
                Instructions =
                    """
                    You are Pragmatic, and you act for the council: you turn its debate into files in
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
                    """ + "\n" + Cast(DebaterNames),
            },
            AIContextProviders =
            [
                // Reads flow freely; every write surfaces a ToolApprovalRequestContent.
                new FileAccessProvider(Store, new FileAccessProviderOptions { DisableReadOnlyToolApproval = true }),
                HarnessTodos,
                HarnessModes,
                HarnessBackground,
            ],
        });

        // Order: approval middleware wraps the agent (queues requests, persists "always approve" rules in
        // the session), and the loop wraps that — it keeps re-invoking while execute-mode todos remain and
        // stops as soon as an iteration surfaces a pending approval.
        Harness = new LoopAgent(
            new AIAgentBuilder(pragmaticWithHands).UseToolApproval().Build(),
            new TodoCompletionLoopEvaluator(new TodoCompletionLoopEvaluatorOptions { Modes = ["execute"] }),
            new LoopAgentOptions { MaxIterations = 12, ExcludeOnBehalfOfMessages = true });
    }

    public AIAgent Moderator { get; }

    public AIAgent Optimistic { get; }

    public AIAgent Skeptic { get; }

    public AIAgent Pragmatic { get; }

    public AIAgent Pessimistic { get; }

    public IReadOnlyList<AIAgent> Personas => [Optimistic, Skeptic, Pragmatic, Pessimistic];

    /// <summary>Pragmatic with file tools, plan/execute modes, todos, a Researcher, approvals and a todo loop.</summary>
    public AIAgent Harness { get; }

    public TodoProvider HarnessTodos { get; }

    public AgentModeProvider HarnessModes { get; }

    public BackgroundAgentsProvider HarnessBackground { get; }

    public string WorkspaceRoot { get; }

    public AgentFileStore Store { get; }

    /// <summary>Builds one debater tuned for a session. Agents are stateless, so a fresh one per session is free.</summary>
    /// <param name="seated">Who is at the table, so the agent only addresses members who are there. Defaults to everyone.</param>
    /// <param name="canHandOff">Handoff mode: the agent passes the floor itself, so it is told whom to hand off to.</param>
    /// <exception cref="ArgumentException"><paramref name="setup"/> names an agent that isn't on the council.</exception>
    public AIAgent CreateDebater(AgentSetup setup, IReadOnlyCollection<string>? seated = null, bool canHandOff = true)
    {
        // Personas pass the floor around the table; the Moderator calls on people and hands back to the user.
        string handOff = !canHandOff ? string.Empty
            : setup.Name == ModeratorName
                ? """
                  Call on one member by name and call the handoff tool for them. When the floor comes back
                  to you after a few voices have been heard, sum up the clash in one line and ask Your
                  Majesty one focused question, without handing off.
                  """
                : """
                  Finish every turn by calling a handoff tool: pass the floor to the member you just named,
                  or to the Moderator once a point has gone back and forth a couple of times. Naming someone
                  without calling the handoff tool leaves them silent. Never hand straight back to whoever
                  just handed to you.
                  """;

        (IChatClient client, string description, string persona) = setup.Name switch
        {
            ModeratorName => (_chatClient,
                "Chairs the council debate and decides when to ask the user for input.",
                """
                You are the Moderator of a council debating Your Majesty's topic. Frame or sharpen the
                debate and keep it moving between the members, always by name.
                """),
            OptimisticName => (_cheapClient,
                "Argues the upside, opportunities and benefits.",
                """
                You are Optimistic on the council: argue the upside with concrete, specific benefits, and
                take on the gloomiest point just made head-on.
                """),
            SkepticName => (_cheapClient,
                "Surfaces risks and counter-arguments.",
                """
                You are Skeptic on the council: surface the sharpest risk, hidden cost or failure mode,
                with specifics, not vague doubt. When a workspace file such as decision-record.md has been
                written or mentioned, grep or read it first and rebut its weakest specific line.
                """),
            PragmaticName => (_cheapClient,
                "Grounds the debate in trade-offs and next steps, and writes things down when asked.",
                """
                You are Pragmatic on the council: weigh the trade-off on the table and propose one
                concrete, realistic next step. When Your Majesty asks you to write something down, write it
                right away with file_access_write, without asking anyone first (Your Majesty approves every
                write). Never write files unasked.
                """),
            PessimisticName => (_cheapClient,
                "Complains about everything and jokes at everyone else's expense.",
                """
                You are Pessimistic on the council: you complain about everything (the topic, the meeting,
                the coffee) and make dry jokes at the others' expense, especially Optimistic's sunshine
                and Pragmatic's spreadsheets. Never cruel, always funny, and slip in one genuine worry.
                """),
            _ => throw new ArgumentException($"'{setup.Name}' is not a council debater.", nameof(setup)),
        };

        AIContextProvider[] capabilities = setup.Name switch
        {
            SkepticName =>
            [
                // Read-only: the Skeptic can grep what Pragmatic wrote, never change it.
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
            ],

            // Writes need approval, which surfaces from the workflow as a RequestInfoEvent (fixed in 1.27).
            PragmaticName =>
            [
                new FileAccessProvider(Store, new FileAccessProviderOptions { DisableReadOnlyToolApproval = true }),
                Compaction(_cheapClient),
            ],
            _ => [Compaction(_cheapClient)],
        };

        return new ChatClientAgent(client, new ChatClientAgentOptions
        {
            Name = setup.Name,
            Description = description,
            ChatOptions = new()
            {
                // The budget is enforced twice: as a prompt rule (so the turn *ends*) and as a hard cap
                // (so a runaway turn is truncated rather than billed).
                Instructions =
                    $"{persona}\n{Conversation}\n{handOff}\n{Cast(seated ?? DebaterNames)}\n"
                    + $"Turn length: {setup.Style}. Never exceed {setup.MaxOutputTokens} output tokens.",
                MaxOutputTokens = setup.MaxOutputTokens,

                // The summary feeds the UI's reasoning log; non-reasoning deployments reject both, hence "auto" sends neither.
                Reasoning = Enum.TryParse(setup.Effort, ignoreCase: true, out ReasoningEffort effort)
                    ? new ReasoningOptions { Effort = effort, Output = ReasoningOutput.Summary }
                    : null,

                // Hosted by the Responses API: the model searches on its own, no approval needed.
                Tools = [new HostedWebSearchTool()],
            },
            // First, so compaction and the model never see a half-removed tool exchange.
            AIContextProviders = [new OrphanToolContentFilter(), .. capabilities],
        });
    }

    /// <summary>
    /// The Magentic One manager. It writes the task and progress ledgers as JSON, so it gets no token cap
    /// and no tools (Magentic doesn't support tool calls on the manager).
    /// </summary>
    public AIAgent CreateMagenticManager(IReadOnlyCollection<string> seated) => new ChatClientAgent(_chatClient, new ChatClientAgentOptions
    {
        Name = ModeratorName,
        Description = "Plans the debate, picks who speaks next and delivers the verdict.",
        ChatOptions = new()
        {
            Instructions =
                """
                You are the Moderator chairing the council. Give every member a fair hearing and pick the
                member whose view the last speaker challenged. Your final answer is a short spoken verdict
                to Your Majesty, two or three sentences, crediting members by name.
                """ + "\n" + Cast(seated),
        },

        // Tags the members' lines by name, which the Responses API would otherwise drop.
        AIContextProviders = [new OrphanToolContentFilter()],
    });

    public string[] ListWorkspaceFiles() => Directory
        .EnumerateFiles(WorkspaceRoot, "*", SearchOption.AllDirectories)
        .Select(path => Path.GetRelativePath(WorkspaceRoot, path).Replace('\\', '/'))
        .Order(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static string Cast(IEnumerable<string> seated) =>
        $"Around the table: {string.Join(", ", seated)}. The user is Your Majesty. Call everyone by these names.";

    // Each agent gets its own provider; they never share a session, so the default state key is fine.
    private static CompactionProvider Compaction(IChatClient summarizer) =>
        new(new SummarizationCompactionStrategy(summarizer, CompactionTriggers.TokensExceed(6000)));
}
