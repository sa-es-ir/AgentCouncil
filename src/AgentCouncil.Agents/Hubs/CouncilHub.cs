using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AgentCouncil.Agents.Agents;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Specialized.Magentic;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;

namespace AgentCouncil.Agents.Hubs;

/// <summary>One todo line as the Blazor front-end shows it.</summary>
public sealed record TodoView(string Title, bool Done);

/// <summary>
/// SignalR hub the Blazor front-end uses to drive a council session.
///
/// Client-bound messages:
///   AgentDelta(string agent, string text)                     — a chunk of streamed text from the named agent.
///   AgentLog(string agent, string kind, string text)           — side-log line: kind is reasoning (streamed in chunks), tool, result or search.
///   AwaitUserInput()                                           — the floor is back with the user; enable the input box.
///   CouncilError(string message)                               — the workflow or harness raised an error.
///   ApprovalRequested(string agent, string tool, string args)  — an agent wants to run a gated tool; answer with RespondToApproval.
///   HarnessState(string mode, TodoView[] todos, int researching) — Pragmatic's mode, todo list and running background tasks.
///   FilesChanged(string[] paths)                               — the workspace file list.
/// </summary>
public sealed class CouncilHub(CouncilSessionManager sessions, CouncilAgents agents, ILogger<CouncilHub> logger) : Hub
{
    // Relaxed escaping keeps "–" and friends readable; the UI renders the text encoded, never as HTML.
    private static readonly JsonSerializerOptions IndentedJson = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonSerializerOptions RelaxedJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Begins a new council session on the given topic.</summary>
    /// <param name="mode">"handoff", "groupchat" or "magentic".</param>
    /// <param name="roundsPerAgent">Group chat and Magentic: how many times each agent may speak per user turn.</param>
    /// <param name="lineup">The invited debaters, each with its per-turn token budget and reasoning effort.</param>
    public async Task StartCouncil(string topic, string mode, int roundsPerAgent, AgentSetup[] lineup)
    {
        CouncilSession session;
        try
        {
            session = await sessions.StartAsync(
                Context.ConnectionId, mode, roundsPerAgent, lineup, Context.ConnectionAborted);
        }
        catch (ArgumentException ex)
        {
            await Clients.Caller.SendAsync("CouncilError", ex.Message);
            return;
        }

        await PushHarnessStateAsync(session, Context.ConnectionAborted);
        await RunTurnAsync(session, topic);
    }

    /// <summary>Submits the user's reply into the ongoing debate and streams the next turns.</summary>
    public async Task SendUserInput(string text)
    {
        if (!sessions.TryGet(Context.ConnectionId, out CouncilSession session))
        {
            await Clients.Caller.SendAsync("CouncilError", "No active council session. Start a topic first.");
            return;
        }

        await RunTurnAsync(session, text);
    }

    /// <summary>Hands the debate so far, plus the user's instruction, to Pragmatic's harness agent.</summary>
    public async Task AskPragmatic(string instruction)
    {
        if (!sessions.TryGet(Context.ConnectionId, out CouncilSession session))
        {
            await Clients.Caller.SendAsync("CouncilError", "No active council session. Start a topic first.");
            return;
        }

        var prompt = new StringBuilder();
        if (session.UnseenDebate.Length > 0)
        {
            prompt.Append("Council debate since you last acted:").Append(session.UnseenDebate).Append("\n\n");
            session.UnseenDebate.Clear();
            session.LastSpeaker = null;
        }

        prompt.Append("User: ").Append(instruction);
        await RunHarnessAsync(session, [new ChatMessage(ChatRole.User, prompt.ToString())]);
    }

    /// <summary>Answers the pending approval, from the debate workflow or the harness, and lets the agent carry on.</summary>
    /// <param name="decision">"approve", "always" (approve and don't ask again for this tool) or "deny".</param>
    public async Task RespondToApproval(string decision)
    {
        if (!sessions.TryGet(Context.ConnectionId, out CouncilSession session))
        {
            await Clients.Caller.SendAsync("CouncilError", "There is no pending approval.");
            return;
        }

        if (session.PendingWorkflowApprovals.TryDequeue(out (ExternalRequest Request, string Agent) pending))
        {
            await RespondToWorkflowApprovalAsync(session, pending.Request, decision);
            return;
        }

        if (session.PendingApproval is not { } request)
        {
            await Clients.Caller.SendAsync("CouncilError", "There is no pending approval.");
            return;
        }

        session.PendingApproval = null;
        AIContent response = decision switch
        {
            "always" => request.CreateAlwaysApproveToolResponse(),
            "approve" => request.CreateResponse(true),
            _ => request.CreateResponse(false, "The user denied this action."),
        };

        // A new run carries the answer; awaiting it inside the streaming invocation would deadlock
        // behind SignalR's one-invocation-per-client queue.
        await RunHarnessAsync(session, [new ChatMessage(ChatRole.User, [response])]);
    }

