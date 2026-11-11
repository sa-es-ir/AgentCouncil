using Microsoft.AspNetCore.SignalR.Client;

namespace AgentCouncil.Web.Services;

/// <summary>One transcript bubble: a single agent's (accumulating) contribution.</summary>
public sealed class TranscriptEntry
{
    public required string Agent { get; init; }
    public string Text { get; set; } = string.Empty;

    /// <summary>The side log: the agent's reasoning summary, tool calls and their results, in order.</summary>
    public List<LogLine> Log { get; } = [];
}

/// <summary>One side-log line. Kind is reasoning, tool, result or search; reasoning streams in, so Text grows.</summary>
public sealed class LogLine
{
    public required string Kind { get; init; }
    public required string Text { get; set; }
}

/// <summary>One line of Pragmatic's todo list (mirrors the hub's TodoView).</summary>
public sealed record TodoView(string Title, bool Done);

/// <summary>One invited debater and how it is tuned for the session (mirrors the hub's AgentSetup).</summary>
/// <param name="Effort">"auto" (the model's own default) or a reasoning effort: low, medium, high.</param>
public sealed record AgentSetup(string Name, int MaxOutputTokens, string Effort);

/// <summary>
/// Per-circuit SignalR client to the <c>agents</c> service's council hub. Resolves the hub URL
/// via Aspire service discovery configuration and relays hub callbacks to the UI as events.
/// </summary>
public sealed class CouncilClient(IConfiguration configuration) : IAsyncDisposable
{
    private HubConnection? _connection;

    public event Action<string, string>? AgentDelta;
    public event Action<string, string, string>? AgentLog;
    public event Action? AwaitUserInput;
    public event Action<string>? CouncilError;
    public event Action<string, string, string>? ApprovalRequested;
    public event Action<string, TodoView[], int>? HarnessState;
    public event Action<string[]>? FilesChanged;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not null)
        {
            return;
        }

        string baseUrl = configuration["services:agents:https:0"]
            ?? configuration["services:agents:http:0"]
            ?? throw new InvalidOperationException("The 'agents' service endpoint was not found in configuration.");

        _connection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl.TrimEnd('/')}/councilhub")
            .WithAutomaticReconnect()
            .Build();

        _connection.On<string, string>("AgentDelta", (agent, text) => AgentDelta?.Invoke(agent, text));
        _connection.On<string, string, string>("AgentLog", (agent, kind, text) => AgentLog?.Invoke(agent, kind, text));
        _connection.On("AwaitUserInput", () => AwaitUserInput?.Invoke());
        _connection.On<string>("CouncilError", message => CouncilError?.Invoke(message));
        _connection.On<string, string, string>("ApprovalRequested", (agent, tool, args) => ApprovalRequested?.Invoke(agent, tool, args));
        _connection.On<string, TodoView[], int>("HarnessState", (mode, todos, researching) => HarnessState?.Invoke(mode, todos, researching));
        _connection.On<string[]>("FilesChanged", files => FilesChanged?.Invoke(files));

        await _connection.StartAsync(cancellationToken);
    }

    /// <param name="mode">"handoff", "groupchat" or "magentic".</param>
    /// <param name="roundsPerAgent">Group chat and Magentic: how many times each agent may speak per user turn.</param>
    /// <param name="lineup">The invited debaters, each with its per-turn token budget and reasoning effort.</param>
    public Task StartCouncilAsync(string topic, string mode, int roundsPerAgent, AgentSetup[] lineup) =>
        _connection?.InvokeAsync("StartCouncil", topic, mode, roundsPerAgent, lineup) ?? Task.CompletedTask;

    public Task SendUserInputAsync(string text) =>
        _connection?.InvokeAsync("SendUserInput", text) ?? Task.CompletedTask;

    public Task AskPragmaticAsync(string instruction) =>
        _connection?.InvokeAsync("AskPragmatic", instruction) ?? Task.CompletedTask;

    /// <param name="decision">"approve", "always" or "deny".</param>
    public Task RespondToApprovalAsync(string decision) =>
        _connection?.InvokeAsync("RespondToApproval", decision) ?? Task.CompletedTask;

    public Task<string?> ReadWorkspaceFileAsync(string path) =>
        _connection?.InvokeAsync<string?>("ReadWorkspaceFile", path) ?? Task.FromResult<string?>(null);

    public Task DeleteWorkspaceFileAsync(string path) =>
        _connection?.InvokeAsync("DeleteWorkspaceFile", path) ?? Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
