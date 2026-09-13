using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AgentCouncil.Agents.Agents;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;

namespace AgentCouncil.Agents.Hubs;

/// <summary>One todo line as the Blazor front-end shows it.</summary>
public sealed record TodoView(string Title, bool Done);

/// <summary>
/// SignalR hub the Blazor front-end uses to drive a council session.
///
/// Client-bound messages:
///   AgentDelta(string agent, string text)                     — a chunk of streamed text (or a tool note) from the named agent.
///   AwaitUserInput()                                           — the floor is back with the user; enable the input box.
///   CouncilError(string message)                               — the workflow or harness raised an error.
///   ApprovalRequested(string agent, string tool, string args)  — the Pragmatist wants to run a gated tool; answer with RespondToApproval.
///   HarnessState(string mode, TodoView[] todos, int researching) — the Pragmatist's mode, todo list and running background tasks.
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

    /// <summary>Begins a new council session on the given topic.</summary>
    /// <param name="mode">"handoff" or "groupchat".</param>
    /// <param name="roundsPerAgent">Group chat only: how many times each agent speaks per user turn.</param>
    /// <param name="agentNames">The invited debaters.</param>
    public async Task StartCouncil(string topic, string mode, int roundsPerAgent, string[] agentNames)
    {
        CouncilSession session;
        try
        {
            session = await sessions.StartAsync(
                Context.ConnectionId, mode, roundsPerAgent, agentNames, Context.ConnectionAborted);
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

    /// <summary>Hands the debate so far, plus the user's instruction, to the Pragmatist's harness agent.</summary>
    public async Task AskPragmatist(string instruction)
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

    /// <summary>Answers the pending approval and lets the Pragmatist carry on.</summary>
    /// <param name="decision">"approve", "always" (approve and don't ask again for this tool) or "deny".</param>
    public async Task RespondToApproval(string decision)
    {
        if (!sessions.TryGet(Context.ConnectionId, out CouncilSession session) || session.PendingApproval is not { } request)
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

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await sessions.EndAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private static string? DisplayText(AIContent content) => content switch
    {
        TextContent text => text.Text,

        // Handoff calls are plumbing; the floor token already shows them.
        FunctionCallContent call when !call.Name.StartsWith("handoff_to", StringComparison.Ordinal) =>
            call.Arguments?.TryGetValue("fileName", out object? file) == true
                ? $"\n\n`🔧 {call.Name} {file}`\n\n"
                : $"\n\n`🔧 {call.Name}`\n\n",
        _ => null,
    };

    private async Task RunTurnAsync(CouncilSession session, string userInput)
    {
        CancellationToken ct = Context.ConnectionAborted;
        session.UnseenDebate.Append("\n\nUser: ").Append(userInput);
        session.LastSpeaker = "User";

        try
        {
            bool completed;
            if (session.IsGroupChat)
            {
                // The group chat workflow terminates after its round budget, so every user turn
                // replays the accumulated history through a fresh run.
                session.History.Add(new ChatMessage(ChatRole.User, userInput));
                await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
                    session.GroupChatWorkflow!, new List<ChatMessage>(session.History), cancellationToken: ct);
                await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
                completed = await StreamTurnAsync(run, session, ct);
            }
            else
            {
                // The handoff executors buffer incoming messages; they only take their turn once a
                // TurnToken arrives. Without it the run idles and the stream ends with no events.
                StreamingRun run = session.HandoffRun!;
                await run.TrySendMessageAsync(new ChatMessage(ChatRole.User, userInput));
                await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
                completed = await StreamTurnAsync(run, session, ct);
            }

            if (completed)
            {
                // Background research may have moved on while the council talked.
                await PushHarnessStateAsync(session, ct);

                // The stream completed without another turn pending: the floor is back with the user.
                await Clients.Caller.SendAsync("AwaitUserInput", ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Connection went away mid-turn; nothing to report.
        }
    }

    /// <summary>Forwards workflow events to the caller; returns false if the workflow errored.</summary>
    private async Task<bool> StreamTurnAsync(StreamingRun run, CouncilSession session, CancellationToken ct)
    {
        List<ChatMessage>? finalConversation = null;

        await foreach (WorkflowEvent evt in run.WatchStreamAsync(ct))
        {
            switch (evt)
            {
                case AgentResponseUpdateEvent update:
                    string agent = update.Update.AuthorName ?? "Council";
                    foreach (AIContent content in update.Update.Contents)
                    {
                        if (DisplayText(content) is not { Length: > 0 } text)
                        {
                            continue;
                        }

                        if (agent != session.LastSpeaker)
                        {
                            session.UnseenDebate.Append("\n\n").Append(agent).Append(": ");
                            session.LastSpeaker = agent;
                        }

                        session.UnseenDebate.Append(text);
                        await Clients.Caller.SendAsync("AgentDelta", agent, text, ct);
                    }

                    break;

                case WorkflowOutputEvent output when output.As<List<ChatMessage>>() is { } conversation:
                    finalConversation = conversation;
                    break;

                case WorkflowErrorEvent error:
                    logger.LogError(error.Exception, "Council workflow error");
                    await Clients.Caller.SendAsync(
                        "CouncilError", error.Exception?.Message ?? "Unknown workflow error.", ct);
                    return false;
            }
        }

        // Group chat emits the full conversation on completion; keep it as the seed for the next turn.
        if (finalConversation is not null)
        {
            session.History.Clear();
            session.History.AddRange(finalConversation);
        }

        return true;
    }

    /// <summary>
    /// Runs the harness agent. The stream completes either when the Pragmatist is done (floor back to the
    /// user) or when it needs a write approved (the request is parked on the session and sent to the client).
    /// </summary>
    private async Task RunHarnessAsync(CouncilSession session, IEnumerable<ChatMessage> messages)
    {
        CancellationToken ct = Context.ConnectionAborted;
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
                        await Clients.Caller.SendAsync("AgentDelta", CouncilAgents.PragmatistName, text, ct);
                    }
                }
            }

            await PushHarnessStateAsync(session, ct);

            if (session.PendingApproval is { ToolCall: var toolCall })
            {
                (string tool, string args) = toolCall is FunctionCallContent call
                    ? (call.Name, JsonSerializer.Serialize(call.Arguments, IndentedJson))
                    : (toolCall.GetType().Name, string.Empty);
                await Clients.Caller.SendAsync("ApprovalRequested", CouncilAgents.PragmatistName, tool, args, ct);
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
            logger.LogError(ex, "Pragmatist harness error");
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
