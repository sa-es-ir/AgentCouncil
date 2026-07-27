using Microsoft.Agents.AI.Workflows;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;

namespace AgentCouncil.Agents.Hubs;

/// <summary>
/// SignalR hub the Blazor front-end uses to drive a council session.
///
/// Client-bound messages:
///   AgentDelta(string agent, string text) — a chunk of streamed text from the named agent.
///   AwaitUserInput()                       — the council yielded the floor; enable the input box.
///   CouncilError(string message)           — the workflow raised an error.
/// </summary>
public sealed class CouncilHub(CouncilSessionManager sessions, ILogger<CouncilHub> logger) : Hub
{
    /// <summary>Begins a new council session on the given topic.</summary>
    /// <param name="mode">"handoff" or "groupchat".</param>
    /// <param name="roundsPerAgent">Group chat only: how many times each agent speaks per user turn.</param>
    public async Task StartCouncil(string topic, string mode, int roundsPerAgent)
    {
        CouncilSession session = await sessions.StartAsync(
            Context.ConnectionId, mode, roundsPerAgent, Context.ConnectionAborted);
        await RunTurnAsync(session, topic);
    }

    /// <summary>Submits the user's reply into the ongoing session and streams the next turns.</summary>
    public async Task SendUserInput(string text)
    {
        if (!sessions.TryGet(Context.ConnectionId, out CouncilSession session))
        {
            await Clients.Caller.SendAsync("CouncilError", "No active council session. Start a topic first.");
            return;
        }

        await RunTurnAsync(session, text);
    }

    private async Task RunTurnAsync(CouncilSession session, string userInput)
    {
        CancellationToken ct = Context.ConnectionAborted;

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
                    string? text = update.Update.Text;
                    if (!string.IsNullOrEmpty(text))
                    {
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

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await sessions.EndAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
