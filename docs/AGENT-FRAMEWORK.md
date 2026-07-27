# Agents & the Microsoft Agent Framework — how this project works

A guided tour of the concepts behind AgentCouncil and exactly where each one lives in the code. Read top-to-bottom once and you'll know how the whole thing fits together.

---

## 1. What is an "agent"?

An **AI agent** is an LLM wrapped with an identity: a **name**, **instructions** (its persona/system prompt), and optionally **tools** it may call. Instead of one prompt answering everything, you compose several narrow agents and let them collaborate.

The **Microsoft Agent Framework** (MAF) is the .NET/Python/Go SDK that provides:

- `AIAgent` / `ChatClientAgent` — an agent over any `IChatClient` (Azure OpenAI here).
- **Workflows** — graphs that connect agents into orchestration patterns (handoff, group chat, sequential, concurrent, Magentic).
- **DevUI** — a built-in web debugger to chat with agents and inspect traces.
- **OpenTelemetry** integration — every LLM/agent call becomes a trace span.

> MAF is the successor that unifies Semantic Kernel's agents and AutoGen's multi-agent patterns.

## 2. The cast: four `ChatClientAgent`s

**Code: [`src/AgentCouncil.Agents/Agents/CouncilAgents.cs`](../src/AgentCouncil.Agents/Agents/CouncilAgents.cs)**

```csharp
Moderator = new ChatClientAgent(chatClient, instructions: "...", name: "Moderator", description: "...");
```

| Agent | Role |
|---|---|
| **Moderator** | Frames the topic, gives the floor to a persona, decides when to ask the user |
| **Optimist** | Argues the upside with concrete benefits |
| **Skeptic** | Surfaces the sharpest risk or hidden cost |
| **Pragmatist** | Weighs the trade-off, proposes one next step |

All four share **one `IChatClient`** (created in [`AzureOpenAIChatClient.cs`](../src/AgentCouncil.Agents/AzureOpenAIChatClient.cs) from the `openai` connection string, wrapped with `UseOpenTelemetry`). The persona is *entirely* in the instructions — same model, different system prompt. Every instruction ends with a hard **2-sentence cap** to keep the debate sharp.

`CouncilAgents` is a **singleton**: agents are stateless between calls (conversation state lives in the workflow run), so one instance safely serves every session *and* DevUI.

## 3. Orchestration: making agents talk to each other

A single agent answers; multiple agents need an **orchestration pattern** that decides *who speaks next*. MAF models these as **Workflows**: a graph of executors (here: agents) built by `AgentWorkflowBuilder`.

This project implements **two patterns, selectable per session** in the UI.

**Code: [`src/AgentCouncil.Agents/Workflow/CouncilWorkflow.cs`](../src/AgentCouncil.Agents/Workflow/CouncilWorkflow.cs)**

### 3a. Handoff (default)

The *speaking agent itself* decides who goes next by invoking a handoff tool the framework injects.

```csharp
AgentWorkflowBuilder
    .CreateHandoffBuilderWith(agents.Moderator)      // Moderator opens
    .WithHandoffs(agents.Moderator, agents.Personas) // Moderator → any persona
    .WithHandoffs(agents.Personas, agents.Moderator) // every persona → back to Moderator
    .EnableReturnToPrevious()
    .Build();
```

Topology: a **star with the Moderator in the middle**. The debate length is *emergent* — the Moderator ends a turn by simply *not* handing off, which is the "ask the user" moment.

### 3b. Group chat (round-robin, configurable)

A central **manager** (not the agents) picks speakers. We use the built-in `RoundRobinGroupChatManager`: everyone speaks in fixed order, Moderator first.

```csharp
AgentWorkflowBuilder
    .CreateGroupChatBuilderWith(participants => new RoundRobinGroupChatManager(participants)
    {
        MaximumIterationCount = roundsPerAgent * agents.All.Count, // rounds × 4
    })
    .AddParticipants([.. agents.All])
    .Build();
```

The UI's **"turns per agent"** setting becomes `MaximumIterationCount`: choose 3 and each of the four agents speaks exactly 3 times, then the workflow **terminates** and emits the full conversation. Deterministic, budget-bound — the opposite trade-off from handoff.

| | Handoff | Group chat |
|---|---|---|
| Who picks the next speaker | The current agent (LLM decision) | The manager (round-robin code) |
| Turn length | Emergent | Fixed: rounds × agents |
| Workflow lifetime | One run persists across user turns | Run terminates each turn; rebuilt per session |

## 4. Running a workflow: streaming runs, turn tokens, events

**Code: [`CouncilHub.cs`](../src/AgentCouncil.Agents/Hubs/CouncilHub.cs) + [`CouncilSessionManager.cs`](../src/AgentCouncil.Agents/Hubs/CouncilSessionManager.cs)**

A built `Workflow` is inert; you execute it with `InProcessExecution`, getting a `StreamingRun`:

