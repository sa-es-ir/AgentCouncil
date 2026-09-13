# Building an agent harness with Microsoft Agent Framework 1.19 — how AgentCouncil does it

Companion to [AGENT-FRAMEWORK.md](AGENT-FRAMEWORK.md), which covers the basics: agents, workflows, the SignalR transport and Aspire. This guide covers what came next. The council stopped only *talking* and started *doing*: it writes files, asks your permission first, plans, keeps a todo list, loads skills, delegates to a helper and runs on two models.

Every section has **the idea**, **the real code in this repo** and, where useful, **a standalone sample** you can paste into a console app to play with. The last part, *What didn't work*, may be the most valuable: those were verified against the real 1.19 packages and a live Azure OpenAI deployment, and the docs don't mention them.

---

## 1. What is a "harness"?

The model only decides *the next step*: some text, or a tool call. Everything around that decision is the **harness**:

- reading and writing files
- asking before doing something risky
- remembering what's in progress
- deciding when the work is done
- delegating to a helper
- staying inside the context window

Claude Code, Copilot agent mode and Cursor are harnesses.

Since 1.19, the GA `Microsoft.Agents.AI` package ships each of those pieces as a type:

| Harness concern | 1.19 type | Where in this repo |
|---|---|---|
| File tools (read/write/ls/grep/replace) | `FileAccessProvider` over `AgentFileStore` | `CouncilAgents.cs`: the Skeptic (read-only) and the harness Pragmatist (read + gated write) |
| Permission prompts | `ToolApprovalAgent` (`UseToolApproval()`) | `CouncilAgents.Harness`, `CouncilHub.RespondToApproval` |
| Plan mode | `AgentModeProvider` | `CouncilAgents.HarnessModes` |
| Todo list | `TodoProvider` | `CouncilAgents.HarnessTodos` |
| Keep going until done | `LoopAgent` + `TodoCompletionLoopEvaluator` | `CouncilAgents.Harness` |
| Skills (SKILL.md) | `AgentSkillsProvider` | the Skeptic + `skills/devils-advocate/SKILL.md` |
| Subagents | `BackgroundAgentsProvider` | `CouncilAgents.HarnessBackground` (the Researcher) |
| Context compaction | `CompactionProvider` + `SummarizationCompactionStrategy` | the four debaters |
| Workflow graph for slides | `WorkflowVisualizer.ToMermaidString` | `GET /council/graph` |

> Most of these types are marked `[Experimental]` and raise **`MAAI001`** at compile time. The Agents csproj suppresses it with `<NoWarn>$(NoWarn);MAAI001</NoWarn>`.

---

## 2. The one concept that makes it all compose: `AIContextProvider` + `AgentSession`

Almost every harness piece is an **`AIContextProvider`**. Before each model call, a provider can add:

- **instructions** (e.g. "you are in plan mode", "here are the files tools")
- **tools** (e.g. `file_access_write`, `todos_add`, `mode_set`)
- **messages** (e.g. a synthetic "current todo list" message)

Providers hold **no conversation state themselves**. State lives in the **`AgentSession`**, in its `StateBag`. That's why one agent instance can safely serve every user: each user gets their own session.

```csharp
// One agent instance (a singleton) ...
AIAgent agent = new ChatClientAgent(chatClient, new ChatClientAgentOptions
{
    Name = "Pragmatist",
    ChatOptions = new() { Instructions = "..." },
    AIContextProviders = [new TodoProvider(), new AgentModeProvider()],
});

// ... many sessions. Todos and mode are stored per session.
AgentSession alice = await agent.CreateSessionAsync();
AgentSession bob   = await agent.CreateSessionAsync();

await foreach (var update in agent.RunStreamingAsync("Plan the release.", alice)) { /* ... */ }
```

Since the state lives in a session *you* hold, the **host can read it**, not just the model. The hub uses this to drive the mode chip and the todo panel:

```csharp
// CouncilHub.PushHarnessStateAsync
string mode = await agents.HarnessModes.GetModeAsync(session.HarnessSession, ct);
var todos   = await agents.HarnessTodos.GetAllTodosAsync(session.HarnessSession, ct);
int running = agents.HarnessBackground.GetIncompleteTasks(session.HarnessSession).Count;
```

**Middleware** is the other building block. A `DelegatingAIAgent` wraps another agent: `ToolApprovalAgent` and `LoopAgent` both work this way. You stack them like ASP.NET middleware:

```csharp
AIAgent pipeline = new LoopAgent(                      // outermost: keep going until done
    new AIAgentBuilder(innerAgent).UseToolApproval()   // middle: queue approvals, remember "always" rules
        .Build(),
    evaluator, options);
```

---

## 3. The architecture after this work — and *why* it's split in two

