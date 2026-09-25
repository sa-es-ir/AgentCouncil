using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;

namespace AgentCouncil.Agents;

/// <summary>
/// Registers the council's <see cref="IChatClient"/>s backed by Azure OpenAI (API-key auth).
/// The single "openai" connection string carries everything:
///   Endpoint=https://&lt;resource&gt;.openai.azure.com/;Key=&lt;key&gt;;Deployment=&lt;deployment&gt;[;CheapDeployment=&lt;deployment&gt;]
/// The default client (Deployment) runs the Moderator and the Pragmatist's harness; the keyed
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
        AzureOpenAIClient azure = new(new Uri(endpoint), new ApiKeyCredential(key));

        builder.Services.AddChatClient(_ => azure.GetChatClient(deployment).AsIChatClient())
            .UseOpenTelemetry(sourceName: ActivitySourceName, configure: o => o.EnableSensitiveData = enableSensitiveData);

        builder.Services.AddKeyedChatClient(CheapClientKey, _ => azure.GetChatClient(cheapDeployment ?? deployment).AsIChatClient())
            .UseOpenTelemetry(sourceName: ActivitySourceName, configure: o => o.EnableSensitiveData = enableSensitiveData);
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
