# AgentCouncil

A multi-agent "council" app built on the **Microsoft Agent Framework** (.NET, 1.19) and **.NET Aspire**. A user invites any of four debaters (**Moderator, Optimist, Skeptic, Pragmatist**), drops a topic, and they debate it via a per-session choice of **handoff** or **group chat (round-robin)** orchestration, then return the floor to the user. On request, the **Pragmatist acts**: a standalone *harness* agent plans todos, writes files to a shared workspace behind an **approval gate**, loops until its todo list is empty and can dispatch a background **Researcher**. Backed by **Azure OpenAI** (API-key auth). Visualized through the Agent Framework **DevUI** debugger and a custom **Blazor** front-end.

## Architecture

```
src/AgentCouncil.AppHost          Aspire orchestration: openai connection + agents + web
src/AgentCouncil.ServiceDefaults  Shared telemetry/health/service-discovery (AddServiceDefaults / MapDefaultEndpoints)
src/AgentCouncil.Agents           ASP.NET Core: debaters, harness agent, workflows, DevUI, SignalR hub
src/AgentCouncil.Web              Blazor Server: council UI
test/AgentCouncil.AppHost.Tests   AppHost model test
```

### AgentCouncil.Agents
- **`AzureOpenAIChatClient.cs`** — `AddCouncilChatClient()` parses the single `openai` connection string (`Endpoint=...;Key=...;Deployment=...[;CheapDeployment=...]`) and registers two `IChatClient`s with OpenTelemetry (source `AgentCouncil.Agents`): the default one (Moderator + harness) and a keyed `"cheap"` one (personas, Researcher, compaction summaries). Without `CheapDeployment` both use `Deployment`.
- **`Agents/CouncilAgents.cs`** — singleton holding every agent:
  - The four debaters (reused by the workflows and DevUI). Every one has a `CompactionProvider` (summarization past 6000 tokens). The **Skeptic** also gets a read-only `FileAccessProvider` over the workspace and an `AgentSkillsProvider` over `skills/` (SKILL.md files, e.g. `skills/devils-advocate`).
  - **`Harness`** — the Pragmatist "with hands": `LoopAgent(ToolApprovalAgent(ChatClientAgent))`. Its providers are `FileAccessProvider` (reads free, writes need approval), `TodoProvider`, `AgentModeProvider` (`plan`/`execute`) and `BackgroundAgentsProvider` (Researcher). It deliberately has no compaction (see gotchas). The loop (`TodoCompletionLoopEvaluator`, execute mode only, max 12 iterations) keeps going until the todos are done and stops whenever an approval is pending.
  - The workspace is `FileSystemAgentFileStore` over `<content root>/workspace` (git-ignored), **shared by all connections**.
- **`Workflow/CouncilWorkflow.cs`** — builds the workflows per session for the invited line-up. `BuildHandoff(personas)`: Moderator ↔ each persona, `EnableReturnToPrevious()`. `BuildGroupChat(members, rounds)`: `RoundRobinGroupChatManager` with `MaximumIterationCount = rounds * members.Count`.
- **`Hubs/CouncilHub.cs`** + **`CouncilSessionManager.cs`** — SignalR hub at `/councilhub`. One `CouncilSession` per connection. It holds either the handoff `StreamingRun` (persistent) or the group chat workflow plus a `List<ChatMessage>` history (fresh run per turn, reseeded from `WorkflowOutputEvent`). Alongside that it keeps the harness agent's own `AgentSession`, the debate text the harness hasn't seen, and the pending approval.
  - Server methods: `StartCouncil(topic, mode, roundsPerAgent, agentNames[])` (`mode` = `"handoff"` | `"groupchat"`; handoff always seats the Moderator; at least one persona and two agents), `SendUserInput(text)`, `AskPragmatist(instruction)`, `RespondToApproval(decision)` (`"approve"` | `"always"` | `"deny"`), `ReadWorkspaceFile(path)`, `DeleteWorkspaceFile(path)`.
  - Client-bound: `AgentDelta(agent, text)` (text or a `` `🔧 tool file` `` note), `AwaitUserInput()`, `CouncilError(message)`, `ApprovalRequested(agent, tool, argsJson)`, `HarnessState(mode, TodoView[] todos, researching)`, `FilesChanged(paths[])`.
  - Debater instructions cap every turn at 2 sentences (demo-friendly pacing).
  - `EndAsync` calls `BackgroundAgentsProvider.ReleaseSessionAsync` so Researcher tasks stop when the tab closes.
- **`Program.cs`** — registers everything. **Development-only**: DevUI (`AddDevUI` + `MapOpenAIResponses`/`MapOpenAIConversations`/`MapDevUI`) and `GET /council/graph`, which returns the full-roster handoff graph as Mermaid (`WorkflowVisualizer`).