```
┌─────────────────────────── one SignalR connection = one CouncilSession ────────────────────────────┐
│                                                                                                      │
│  DEBATE (talks)                                   HARNESS (acts)                                     │
│  Workflow: handoff or round-robin group chat      Standalone agent + its own AgentSession            │
│  Moderator · Optimist · Skeptic · Pragmatist      LoopAgent → ToolApprovalAgent → ChatClientAgent    │
│  (only the ones you invited)                      (the "Pragmatist with hands")                      │
│  • tools that need NO approval                    • file writes behind approval                      │
│    (Skeptic: read files, load skills)             • plan/execute modes, todos, Researcher            │
│  • state: StreamingRun / history list             • state: HarnessSession (hub can read it)          │
│                                                                                                      │
│                       ▲ shared workspace folder (FileSystemAgentFileStore) ▲                         │
└──────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

The original plan was to turn the whole workflow into one agent (`workflow.AsAIAgent()`) and put approvals around it. A live spike showed that **tool approvals can't round-trip through a workflow in 1.19** (details in §12). The plan had a fallback, "run the Pragmatist alone as a plain agent for the harness beats", and that's what shipped. The debate agents only get tools that need no approval. Anything that needs your permission runs on the standalone harness agent.

Flow in the UI:

1. Invite agents, pick handoff or group chat, drop a topic. The council debates.
2. Press **🛠️ Pragmatist, act**. The hub sends the harness *the debate transcript it hasn't seen yet* plus your instruction.
3. The Pragmatist plans todos (plan mode) and you confirm. It switches to execute and starts writing files. **Every write stops for your approval.**
4. Files appear in the workspace pane. Ask the Skeptic to rebut them: it reads the file inside the debate.

---

## 4. Letting the user choose the council

**Code:** `Council.razor` (roster toggles), `CouncilSessionManager.StartAsync`, `CouncilWorkflow`.

The workflows used to be built once for all four agents. Now they are built **per session** for the invited line-up:

```csharp
// CouncilWorkflow.cs
public Workflow BuildHandoff(IReadOnlyList<AIAgent> personas) => AgentWorkflowBuilder
    .CreateHandoffBuilderWith(agents.Moderator)
    .WithHandoffs(agents.Moderator, personas)
    .WithHandoffs(personas, agents.Moderator)
    .EnableReturnToPrevious()
    .Build();

public Workflow BuildGroupChat(IReadOnlyList<AIAgent> members, int roundsPerAgent) => AgentWorkflowBuilder
    .CreateGroupChatBuilderWith(participants => new RoundRobinGroupChatManager(participants)
    {
        MaximumIterationCount = roundsPerAgent * participants.Count, // was "* 4"
    })
    .AddParticipants([.. members])
    .Build();
```

Rules, enforced **on the server** (trust boundary) and mirrored in the UI (UX):

- **Handoff** always seats the Moderator: it's the start agent and the hub of the star.
- At least **one persona**, and at least **two agents** in total.

```csharp
// CouncilSessionManager.StartAsync
bool withModerator = !groupChat || agentNames.Contains(CouncilAgents.ModeratorName, StringComparer.OrdinalIgnoreCase);
if (personas.Count == 0 || personas.Count + (withModerator ? 1 : 0) < 2)
{
    throw new ArgumentException("Invite at least one persona, and at least two agents in total.");
}
```

The hub catches that `ArgumentException` and sends `CouncilError` instead of letting it become a `HubException`, which would surface as an unhandled exception in the Blazor circuit.

One subtle change: the Moderator's instructions used to name "Optimist, Skeptic, Pragmatist". With a subset, it would try to hand off to someone absent. It now says *"hand the floor to one of the personas you can hand off to"*, and the handoff tools the framework injects only list the invited agents.

On the stage, seats are rendered only for invited agents, so the orb position is computed from the seat count instead of a hard-coded 20% step:

```csharp
// (index + .5) / count — the centre of the active seat. Invariant culture, or "12,5%" breaks CSS in some locales.
return string.Create(CultureInfo.InvariantCulture,
    $"top:{(index + .5) * 100 / Math.Max(1, seats.Count):0.##}%; --orb-color:var(--{css})");