    /// <summary>Reads a workspace file for preview; null when it doesn't exist or the path is rejected.</summary>
    public async Task<string?> ReadWorkspaceFile(string path)
    {
        try
        {
            // The store resolves the path safely: no escaping the workspace root, no symlinks.
            return await agents.Store.ReadAsync(path, Context.ConnectionAborted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Rejected workspace read of {Path}", path);
            return null;
        }
    }

    /// <summary>Deletes a workspace file and pushes the refreshed file list to the caller.</summary>
    public async Task DeleteWorkspaceFile(string path)
    {
        try
        {
            // Same safe path resolution as reads: nothing outside the workspace root can be deleted.
            await agents.Store.DeleteAsync(path, Context.ConnectionAborted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Rejected workspace delete of {Path}", path);
            await Clients.Caller.SendAsync("CouncilError", $"Could not delete {path}.");
        }

        await Clients.Caller.SendAsync("FilesChanged", agents.ListWorkspaceFiles());
    }

    /// <summary>Sends the workspace file list up front, so the start screen can offer resuming from a record.</summary>
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("FilesChanged", agents.ListWorkspaceFiles());
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await sessions.EndAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private static string? DisplayText(AIContent content) => content is TextContent text ? text.Text : null;

    private static string Clip(string text) => text.Length <= 300 ? text : text[..300] + "…";

    private static string ToolName(ToolApprovalRequestContent request) =>
        (request.ToolCall as FunctionCallContent)?.Name ?? request.ToolCall.GetType().Name;

    private static ToolApprovalRequestContent? AsApproval(ExternalRequest request) =>
        request.TryGetDataAs(out ToolApprovalRequestContent? approval) ? approval : null;

    private async Task RunTurnAsync(CouncilSession session, string userInput)
    {
        CancellationToken ct = Context.ConnectionAborted;
        session.UnseenDebate.Append("\n\nUser: ").Append(userInput);
        session.LastSpeaker = "User";
        var message = new ChatMessage(ChatRole.User, userInput);

        try
        {
            StreamingRun run;
            if (session.IsMagentic)
            {
                // A Magentic run ends with its verdict and can't take another task, so each user turn
                // is a fresh run seeded with the conversation so far.
                session.History.Add(message);
                run = await session.StartMagenticTurnAsync(ct);
                await run.TrySendMessageAsync(new List<ChatMessage>(session.History));
            }
            else if (session.IsGroupChat)
            {
                run = session.Run!;
                await run.TrySendMessageAsync(new List<ChatMessage> { message });
            }
            else
            {
                run = session.Run!;
                await run.TrySendMessageAsync(message);
            }

            // The executors buffer incoming messages; they only take their turn once a TurnToken
            // arrives. Without it the run idles and the stream ends with no events.
            await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
            await DriveAsync(session, run, ct);
        }
        catch (OperationCanceledException)
        {
            // Connection went away mid-turn; nothing to report.
        }
    }

    /// <summary>
    /// Streams the debate run until the floor is back with the user or an agent needs a tool approved.
    /// Approvals for tools the user chose "Always approve" for are answered here without asking.
    /// </summary>
    private async Task DriveAsync(CouncilSession session, StreamingRun run, CancellationToken ct)
    {
        while (true)
        {
            (bool ok, List<(ExternalRequest Request, string Agent)> requests) = await StreamTurnAsync(run, session, ct);
            if (!ok)
            {
                return;
            }

            bool answered = false;
            foreach ((ExternalRequest Request, string Agent) pending in requests)
            {
                if (AsApproval(pending.Request) is not { } approval)
                {
                    logger.LogWarning("Unsupported workflow request {Type}", pending.Request.Data.TypeId);
                    await Clients.Caller.SendAsync("CouncilError", "The council asked for an input this page can't show.", ct);
                    return;
                }

                if (session.AlwaysApprovedTools.Contains(ToolName(approval)))
                {
                    await run.SendResponseAsync(pending.Request.CreateResponse(approval.CreateResponse(true)));
                    answered = true;
                }
                else
                {
                    session.PendingWorkflowApprovals.Enqueue(pending);
                }
            }

            if (session.PendingWorkflowApprovals.TryPeek(out (ExternalRequest Request, string Agent) next))
            {
                await PushHarnessStateAsync(session, ct);
                await SendApprovalRequestedAsync(next.Agent, AsApproval(next.Request)!, ct);
                return;
            }

            if (!answered)
            {
                // Background research may have moved on while the council talked.
                await PushHarnessStateAsync(session, ct);

                // The stream completed without another turn pending: the floor is back with the user.
                await Clients.Caller.SendAsync("AwaitUserInput", ct);
                return;
            }
        }
    }

    /// <summary>Answers one approval a debate agent raised inside the workflow, then resumes the run.</summary>
    private async Task RespondToWorkflowApprovalAsync(CouncilSession session, ExternalRequest request, string decision)
    {
        CancellationToken ct = Context.ConnectionAborted;
        StreamingRun run = session.Run!;
        ToolApprovalRequestContent approval = AsApproval(request)!;
        if (decision == "always")
        {
            session.AlwaysApprovedTools.Add(ToolName(approval));
        }

        try
        {
            await run.SendResponseAsync(request.CreateResponse(decision == "deny"
                ? approval.CreateResponse(false, "The user denied this action.")
                : approval.CreateResponse(true)));

            // "Always" may cover the rest of the queue; anything else is asked one at a time, and the
            // run only resumes once every request it halted on has an answer.
            while (session.PendingWorkflowApprovals.TryPeek(out (ExternalRequest Request, string Agent) queued))
            {
                ToolApprovalRequestContent next = AsApproval(queued.Request)!;
                if (!session.AlwaysApprovedTools.Contains(ToolName(next)))
                {
                    await SendApprovalRequestedAsync(queued.Agent, next, ct);
                    return;
                }

                session.PendingWorkflowApprovals.Dequeue();
                await run.SendResponseAsync(queued.Request.CreateResponse(next.CreateResponse(true)));
            }

            await DriveAsync(session, run, ct);
        }
        catch (OperationCanceledException)
        {
            // Connection went away mid-turn; nothing to report.
        }
    }

    /// <summary>Forwards workflow events to the caller; returns false if the workflow errored.</summary>
    private async Task<(bool Ok, List<(ExternalRequest Request, string Agent)> Requests)> StreamTurnAsync(
        StreamingRun run, CouncilSession session, CancellationToken ct)
    {
        List<(ExternalRequest Request, string Agent)> requests = [];
        HashSet<string> handoffs = [];
        string agent = "Council";

        // Ends at a pending-request halt instead of blocking on it, so the approval can go to the user.
        await foreach (WorkflowEvent evt in run.WatchStreamAsync(blockOnPendingRequest: false, ct))
        {
            switch (evt)
            {
                case AgentResponseUpdateEvent update:
                    agent = update.Update.AuthorName ?? agent;
                    foreach (AIContent content in update.Update.Contents)
                    {
                        if (DisplayText(content) is { Length: > 0 } text)
                        {
                            await EmitAsync(session, agent, text, ct);
                        }
                        else
                        {
                            await LogAsync(agent, content, handoffs, ct);
                        }
                    }

                    break;

                case RequestInfoEvent request:
                    requests.Add((request.Request, agent));
                    break;

                // Magentic: the manager's instruction to the next speaker, voiced by the Moderator. Notes it
                // addresses to itself (it sometimes names the Moderator as the next speaker) stay hidden.
                case MagenticProgressLedgerUpdatedEvent { ProgressLedger: { IsRequestSatisfied: false } ledger }
                    when !string.IsNullOrWhiteSpace(ledger.NextSpeaker) && ledger.NextSpeaker != CouncilAgents.ModeratorName
                        && !string.IsNullOrWhiteSpace(ledger.InstructionOrQuestion):
                    await EmitAsync(session, CouncilAgents.ModeratorName, $"→ **{ledger.NextSpeaker}**: {ledger.InstructionOrQuestion}\n\n", ct);
                    break;

                // Magentic: the manager's final answer isn't streamed; it arrives as the workflow output.
                case WorkflowOutputEvent output when session.IsMagentic
                    && output.As<List<ChatMessage>>() is [.., { AuthorName: CouncilAgents.ModeratorName } verdict]:
                    await EmitAsync(session, CouncilAgents.ModeratorName, verdict.Text, ct);
                    break;

                case WorkflowErrorEvent error:
                    logger.LogError(error.Exception, "Council workflow error");
                    await Clients.Caller.SendAsync(
                        "CouncilError", error.Exception?.Message ?? "Unknown workflow error.", ct);
                    return (false, requests);
            }
        }

        return (true, requests);
    }

    /// <summary>Streams one chunk to the caller and records it for the harness (and the Magentic seed).</summary>
    private async Task EmitAsync(CouncilSession session, string agent, string text, CancellationToken ct)
    {
        bool newSpeaker = agent != session.LastSpeaker;
        if (newSpeaker)
        {
            session.UnseenDebate.Append("\n\n").Append(agent).Append(": ");
            session.LastSpeaker = agent;
        }

        session.UnseenDebate.Append(text);
        if (session.IsMagentic)
        {
            if (newSpeaker || session.History.Count == 0)
            {
                session.History.Add(new ChatMessage(ChatRole.Assistant, text) { AuthorName = agent });
            }
            else
            {
                session.History[^1].Contents.Add(new TextContent(text));
            }
        }

        await Clients.Caller.SendAsync("AgentDelta", agent, text, ct);
    }

    /// <summary>
    /// Sends the side-log line for a piece of non-text content: reasoning, a tool call, its result or a web
    /// search. Handoff calls are plumbing (the floor token shows them), so they and their results stay hidden.
    /// </summary>
    private Task LogAsync(string agent, AIContent content, HashSet<string> handoffs, CancellationToken ct)
    {
        if (content is FunctionCallContent { Name: var name } handoff && name.StartsWith("handoff_to", StringComparison.Ordinal))
        {
            handoffs.Add(handoff.CallId);
            return Task.CompletedTask;
        }

        (string Kind, string Text)? line = content switch
        {
            TextReasoningContent { Text.Length: > 0 } reasoning => ("reasoning", reasoning.Text),
            FunctionCallContent call => ("tool", $"{call.Name} {Clip(JsonSerializer.Serialize(call.Arguments, RelaxedJson))}"),
            FunctionResultContent result when !handoffs.Contains(result.CallId) =>
                ("result", Clip(result.Result as string ?? JsonSerializer.Serialize(result.Result, RelaxedJson))),
            WebSearchToolCallContent search => ("search", string.Join(" · ", search.Queries ?? Array.Empty<string>())),
            WebSearchToolResultContent found => ("search", $"{found.Outputs?.Count ?? 0} source(s) read"),
            _ => null,
        };

        return line is { } l ? Clients.Caller.SendAsync("AgentLog", agent, l.Kind, l.Text, ct) : Task.CompletedTask;
    }

    private Task SendApprovalRequestedAsync(string agent, ToolApprovalRequestContent request, CancellationToken ct)
    {
        (string tool, string args) = request.ToolCall is FunctionCallContent call
            ? (call.Name, JsonSerializer.Serialize(call.Arguments, IndentedJson))
            : (request.ToolCall.GetType().Name, string.Empty);
        return Clients.Caller.SendAsync("ApprovalRequested", agent, tool, args, ct);
    }

    /// <summary>
    /// Runs the harness agent. The stream completes either when Pragmatic is done (floor back to the
    /// user) or when it needs a write approved (the request is parked on the session and sent to the client).
    /// </summary>
    private async Task RunHarnessAsync(CouncilSession session, IEnumerable<ChatMessage> messages)
    {
        CancellationToken ct = Context.ConnectionAborted;
        HashSet<string> handoffs = [];
        try
        {
            await foreach (AgentResponseUpdate update in agents.Harness.RunStreamingAsync(
                messages, session.HarnessSession, cancellationToken: ct))
            {
                foreach (AIContent content in update.Contents)
                {
                    if (content is ToolApprovalRequestContent request)
                    {
                        session.PendingApproval = request;
                    }
                    else if (DisplayText(content) is { Length: > 0 } text)
                    {
                        await Clients.Caller.SendAsync("AgentDelta", CouncilAgents.PragmaticName, text, ct);
                    }
                    else
                    {
                        await LogAsync(CouncilAgents.PragmaticName, content, handoffs, ct);
                    }
                }
            }

            await PushHarnessStateAsync(session, ct);

            if (session.PendingApproval is { } pending)
            {
                await SendApprovalRequestedAsync(CouncilAgents.PragmaticName, pending, ct);
            }
            else
            {
                await Clients.Caller.SendAsync("AwaitUserInput", ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Connection went away mid-run; nothing to report.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Pragmatic harness error");
            await Clients.Caller.SendAsync("CouncilError", ex.Message, ct);
        }
    }

    private async Task PushHarnessStateAsync(CouncilSession session, CancellationToken ct)
    {
        string mode = await agents.HarnessModes.GetModeAsync(session.HarnessSession, ct);
        TodoView[] todos = (await agents.HarnessTodos.GetAllTodosAsync(session.HarnessSession, ct))
            .Select(t => new TodoView(t.Title, t.IsComplete))
            .ToArray();
        int researching = agents.HarnessBackground.GetIncompleteTasks(session.HarnessSession).Count;

        await Clients.Caller.SendAsync("HarnessState", mode, todos, researching, ct);
        await Clients.Caller.SendAsync("FilesChanged", agents.ListWorkspaceFiles(), ct);
    }
}