### AgentCouncil.Web
- **`Services/CouncilClient.cs`** — per-circuit `HubConnection` to the `agents` service. Resolves the hub URL from Aspire service-discovery config (`services:agents:https:0`, falling back to `http`).
- **`Components/Pages/Council.razor`** — two animated start stages. (1) *Who do you want to talk with?*: character cards (🛡️ Sir Gavelot, 🏴‍☠️ Cap'n Sunny, 🧐 Baron von Doubt, 🔨 Master Anvil) are **invite toggles**. (2) The chosen party → mode picker (handoff / group chat + turns-per-agent) → topic box; handoff auto-seats the Moderator.
  - Session **stage rail**: seats for the invited agents only, with a glowing "floor token" on the current speaker (derived client-side from `AgentDelta` speaker changes). Below it: mode chips (orchestration, Pragmatist `plan`/`execute`, researching count), the Pragmatist's **todo panel** and the **workspace** file list (click to preview, rendered as markdown).
  - Transcript: emoji avatar, colored name and role chip per message; bodies render **markdown via Markdig** (`DisableHtml` — model output is never injected as raw HTML).
  - Reply box: **Send** (continue the debate) and **🔨 Master Anvil, act** (run the harness). In plan mode with open todos a **Confirm plan** bar appears. An **approval modal** offers Approve / Always approve / Deny.
  - Agent identity (character title, icon, role, blurb, catchphrase, color) lives in the `Members` dictionary in this file; `Name` stays the hub key.

## How to run

1. Store the Azure OpenAI connection string in **AppHost user-secrets** (one combined string carries endpoint, key, deployment, and an optional cheaper deployment for the personas):
   ```bash
   dotnet user-secrets --project src/AgentCouncil.AppHost set "ConnectionStrings:openai" "Endpoint=https://<resource>.openai.azure.com/;Key=<key>;Deployment=<deployment>;CheapDeployment=<optional-deployment>"
   ```
2. Run the app host:
   ```bash
   dotnet run --project src/AgentCouncil.AppHost
   ```
3. Open the **Aspire dashboard** (URL printed on startup) and confirm `agents` + `web` are healthy.
4. **DevUI**: open `https://localhost:<agents-port>/devui` → the four debaters appear; send a topic and watch turns + traces.
5. **Blazor**: open the `web` endpoint → invite agents → drop a topic (e.g. *"Should we adopt a 4-day work week?"*) → debate → press **🔨 Master Anvil, act**. It proposes todos in plan mode; confirm and it switches to execute. Approve its first write, pick *Always approve* for the next, and watch `decision-record.md` appear in the workspace pane. Then ask the Skeptic to rebut the record (it greps the file).
6. Agent/LLM OpenTelemetry traces, including the token split by model, show up in the Aspire dashboard.

## Build & test

```bash
dotnet build AgentCouncil.slnx
dotnet test  AgentCouncil.slnx
```

## Conventions / gotchas
- **Central package management**: all versions live in `Directory.Packages.props`; project files use versionless `<PackageReference>`. Agent Framework Hosting/DevUI are **prerelease** (`*-preview`); core + Workflows are GA. The harness types are `[Experimental]` (`MAAI001`, suppressed in the Agents csproj).
- **Tool approvals cannot round-trip through a workflow in 1.19.** Tried: `ToolApprovalAgent` around the workflow-hosting agent (the workflow errors after resume with *"Expected exactly one update for key 'SharedState'"*), around the inner agent, raw `WorkflowSession`, and raw `StreamingRun` `SendResponseAsync`. So agents *inside* workflows only get tools that need no approval, and approval-gated work runs on the standalone harness agent.
- **Don't add `CompactionProvider` to the harness agent.** Together with `TodoProvider` it breaks `ToolApprovalAgent` in 1.19: the approved call never runs, and the model replays its last tool call (e.g. dozens of `mode_set`). Bisected in isolation; removing either provider fixes it.
- **Group chat can't be a persistent `AsAIAgent()`**: a second run on the same `WorkflowSession` starts with no history, hence the per-turn history reseed.
- **DevUI pipeline order matters**: `MapOpenAIResponses()` → `MapOpenAIConversations()` → `MapDevUI()`, and it's guarded by `IsDevelopment()`.
- **Handoff state** is held by the per-connection `StreamingRun` (opened once via `InProcessExecution.OpenStreamingAsync`), not by a manually-managed message list. A turn ends when `WatchStreamAsync` completes with no further handoff — that's the "ask the user" moment.
- **Don't raise `HubOptions.MaximumParallelInvocationsPerClient`** (default 1): per-connection invocations are serialized, which is the only turn lock the hub has. It is also why an approval answer arrives as a *new* hub call and harness run, never awaited inside a streaming one.
- The **AppHost must not** carry a plain `<ProjectReference>` to `ServiceDefaults` (triggers `ASPIRE004`); only `agents` and `web` are referenced as resources.