```

---

## 5. File access: giving agents hands

**Code:** `CouncilAgents.cs`.

`FileAccessProvider` exposes seven tools over an `AgentFileStore`:

| Tool | Kind |
|---|---|
| `file_access_read`, `file_access_ls`, `file_access_grep` | read-only |
| `file_access_write`, `file_access_delete`, `file_access_replace`, `file_access_replace_lines` | modify |

**By default every tool requires approval.** Each one is an `ApprovalRequiredAIFunction`. Three switches change that:

```csharp
new FileAccessProviderOptions
{
    DisableWriteTools = true,             // hide the 4 modifying tools entirely
    DisableReadOnlyToolApproval = true,   // read/ls/grep run without asking
    DisableWriteToolApproval = true,      // writes run without asking (we never set this)
};
```

The two agents use it differently:

```csharp
// Skeptic (inside the debate workflow): read-only, no approvals at all.
new FileAccessProvider(Store, new FileAccessProviderOptions
{
    DisableWriteTools = true,
    DisableReadOnlyToolApproval = true,
}),

// Harness Pragmatist: reads free, every write surfaces an approval request.
new FileAccessProvider(Store, new FileAccessProviderOptions { DisableReadOnlyToolApproval = true }),
```

Both share one store over `<content root>/workspace` (git-ignored):

```csharp
WorkspaceRoot = Path.Combine(environment.ContentRootPath, "workspace");
Directory.CreateDirectory(WorkspaceRoot);
Store = new FileSystemAgentFileStore(WorkspaceRoot);
```

**Security point worth saying out loud:** `FileSystemAgentFileStore` resolves every path against the root and rejects `..` escapes and symlinks. The UI's file preview reuses the store for exactly that reason, so a user-supplied path can't read outside the workspace:

```csharp
// CouncilHub.ReadWorkspaceFile — the client sends the path, the store validates it.
return await agents.Store.ReadAsync(path, Context.ConnectionAborted);
```

> Because the agents are singletons, the workspace is **shared by every connection**. That's fine for a demo. `InMemoryAgentFileStore` per session is the upgrade.

### Standalone sample: an agent that can write files

```csharp
var store = new FileSystemAgentFileStore(Path.Combine(Environment.CurrentDirectory, "ws"));
AIAgent writer = new ChatClientAgent(chatClient, new ChatClientAgentOptions
{
    Name = "Writer",
    ChatOptions = new() { Instructions = "Write the requested file with file_access_write." },
    AIContextProviders = [new FileAccessProvider(store, new()
    {
        DisableReadOnlyToolApproval = true,
        DisableWriteToolApproval = true,   // no approval yet — see §6
    })],
});

AgentSession session = await writer.CreateSessionAsync();
Console.WriteLine(await writer.RunAsync("Write hello.md saying hi.", session));
```

---

## 6. The approval gate ⭐

**Code:** `CouncilAgents.Harness`, `CouncilHub.RunHarnessAsync` / `RespondToApproval`, the modal in `Council.razor`.

### 6a. The anatomy of a permission prompt

Four pieces, all built in:

1. **Request content.** When the model calls an approval-required tool, the function-invoking chat client **doesn't run it**. Instead a `ToolApprovalRequestContent` appears in the response stream, carrying `RequestId` and `ToolCall` (a `FunctionCallContent` with `Name` + `Arguments`).
2. **Response content.** The caller answers with `request.CreateResponse(approved, reason)` inside a **new** run, and the tool actually executes then.
3. **"Don't ask again".** `request.CreateAlwaysApproveToolResponse()` wraps the response. `ToolApprovalAgent` unwraps it, stores a `ToolApprovalRule` in the **session** state bag, and auto-approves matching calls from then on. You write no rule-persistence code. (`CreateAlwaysApproveToolWithArgumentsResponse` scopes the rule to identical arguments.)
4. **A queue.** If the model asks for several approvals at once, `ToolApprovalAgent` surfaces them **one at a time**. `MaxAutoApprovalIterations` caps runaway auto-approved loops (billable model calls!).

### 6b. Wiring it

```csharp
Harness = new LoopAgent(
    new AIAgentBuilder(pragmatistWithHands).UseToolApproval().Build(),
    ...);
```

### 6c. The round-trip through SignalR

The key realisation is that **a pending approval ends the run.** The stream completes while the request is pending, so the hub parks the request on the session and returns. The user's answer arrives later as a *separate* hub call, which starts a *new* run:

```csharp
// CouncilHub.RunHarnessAsync — stream, park the request, then tell the client
await foreach (AgentResponseUpdate update in agents.Harness.RunStreamingAsync(messages, session.HarnessSession, cancellationToken: ct))
{
    foreach (AIContent content in update.Contents)
    {
        if (content is ToolApprovalRequestContent request)
        {
            session.PendingApproval = request;            // parked; the stream will now complete
        }
        else if (DisplayText(content) is { Length: > 0 } text)
        {
            await Clients.Caller.SendAsync("AgentDelta", CouncilAgents.PragmatistName, text, ct);
        }
    }
}

