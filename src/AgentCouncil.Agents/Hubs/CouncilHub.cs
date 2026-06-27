using Microsoft.Agents.AI.Workflows;
using Microsoft.AspNetCore.SignalR;

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
    public async Task StartCouncil(string topic)
    {
        StreamingRun run = await sessions.StartAsync(Context.ConnectionId, Context.ConnectionAborted);
        await RunTurnAsync(run, topic);
    }

    /// <summary>Submits the user's reply into the ongoing session and streams the next turns.</summary>
    public async Task SendUserInput(string text)
    {
        if (!sessions.TryGet(Context.ConnectionId, out StreamingRun run))
        {
            await Clients.Caller.SendAsync("CouncilError", "No active council session. Start a topic first.");
            return;
        }

        await RunTurnAsync(run, text);
    }

    private async Task RunTurnAsync(StreamingRun run, string userInput)
    {
        CancellationToken ct = Context.ConnectionAborted;
        await run.TrySendMessageAsync(userInput);

        string? speakingAgent = null;
        try
        {
            await foreach (WorkflowEvent evt in run.WatchStreamAsync(ct))
            {
                switch (evt)
                {
                    case AgentResponseUpdateEvent update:
                        string agent = update.Update.AuthorName ?? "Council";
                        if (speakingAgent != agent)
                        {
                            speakingAgent = agent;
                        }

                        string? text = update.Update.Text;
                        if (!string.IsNullOrEmpty(text))
                        {
                            await Clients.Caller.SendAsync("AgentDelta", agent, text, ct);
                        }

                        break;

                    case WorkflowErrorEvent error:
                        logger.LogError(error.Exception, "Council workflow error");
                        await Clients.Caller.SendAsync(
                            "CouncilError", error.Exception?.Message ?? "Unknown workflow error.", ct);
                        return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Connection went away mid-turn; nothing to report.
            return;
        }

        // The stream completed without another handoff: the floor is back with the user.
        await Clients.Caller.SendAsync("AwaitUserInput", ct);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await sessions.EndAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
