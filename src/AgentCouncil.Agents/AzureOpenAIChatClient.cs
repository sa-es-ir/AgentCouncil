using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Responses;

namespace AgentCouncil.Agents;

/// <summary>
/// Registers the council's <see cref="IChatClient"/>s backed by the Azure OpenAI Responses API (v1 endpoint,
/// API-key auth), which brings the hosted web search tool and reasoning summaries.
/// The single "openai" connection string carries everything:
///   Endpoint=https://&lt;resource&gt;.openai.azure.com/;Key=&lt;key&gt;;Deployment=&lt;deployment&gt;[;CheapDeployment=&lt;deployment&gt;]
/// The default client (Deployment) runs the Moderator and Pragmatic's harness; the keyed
/// <see cref="CheapClientKey"/> client (CheapDeployment, falling back to Deployment) runs the personas,
/// the Researcher and compaction summaries.
/// </summary>
public static class AzureOpenAIChatClient
{
    public const string ActivitySourceName = "AgentCouncil.Agents";

    public const string CheapClientKey = "cheap";

    public static void AddCouncilChatClient(this IHostApplicationBuilder builder)
    {
        string connectionString = builder.Configuration.GetConnectionString("openai")
            ?? throw new InvalidOperationException(
                "Missing 'openai' connection string. Set ConnectionStrings:openai to " +
                "'Endpoint=...;Key=...;Deployment=...'.");

        (string endpoint, string key, string deployment, string? cheapDeployment) = Parse(connectionString);
        bool enableSensitiveData = builder.Environment.IsDevelopment();

        // Azure's v1 endpoint speaks the plain OpenAI protocol, so the stock OpenAI client reaches it.
        ResponsesClient responses = new OpenAIClient(
            new ApiKeyCredential(key),
            new OpenAIClientOptions { Endpoint = new Uri(V1(endpoint)) }).GetResponsesClient();

        builder.Services.AddChatClient(_ => responses.AsIChatClient(deployment))
            .Use(Stateless)
            .UseOpenTelemetry(sourceName: ActivitySourceName, configure: o => o.EnableSensitiveData = enableSensitiveData);

        builder.Services.AddKeyedChatClient(CheapClientKey, _ => responses.AsIChatClient(cheapDeployment ?? deployment))
            .Use(Stateless)
            .UseOpenTelemetry(sourceName: ActivitySourceName, configure: o => o.EnableSensitiveData = enableSensitiveData);
    }

    /// <summary>
    /// The workflows and agent sessions own the conversation, so nothing is stored service-side: with
    /// store=false no response id comes back, and <c>ChatClientAgent</c> never switches to
    /// previous_response_id. Reasoning and web-search items can't be replayed without a stored id, so they
    /// are stripped from the input; they only feed the UI's log.
    /// </summary>
    private static Task Stateless(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, Task> next,
        CancellationToken cancellationToken)
    {
        options = options?.Clone() ?? new ChatOptions();
        Func<IChatClient, object?>? inner = options.RawRepresentationFactory;
        options.RawRepresentationFactory = client =>
        {
            CreateResponseOptions raw = inner?.Invoke(client) as CreateResponseOptions ?? new CreateResponseOptions();
            raw.StoredOutputEnabled = false;
            return raw;
        };

        return next(
            messages.Select(WithoutServerItems).Where(m => m.Contents.Count > 0).ToList(), options, cancellationToken);
    }

    private static ChatMessage WithoutServerItems(ChatMessage message)
    {
        if (!message.Contents.Any(IsServerItem))
        {
            return message;
        }

        ChatMessage copy = message.Clone();
        copy.Contents = message.Contents.Where(c => !IsServerItem(c)).ToList();
        return copy;
    }

    private static bool IsServerItem(AIContent content) =>
        content is TextReasoningContent or WebSearchToolCallContent or WebSearchToolResultContent;

    /// <summary>Accepts the resource endpoint with or without the /openai/v1 suffix.</summary>
    private static string V1(string endpoint)
    {
        string trimmed = endpoint.TrimEnd('/');
        return trimmed.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase) ? trimmed + "/" : trimmed + "/openai/v1/";
    }

    private static (string Endpoint, string Key, string Deployment, string? CheapDeployment) Parse(string connectionString)
    {
        string? endpoint = null, key = null, deployment = null, cheapDeployment = null;
        foreach (string part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int eq = part.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            string name = part[..eq].Trim();
            string value = part[(eq + 1)..].Trim();
            switch (name.ToLowerInvariant())
            {
                case "endpoint": endpoint = value; break;
                case "key": key = value; break;
                case "deployment": case "model": deployment = value; break;
                case "cheapdeployment": cheapDeployment = value; break;
            }
        }

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(deployment))
        {
            throw new InvalidOperationException(
                "Connection string 'openai' must contain Endpoint, Key and Deployment.");
        }

        return (endpoint, key, deployment, string.IsNullOrWhiteSpace(cheapDeployment) ? null : cheapDeployment);
    }
}