if (session.PendingApproval is { ToolCall: FunctionCallContent call })
{
    await Clients.Caller.SendAsync("ApprovalRequested", "Pragmatist", call.Name,
        JsonSerializer.Serialize(call.Arguments, IndentedJson), ct);
}
else
{
    await Clients.Caller.SendAsync("AwaitUserInput", ct);
}
```

```csharp
// CouncilHub.RespondToApproval — the answer is a NEW run carrying the response content
AIContent response = decision switch
{
    "always"  => request.CreateAlwaysApproveToolResponse(),
    "approve" => request.CreateResponse(true),
    _         => request.CreateResponse(false, "The user denied this action."),
};
await RunHarnessAsync(session, [new ChatMessage(ChatRole.User, [response])]);
```

**Why not `await` the user's answer inside the streaming call?** SignalR runs **one invocation per client at a time** (`HubOptions.MaximumParallelInvocationsPerClient` defaults to 1). If `RunHarnessAsync` waited for `RespondToApproval`, that second call would sit in the queue behind the first: a deadlock. The same default is also the hub's only turn lock, so **don't raise it**.

The JSON for the dialog uses `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, so "6–8 weeks" doesn't show as `6–8`. "Unsafe" only means "not safe to paste into HTML/JS". Blazor renders the text encoded, so it's fine here.

### 6d. The UI side

```razor
@if (_approval is not null)
{
    <div class="modal-backdrop">
        <div class="modal" role="dialog" aria-modal="true" aria-labelledby="approval-title">
            <h2 id="approval-title">🔐 @_approval.Agent wants to run <code>@_approval.Tool</code></h2>
            <pre>@_approval.Arguments</pre>
            <div class="modal-actions">
                <button class="btn danger"    @onclick="@(() => RespondAsync("deny"))">Deny</button>
                <button class="btn secondary" @onclick="@(() => RespondAsync("always"))">Always approve</button>
                <button class="btn"           @onclick="@(() => RespondAsync("approve"))">Approve</button>
            </div>
        </div>
    </div>
}
```

While a request is pending, the reply box stays disabled (`_awaitingUser == false`). The user can't start another turn with an approval still open.

### Standalone sample: the whole approval loop in a console

This is the loop that was verified live. The second write goes through without a prompt:

```csharp
var store = new FileSystemAgentFileStore("ws");
AIAgent inner = new ChatClientAgent(chatClient, new ChatClientAgentOptions
{
    Name = "Pragmatist",
    ChatOptions = new() { Instructions = "Write files when asked. One-sentence replies." },
    AIContextProviders = [new FileAccessProvider(store, new() { DisableReadOnlyToolApproval = true })],
});
AIAgent agent = new AIAgentBuilder(inner).UseToolApproval().Build();
AgentSession session = await agent.CreateSessionAsync();

IEnumerable<ChatMessage> input = [new(ChatRole.User, "Write a.md, then b.md, 1 line each.")];
while (true)
{
    ToolApprovalRequestContent? request = null;
    await foreach (var u in agent.RunStreamingAsync(input, session))
    {
        Console.Write(u.Text);
        request ??= u.Contents.OfType<ToolApprovalRequestContent>().FirstOrDefault();
    }

    if (request is null) break;                                   // done — no pending approval

    var call = (FunctionCallContent)request.ToolCall;
    Console.Write($"\nApprove {call.Name}? [y/a/n] ");
    AIContent answer = Console.ReadLine() switch
    {
        "a" => request.CreateAlwaysApproveToolResponse(),         // won't ask again this session
        "y" => request.CreateResponse(true),
        _   => request.CreateResponse(false, "Denied."),
    };
    input = [new(ChatRole.User, [answer])];                       // the answer is the next run's input
}
```

---

## 7. Plan mode, todos and the loop

**Code:** `CouncilAgents.cs` (harness construction and instructions).

### 7a. `AgentModeProvider`: plan vs execute

It needs no configuration: the defaults are `plan` (interactive) and `execute` (autonomous). It gives the model `mode_set` / `mode_get` and injects mode-specific instructions each call. The host can use `GetModeAsync` / `SetModeAsync`.

Two things the docs don't make obvious:

- **It doesn't gate tools.** Plan mode only *tells* the model not to act. Here the real guard is the approval gate: even if the model writes in plan mode, you still get asked.
- **Mode is per session of the agent it's attached to.** A mode provider on the Moderator wouldn't affect the Pragmatist. That's another reason the harness is its own agent with its own session.

Custom modes are just names plus instructions:

```csharp
new AgentModeProvider(new AgentModeProviderOptions
{
    Modes = [new("research", "Only gather facts."), new("draft", "Write files.")],
    DefaultMode = "research",
});
```

### 7b. `TodoProvider`

