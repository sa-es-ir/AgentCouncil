using System.ClientModel;
using System.ClientModel.Primitives;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json.Nodes;
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
        OpenAIClientOptions clientOptions = new() { Endpoint = new Uri(V1(endpoint)) };
        clientOptions.AddPolicy(new IncompleteWebSearchPolicy(), PipelinePosition.PerCall);
        ResponsesClient responses = new OpenAIClient(new ApiKeyCredential(key), clientOptions).GetResponsesClient();

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
            messages.Select(WithoutServerItems).Where(m => m.Contents.Count > 0).Select(WithSpeaker).ToList(), options, cancellationToken);
    }

    /// <summary>
    /// Group chat hands the other members' turns over as user messages with an author, and the Responses API
    /// drops author names, so each would arrive as an anonymous line. Tagged here, on the wire only, so the
    /// agent's stored history keeps the original message.
    /// </summary>
    private static ChatMessage WithSpeaker(ChatMessage message) =>
        message.Role == ChatRole.User && message.AuthorName is { } author && message.Text is { Length: > 0 } text
            && !text.StartsWith($"[{author}]", StringComparison.Ordinal)
            ? new ChatMessage(ChatRole.User, $"[{author}] {text}") { AuthorName = author }
            : message;

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

    /// <summary>
    /// A web search cut short (e.g. by the turn's output budget) comes back as a web_search_call with
    /// status "incomplete", which OpenAI 2.14/2.15 can't deserialize (WebSearchCallStatus has no such value),
    /// so the whole stream throws mid-turn. Rewrites that status to "failed" in each SSE line before the SDK
    /// parses it.
    /// </summary>
    // ponytail: streaming responses only (all agents stream); drop this once the OpenAI release after 2.15.0 ships
    // openai/openai-dotnet#1267 (WebSearchCallStatus becomes an extensible enum that keeps unknown values).
    internal sealed class IncompleteWebSearchPolicy : PipelinePolicy
    {
        public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            ProcessNext(message, pipeline, currentIndex);
            Wrap(message);
        }

        public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            await ProcessNextAsync(message, pipeline, currentIndex).ConfigureAwait(false);
            Wrap(message);
        }

        internal static string FixLine(string line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal)
                || !line.Contains("web_search_call", StringComparison.Ordinal)
                || !line.Contains("\"incomplete\"", StringComparison.Ordinal))
            {
                return line;
            }

            JsonNode? root = JsonNode.Parse(line[5..]);
            Fix(root);
            return "data: " + root?.ToJsonString();
        }

        private static void Wrap(PipelineMessage message)
        {
            if (message.Response is { ContentStream: { } source } response
                && response.Headers.TryGetValue("Content-Type", out string? type)
                && type?.StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase) == true)
            {
                Pipe pipe = new();
                _ = PumpAsync(source, pipe.Writer);
                response.ContentStream = pipe.Reader.AsStream();
            }
        }

        private static async Task PumpAsync(Stream source, PipeWriter writer)
        {
            Exception? error = null;
            try
            {
                using StreamReader reader = new(source);
                while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
                {
                    FlushResult flushed = await writer.WriteAsync(Encoding.UTF8.GetBytes(FixLine(line) + "\n")).ConfigureAwait(false);
                    if (flushed.IsCompleted)
                    {
                        break; // the SDK stopped reading
                    }
                }
            }
            catch (Exception ex)
            {
                error = ex;
            }

            await writer.CompleteAsync(error).ConfigureAwait(false);
        }

        private static void Fix(JsonNode? node)
        {
            if (node is JsonArray array)
            {
                foreach (JsonNode? item in array)
                {
                    Fix(item);
                }
            }
            else if (node is JsonObject obj)
            {
                if (Is(obj["type"], "web_search_call") && Is(obj["status"], "incomplete"))
                {
                    obj["status"] = "failed";
                }

                foreach (KeyValuePair<string, JsonNode?> child in obj)
                {
                    Fix(child.Value);
                }
            }
        }

        private static bool Is(JsonNode? node, string value) =>
            node is JsonValue v && v.TryGetValue(out string? s) && s == value;
    }
}
