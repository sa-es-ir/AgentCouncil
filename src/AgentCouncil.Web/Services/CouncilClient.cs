using Microsoft.AspNetCore.SignalR.Client;

namespace AgentCouncil.Web.Services;

/// <summary>One transcript bubble: a single agent's (accumulating) contribution.</summary>
public sealed class TranscriptEntry
{
    public required string Agent { get; init; }
    public string Text { get; set; } = string.Empty;
}

/// <summary>One line of the Pragmatist's todo list (mirrors the hub's TodoView).</summary>
public sealed record TodoView(string Title, bool Done);

/// <summary>
/// Per-circuit SignalR client to the <c>agents</c> service's council hub. Resolves the hub URL
/// via Aspire service discovery configuration and relays hub callbacks to the UI as events.
/// </summary>
public sealed class CouncilClient(IConfiguration configuration) : IAsyncDisposable
{
    private HubConnection? _connection;

    public event Action<string, string>? AgentDelta;
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
        _connection.On("AwaitUserInput", () => AwaitUserInput?.Invoke());
        _connection.On<string>("CouncilError", message => CouncilError?.Invoke(message));
        _connection.On<string, string, string>("ApprovalRequested", (agent, tool, args) => ApprovalRequested?.Invoke(agent, tool, args));
        _connection.On<string, TodoView[], int>("HarnessState", (mode, todos, researching) => HarnessState?.Invoke(mode, todos, researching));
        _connection.On<string[]>("FilesChanged", files => FilesChanged?.Invoke(files));

        await _connection.StartAsync(cancellationToken);
    }

    /// <param name="mode">"handoff" or "groupchat".</param>
    /// <param name="roundsPerAgent">Group chat only: how many times each agent speaks per user turn.</param>
    /// <param name="agentNames">The invited debaters.</param>
    public Task StartCouncilAsync(string topic, string mode, int roundsPerAgent, string[] agentNames) =>
        _connection?.InvokeAsync("StartCouncil", topic, mode, roundsPerAgent, agentNames) ?? Task.CompletedTask;

    public Task SendUserInputAsync(string text) =>
        _connection?.InvokeAsync("SendUserInput", text) ?? Task.CompletedTask;

    public Task AskPragmatistAsync(string instruction) =>
        _connection?.InvokeAsync("AskPragmatist", instruction) ?? Task.CompletedTask;

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