It provides the tools `todos_add`, `todos_complete`, `todos_remove`, `todos_get_remaining` and `todos_get_all`, and injects a synthetic "current todo list" message on each call (`SuppressTodoListMessage` turns that off). The host reads the list with `GetAllTodosAsync(session)`. The returned items are **live objects**, so project them immediately:

```csharp
TodoView[] todos = (await agents.HarnessTodos.GetAllTodosAsync(session.HarnessSession, ct))
    .Select(t => new TodoView(t.Title, t.IsComplete))
    .ToArray();
```

### 7c. `LoopAgent` + `TodoCompletionLoopEvaluator`: "keep going until done"

```csharp
Harness = new LoopAgent(
    new AIAgentBuilder(pragmatistWithHands).UseToolApproval().Build(),
    new TodoCompletionLoopEvaluator(new TodoCompletionLoopEvaluatorOptions { Modes = ["execute"] }),
    new LoopAgentOptions { MaxIterations = 12, ExcludeOnBehalfOfMessages = true });
```

How it behaves:

- After each run of the inner agent, the **evaluator** decides whether to go again. `TodoCompletionLoopEvaluator` continues while incomplete todos remain, and feeds the remaining list back as the next input.
- **It finds the `TodoProvider` by itself**, via `GetService` on the looped agent. `DelegatingAIAgent` forwards `GetService` down to the `ChatClientAgent`, which exposes its providers. No wiring needed.
- `Modes = ["execute"]` means **only loop in execute mode.** In plan mode the Pragmatist proposes todos and stops to ask you. Once you confirm and it calls `mode_set execute`, the loop drains the list.
- **A pending approval stops the loop.** When you answer, the next run resumes and the loop continues. That's why "approve → write → next todo → next approval" chains naturally.
- `MaxIterations = 12` is the "don't burn $400 overnight" cap. Say the number out loud.
- `ExcludeOnBehalfOfMessages = true` keeps the loop's injected feedback ("these todos remain…") out of the streamed output, so it doesn't show up in the transcript as if someone had typed it.
- `FreshContextPerIteration` (default `false`) is the most interesting knob. `false` appends feedback to the conversation. `true` restarts each iteration from the original input plus a feedback log. It's the compaction question in another form.

**The pipeline order matters:**

```
LoopAgent                ← decides "again?" after each inner run; stops on pending approval
 └─ ToolApprovalAgent    ← queues approval requests, applies "always" rules
     └─ ChatClientAgent  ← providers: FileAccess · Todo · Mode · BackgroundAgents
```

If the order is flipped, the loop would sit *inside* the approval middleware and re-invoke the agent without ever surfacing the prompt to you.

### How the harness sees the debate

The harness has its own session, so it never saw the debate. The hub keeps a running transcript of what the harness **hasn't** been shown yet, and prepends it to your instruction:

```csharp
// CouncilHub.StreamTurnAsync — while forwarding debate deltas
if (agent != session.LastSpeaker)
{
    session.UnseenDebate.Append("\n\n").Append(agent).Append(": ");
    session.LastSpeaker = agent;
}
session.UnseenDebate.Append(text);

// CouncilHub.AskPragmatist
if (session.UnseenDebate.Length > 0)
{
    prompt.Append("Council debate since you last acted:").Append(session.UnseenDebate).Append("\n\n");
    session.UnseenDebate.Clear();
    session.LastSpeaker = null;
}
prompt.Append("User: ").Append(instruction);
```

Only the *delta* is sent, and the harness session keeps everything it saw before, so nothing is resent.

---

## 8. Skills: SKILL.md as a first-class type

**Code:** `src/AgentCouncil.Agents/skills/devils-advocate/SKILL.md` and the Skeptic's providers.

A skill is a folder with a `SKILL.md`: YAML frontmatter plus a markdown body. It's the same spec Claude Code uses:

```markdown
---
name: devils-advocate
description: Stress-test a proposal the council is converging on by naming the one assumption that, if false, sinks it.
---

# Devil's advocate
1. Find the load-bearing assumption...
2. Give the most concrete, plausible scenario in which that assumption is false.
3. Name the cheapest test that would tell the council which world it is in.
```

```csharp
new AgentSkillsProvider(
    Path.Combine(environment.ContentRootPath, "skills"),   // content root, not CWD: same under Aspire and dotnet run
    options: new AgentSkillsProviderOptions
    {
        DisableLoadSkillApproval = true,          // these also default to "approval required"
        DisableReadSkillResourceApproval = true,  // and approvals can't surface inside a workflow (§12)
    }),
```

It works through **progressive disclosure**:

1. **Advertise:** only names and descriptions go into the system prompt, which is cheap.
2. **Load:** the model calls `load_skill` to pull the full body when it decides the skill fits.
3. **Resources and scripts:** `read_skill_resource` and `run_skill_script` (scripts need a runner and deserve approval).

