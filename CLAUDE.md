# AgentCouncil

A multi-agent "council" app built on the **Microsoft Agent Framework** (.NET, GA 1.11) and **.NET Aspire**. A user drops a topic; a council of four agents (**Moderator + Optimist + Skeptic + Pragmatist**) debate it via **handoff orchestration**, then return the floor to the user. Backed by **Azure OpenAI** (API-key auth). Visualized through the Agent Framework **DevUI** debugger and a custom **Blazor** chat front-end.

## Architecture

```
src/AgentCouncil.AppHost          Aspire orchestration: openai connection + agents + web
src/AgentCouncil.ServiceDefaults  Shared telemetry/health/service-discovery (AddServiceDefaults / MapDefaultEndpoints)
src/AgentCouncil.Agents           ASP.NET Core: the 4 agents, handoff workflow, DevUI, SignalR hub
src/AgentCouncil.Web              Blazor Server: council chat UI
test/AgentCouncil.AppHost.Tests   Aspire integration test
```

### AgentCouncil.Agents
- **`AzureOpenAIChatClient.cs`** — `AddCouncilChatClient()` parses the single `openai` connection string (`Endpoint=...;Key=...;Deployment=...`) and registers a shared `IChatClient` wrapped with OpenTelemetry (source `AgentCouncil.Agents`).
- **`Agents/CouncilAgents.cs`** — singleton holding the four `ChatClientAgent`s with their persona instructions. Reused by both the workflow and DevUI.
- **`Workflow/CouncilWorkflow.cs`** — builds the handoff workflow: `AgentWorkflowBuilder.CreateHandoffBuilderWith(moderator).WithHandoffs(moderator, personas).WithHandoffs(personas, moderator).EnableReturnToPrevious().Build()`. Built once; each session opens its own streaming run.
- **`Hubs/CouncilHub.cs`** + **`CouncilSessionManager.cs`** — SignalR hub at `/councilhub`. One `StreamingRun` per connection holds the full conversation across turns.
  - `StartCouncil(topic)` / `SendUserInput(text)` → `run.TrySendMessageAsync(text)` then iterate `run.WatchStreamAsync()`.
  - Client-bound: `AgentDelta(agent, text)`, `AwaitUserInput()`, `CouncilError(message)`.
- **`Program.cs`** — registers everything; DevUI (`AddDevUI` + `MapOpenAIResponses`/`MapOpenAIConversations`/`MapDevUI`) is **Development-only**.

### AgentCouncil.Web
- **`Services/CouncilClient.cs`** — per-circuit `HubConnection` to the `agents` service. Resolves the hub URL from Aspire service-discovery config (`services:agents:https:0`, falling back to `http`).
- **`Components/Pages/Council.razor`** — topic box → transcript (one colored bubble per speaking agent, text accumulates on `AgentDelta`) → reply box enabled on `AwaitUserInput`.

## How to run

1. Store the Azure OpenAI connection string in **AppHost user-secrets** (one combined string carries endpoint, key, and deployment):
   ```bash
   dotnet user-secrets --project src/AgentCouncil.AppHost set "ConnectionStrings:openai" "Endpoint=https://<resource>.openai.azure.com/;Key=<key>;Deployment=<deployment>"
   ```
2. Run the app host:
   ```bash
   dotnet run --project src/AgentCouncil.AppHost
   ```
3. Open the **Aspire dashboard** (URL printed on startup) and confirm `agents` + `web` are healthy.
4. **DevUI**: open `https://localhost:<agents-port>/devui` → the four agents appear; send a topic and watch handoff turns + traces.
5. **Blazor**: open the `web` endpoint → drop a topic (e.g. *"Should we adopt a 4-day work week?"*) → the Moderator opens, personas debate (each turn labeled), and the reply box activates when the council asks for your input.
6. Agent/LLM OpenTelemetry traces show up in the Aspire dashboard.

## Build & test

```bash
dotnet build AgentCouncil.slnx
dotnet test  AgentCouncil.slnx
```

## Conventions / gotchas
- **Central package management**: all versions live in `Directory.Packages.props`; project files use versionless `<PackageReference>`. Agent Framework Hosting/DevUI are **prerelease** (`*-preview`); core + Workflows are GA.
- **DevUI pipeline order matters**: `MapOpenAIResponses()` → `MapOpenAIConversations()` → `MapDevUI()`, and it's guarded by `IsDevelopment()`.
- **Handoff state** is held by the per-connection `StreamingRun` (opened once via `InProcessExecution.OpenStreamingAsync`), not by a manually-managed message list. A turn ends when `WatchStreamAsync` completes with no further handoff — that's the "ask the user" moment.
- The **AppHost must not** carry a plain `<ProjectReference>` to `ServiceDefaults` (triggers `ASPIRE004`); only `agents` and `web` are referenced as resources.
```