1. Send input: `run.TrySendMessageAsync(new ChatMessage(ChatRole.User, text))`.
2. Send a **`TurnToken(emitEvents: true)`** — agents buffer messages and only act when the token arrives. Forget it and the run idles forever (the classic gotcha).
3. Iterate `run.WatchStreamAsync()` and switch on the events:
   - `AgentResponseUpdateEvent` — a streamed text delta + `AuthorName` → forwarded to the browser as `AgentDelta(agent, text)`.
   - `WorkflowOutputEvent` — group chat only: the final `List<ChatMessage>` transcript.
   - `WorkflowErrorEvent` — forwarded as `CouncilError`.
4. When the stream completes, the floor is back with the user → `AwaitUserInput()`.

### Conversation state — the key difference between the modes

- **Handoff**: the `StreamingRun` itself holds the history, so the session keeps **one run alive** across user turns and just keeps feeding it messages.
- **Group chat**: the run *terminates* after its round budget, so the session keeps a `List<ChatMessage>` history instead, and each user turn **opens a fresh run seeded with that history** (reseeded from the `WorkflowOutputEvent` transcript).

Both live in `CouncilSession`, one per SignalR connection, dict-keyed by connection id.

## 5. Transport: SignalR → Blazor

**Code: [`src/AgentCouncil.Web/Services/CouncilClient.cs`](../src/AgentCouncil.Web/Services/CouncilClient.cs) + [`Components/Pages/Council.razor`](../src/AgentCouncil.Web/Components/Pages/Council.razor)**

```
Browser ── Blazor Server circuit ── CouncilClient (HubConnection)
    └→ hub methods:  StartCouncil(topic, mode, roundsPerAgent) · SendUserInput(text)
    ←─ hub callbacks: AgentDelta(agent, text) · AwaitUserInput() · CouncilError(msg)
```

The hub URL comes from **Aspire service discovery** (`services:agents:https:0`) — the web app never hardcodes the agents service address. The Razor page accumulates `AgentDelta` chunks into per-agent transcript bubbles; a change of `AuthorName` is what drives the animated stage (the glowing "floor token" sliding between seats). The UI needs no extra server events — speaker identity rides on the deltas.

## 6. Hosting: Aspire, DevUI, telemetry

**Code: [`Program.cs`](../src/AgentCouncil.Agents/Program.cs) + [`src/AgentCouncil.AppHost`](../src/AgentCouncil.AppHost)**

- **Aspire AppHost** wires the `openai` connection string, the `agents` API, and the `web` front-end, and gives you the dashboard.
- **DevUI** (Development only): `AddDevUI()` + `AddAIAgent(...)` per agent, then `MapOpenAIResponses() → MapOpenAIConversations() → MapDevUI()` — *in that order*. Browse `/devui` to chat with any single agent outside the workflow.
- **Telemetry**: the chat client's `UseOpenTelemetry` plus tracing sources (`Microsoft.Extensions.AI`, `*Microsoft.Agents.AI*`) mean every LLM call and handoff shows up as spans in the Aspire dashboard.

## 7. Mental model in one paragraph

Four stateless personas over one Azure OpenAI client are composed into a graph — either a Moderator-centred handoff star where the LLM routes the conversation, or a round-robin group chat where code routes it under a fixed round budget. A SignalR connection owns one session (a live run, or a history that reseeds fresh runs), turn tokens gate each debate round, and streamed `AgentResponseUpdateEvent`s fan out to the Blazor UI as per-agent deltas that drive both the transcript and the stage animation.

---

## Dig deeper

**Microsoft Agent Framework**
- [Overview](https://learn.microsoft.com/en-us/agent-framework/overview/) — what MAF is, how it relates to Semantic Kernel / AutoGen
- [Agents quickstart (C#)](https://learn.microsoft.com/en-us/agent-framework/tutorials/quick-start?pivots=programming-language-csharp)
- [Workflows overview](https://learn.microsoft.com/en-us/agent-framework/workflows/overview)
- [Handoff orchestration](https://learn.microsoft.com/en-us/agent-framework/workflows/orchestrations/handoff?pivots=programming-language-csharp) — pattern used by default here
- [Group chat orchestration](https://learn.microsoft.com/en-us/agent-framework/workflows/orchestrations/group-chat?pivots=programming-language-csharp) — includes custom managers (e.g. approval-based termination)
- [Other orchestrations: sequential, concurrent, Magentic](https://learn.microsoft.com/en-us/agent-framework/workflows/orchestrations/)
- [GitHub: microsoft/agent-framework](https://github.com/microsoft/agent-framework) — source + samples

**Building blocks**
- [Microsoft.Extensions.AI (`IChatClient`)](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)
- [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/get-started/aspire-overview) · [service discovery](https://learn.microsoft.com/en-us/dotnet/aspire/service-discovery/overview)
- [ASP.NET Core SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/introduction)
- [Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/)