Verified live: in a group chat, the Skeptic called `load_skill` and answered in the skill's exact shape (load-bearing assumption → failure scenario → cheapest test). To add a capability, drop another folder in `skills/` and restart. No code change.

---

## 9. Subagents: `BackgroundAgentsProvider`

**Code:** `CouncilAgents.HarnessBackground`, `CouncilSessionManager.EndAsync`.

```csharp
HarnessBackground = new BackgroundAgentsProvider(
    [new ChatClientAgent(cheapClient, instructions: "…3 concise bullet points…",
        name: "Researcher", description: "Gathers facts, precedents and figures on a focused question.")],
    new BackgroundAgentsProviderOptions());
```

The parent gets these tools:

- `background_agents_start_task`
- `background_agents_wait_for_first_completion`
- `background_agents_get_task_results`
- `background_agents_get_all_tasks`
- `background_agents_continue_task`
- `background_agents_clear_completed_task`

Each task runs **concurrently in its own session**. The *description* you give the subagent is what the parent reads to decide when to delegate, so write it like a job ad.

The rule you must not skip: **background tasks keep calling the model after the user leaves.** Release the session when the connection ends:

```csharp
// CouncilSessionManager.EndAsync (called from OnDisconnectedAsync)
await agents.HarnessBackground.ReleaseSessionAsync(session.HarnessSession, cancelRunning: true);
```

The UI shows `🔎 researching · n` from `GetIncompleteTasks(session).Count`, pushed after every debate turn and harness run.

The type docs carry a security note worth repeating: a subagent receives whatever text the parent sends it, and its output flows back into the parent's context. Only give it agents you trust.

---

## 10. The bill: two models and compaction

### 10a. One `IChatClient` per cost tier

`RoutePersistingRoutingChatClient` sounds right but routes **per session**, not per agent, so it can't put the Moderator and the personas on different models inside one council. The simple, correct version is **two clients**:

```csharp
// AzureOpenAIChatClient.AddCouncilChatClient
AzureOpenAIClient azure = new(new Uri(endpoint), new ApiKeyCredential(key));

builder.Services.AddChatClient(_ => azure.GetChatClient(deployment).AsIChatClient())
    .UseOpenTelemetry(sourceName: ActivitySourceName, configure: o => o.EnableSensitiveData = enableSensitiveData);

builder.Services.AddKeyedChatClient(CheapClientKey, _ => azure.GetChatClient(cheapDeployment ?? deployment).AsIChatClient())
    .UseOpenTelemetry(sourceName: ActivitySourceName, configure: o => o.EnableSensitiveData = enableSensitiveData);
```

```csharp
// CouncilAgents constructor — DI resolves the keyed one by attribute
public CouncilAgents(
    IChatClient chatClient,
    [FromKeyedServices(AzureOpenAIChatClient.CheapClientKey)] IChatClient cheapClient,
    IHostEnvironment environment)
```

| Runs on `Deployment` (smart) | Runs on `CheapDeployment` |
|---|---|
| Moderator, harness Pragmatist | Optimist, Skeptic, debate Pragmatist, Researcher, compaction summaries |

`CheapDeployment` is optional in the connection string. Without it, both clients use `Deployment`:

```
Endpoint=https://<resource>.openai.azure.com/;Key=<key>;Deployment=gpt-5;CheapDeployment=gpt-5-mini
```

Because both clients are wrapped in `UseOpenTelemetry`, the Aspire dashboard shows the token split by model.

### 10b. `CompactionProvider`

Compaction runs as a provider **before each invocation**. It groups messages atomically (a tool call and its result are never split) and replaces the old ones with a summary:

```csharp
private static CompactionProvider Compaction(IChatClient summarizer) =>
    new(new SummarizationCompactionStrategy(summarizer, CompactionTriggers.TokensExceed(6000)));
```

- **Triggers** compose: `TokensExceed`, `MessagesExceed`, `TurnsExceed`, `GroupsExceed`, `HasToolCalls`, `All(...)`, `Any(...)`.
- **Minimum preserved groups** (default 8) is a hard floor: recent turns are never summarised.
- **Why it matters here:** in a workflow, each agent keeps its *own* copy of the conversation. Four agents means four growing histories, so unbounded growth is the first thing that breaks.
- If several agents with compaction ever **share** a session, give each provider a distinct `stateKey`.
- **Security:** the summary *replaces* history permanently, so the summariser must be as trusted as the main model.

Compaction sits on the **four debaters only** and deliberately **not on the harness**. See §12c for why.

---

## 11. Streaming the harness to the UI

### Tool calls as visible notes

A harness demo is only convincing if you can *see* the tools being used. The hub turns function calls into small inline notes in the same streamed text (the handoff plumbing is skipped):

```csharp
private static string? DisplayText(AIContent content) => content switch
{
    TextContent text => text.Text,
    FunctionCallContent call when !call.Name.StartsWith("handoff_to", StringComparison.Ordinal) =>
        call.Arguments?.TryGetValue("fileName", out object? file) == true
            ? $"\n\n`🔧 {call.Name} {file}`\n\n"
            : $"\n\n`🔧 {call.Name}`\n\n",
    _ => null,
};
```

The transcript then reads like a harness log:

```
🔧 mode_set
🔧 todos_add
🔧 file_access_write decision-record.md
🔧 todos_complete
🔧 file_access_write pilot-metrics.md
🔧 todos_complete
🔧 mode_set
I wrote decision-record.md and pilot-metrics.md from the debate.
```

Model output is rendered with Markdig using `DisableHtml()`, so inline code renders as a chip and raw HTML from the model is never injected.

### Hub protocol reference

| Direction | Message | Purpose |
|---|---|---|
| → server | `StartCouncil(topic, mode, roundsPerAgent, agentNames[])` | Invited line-up; `mode` = `handoff` \| `groupchat` |
| → server | `SendUserInput(text)` | Continue the debate |
| → server | `AskPragmatist(instruction)` | Run the harness with the unseen debate + instruction |
| → server | `RespondToApproval(decision)` | `approve` \| `always` \| `deny` |
| → server | `ReadWorkspaceFile(path)` → `string?` | File preview (path validated by the store) |
| ← client | `AgentDelta(agent, text)` | Streamed text or a tool note |
| ← client | `AwaitUserInput()` | The floor is back with the user |
| ← client | `CouncilError(message)` | Validation, workflow or harness errors |
| ← client | `ApprovalRequested(agent, tool, argsJson)` | Open the modal |
| ← client | `HarnessState(mode, TodoView[], researching)` | Mode chip, todo panel, research badge |
| ← client | `FilesChanged(paths[])` | Workspace pane |

`TodoView` is a one-line record defined in *both* projects. For one record, duplicating it is simpler than adding a shared contracts project. SignalR matches the JSON by property name.

### The graph for slides

```csharp
// Program.cs (Development only)
app.MapGet("/council/graph", (CouncilAgents agents, CouncilWorkflow council) =>
    WorkflowVisualizer.ToMermaidString(council.BuildHandoff(agents.Personas)));
```

`GET https://localhost:7150/council/graph` returns a `flowchart TD` you can paste into any Mermaid renderer.

---

## 12. What didn't work — verified the hard way

The plan marked several things "(unproven)". Throwaway console spikes ran them against the live Azure deployment. These are the results, so you don't have to rediscover them.

### 12a. Tool approvals can't cross a workflow boundary (1.19)

The idea: host the handoff workflow as an agent (`workflow.AsAIAgent()`) and let approvals surface from inner agents. The **request does surface**: `ToolApprovalRequestContent` shows up in the stream and the run completes. **Resuming** is what failed, in every configuration tried:

| Attempt | Result |
|---|---|
| `UseToolApproval()` around the workflow-hosting agent | The write actually ran, then the workflow died: *"Expected exactly one update for key 'SharedState'"*. Every later turn failed the same way. |
| `UseToolApproval()` around the inner persona | Two requests surfaced (inner + workflow-facing IDs); resuming errored immediately |
| Raw `CreateResponse(true)` to the hosting agent (no middleware) | The response was routed to the Moderator as a normal message: *"Requested function 'file_access_write' not found"*. Next turn: *"Cannot have multiple simultaneous conversations in Handoff Orchestration."* |
| Plain `StreamingRun`: `RequestInfoEvent` → `SendResponseAsync(...)` (+ `TurnToken`) | The request surfaced, but after the response the stream just ended. Nothing ran and the run stayed stuck. |

**Lesson:** inside workflows, only give agents tools that need no approval. Put approval-gated work on a standalone agent whose session you own.

### 12b. A group chat hosted as an agent forgets between runs

`BuildGroupChat(...).AsAIAgent()` with two `RunStreamingAsync` calls on the same session: turn 1 said *"the secret code word is PINEAPPLE"*, and in turn 2 all four agents answered **UNKNOWN**. The round-robin workflow terminates each run, and a new run starts with no history.

**Lesson:** keep the per-turn history reseed for group chat (`WorkflowOutputEvent` → `History`). Handoff, by contrast, *did* keep history across runs as an agent, and `AuthorName` survived intact.

### 12c. `CompactionProvider` + `TodoProvider` breaks approvals

This was the nastiest one, because it worked in a small spike and failed in the real app.

**Symptom:** click Approve → no file. The model called `mode_set` again, then asked for a *new* approval, sometimes after 40–111 `mode_set` calls in a row. With the loop disabled, the error sometimes surfaced as *"ToolApprovalRequestContent found with FunctionCall.CallId(s) '…' that have no matching ToolApprovalResponseContent"*, thrown from `FunctionInvokingChatClient`.

**How it was found**, by bisection. Each variant removed one piece and ran the app's exact shape (a planning turn, then the approved write):

| Variant | Approved write executed? |
|---|---|
| full harness | ❌ |
| no `CompactionProvider` | ✅ |
| no `TodoProvider` | ✅ |
| no `BackgroundAgentsProvider` | ❌ |
| no `LoopAgent` | ❌ (so the loop isn't the cause) |

**Conclusion:** with compaction and the todo provider (which injects a message every call) on the same agent, the approval request and response no longer match up. The approved call is never run, and the model replays its last step.

**Fix:** no compaction on the harness agent. Another hypothesis, `AllowMultipleToolCalls = false`, was tested and **didn't help**, so it was reverted.

**Lesson:** one working spike isn't proof. Test the exact composition you ship, and bisect by removing one piece at a time.

### 12d. Smaller things worth knowing

- **Pending approval = completed stream.** That's what makes the SignalR round-trip possible without holding a hub call open.
- **Azure content filter.** One spike variant that called `AgentModeProvider.SetModeAsync(...)` before the first run got `HTTP 400 content_filter` on the prompt. That's *suspected*, not proven, to be the injected "mode changed externally" notification. Letting the model call `mode_set` itself avoided it.
- **429s.** Four harness processes in parallel, each able to loop, hit `rate_limit_exceeded`. `MaxIterations` is a cost cap *and* a rate-limit cap.
- **A repeated closing sentence** ("I've planned… I've planned…") came from the model, not the code. A dump of every streamed update showed the text arriving once under a single message ID.
- **`AsAIAgent` signature:** the third positional argument of `RunStreamingAsync` is `AgentRunOptions`, so pass the token by name: `cancellationToken: ct`.
- **`WithCheckpointing` is order-sensitive** (relevant for the unbuilt Phase 7). It silently no-ops once the workflow host sits behind middleware, so call it *before* wrapping.

---

## 13. Try it yourself

Small experiments, each a few lines, to build intuition:

1. **Watch the queue.** Ask the Pragmatist for three files in one sentence. Approvals arrive one at a time: that's `ToolApprovalAgent`'s queue.
2. **Scope "always".** Swap `CreateAlwaysApproveToolResponse()` for `CreateAlwaysApproveToolWithArgumentsResponse()` in `RespondToApproval`. Now only an identical call skips the prompt.
3. **Deny on purpose.** Its instructions say not to retry a denied write, so see what it asks instead.
4. **Add a skill.** Create `skills/steelman/SKILL.md` for the Optimist (add the provider to the Optimist too), restart and ask for the strongest opposing case.
5. **Flip `FreshContextPerIteration = true`** on the loop and compare the transcript and token counts in the Aspire dashboard.
6. **Break it safely.** Put `Compaction(cheapClient)` back on the harness and reproduce §12c yourself.
7. **Read state from the host.** Add a hub method that calls `HarnessModes.SetModeAsync(session, "plan")` and a "back to plan" button. The model is told about the external change on its next run.

---

## 14. What's not built yet

- **Phase 7, crash recovery** (`WithCheckpointing` + `WorkflowSessionCheckpointRecovery.TryPrepare`). It needs the debate hosted as an agent (a no-go for group chat, §12b), sessions serialised to disk, and a stable council ID that survives reconnects. None of that is proven yet.
- **Replacing group chat's fixed round count with a todo loop.** `TodoCompletionLoopEvaluator` finds the todo provider via `GetService` on the *looped* agent. A workflow host can't expose providers attached to agents inside the workflow, so the loop drives the standalone harness instead.

---

## Dig deeper

- [`ai-plans/option-b-implementation-phases.md`](../ai-plans/option-b-implementation-phases.md): the phased plan this implements (local; `ai-plans/` is git-ignored)
- [AGENT-FRAMEWORK.md](AGENT-FRAMEWORK.md): agents, workflows, transport and hosting basics
- [Microsoft Agent Framework on GitHub](https://github.com/microsoft/agent-framework): source and samples
- [Agent Skills specification](https://agentskills.io/): the SKILL.md format
- [Microsoft.Extensions.AI](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai): `IChatClient`, function invocation, approval content types
- XML docs in your NuGet cache: `~/.nuget/packages/microsoft.agents.ai/1.19.0/lib/net10.0/Microsoft.Agents.AI.xml` is where most of this was verified, and it's more complete than the web docs for these new types
