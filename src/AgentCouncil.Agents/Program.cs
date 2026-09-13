using AgentCouncil.Agents;
using AgentCouncil.Agents.Agents;
using AgentCouncil.Agents.Hubs;
using AgentCouncil.Agents.Workflow;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Shared Azure OpenAI chat client (API-key auth via the "openai" connection string).
builder.AddCouncilChatClient();

// Council agents + handoff workflow, reused across sessions.
builder.Services.AddSingleton<CouncilAgents>();
builder.Services.AddSingleton<CouncilWorkflow>();
builder.Services.AddSingleton<CouncilSessionManager>();

// SignalR transport for the Blazor front-end.
builder.Services.AddSignalR();

// Emit traces for the agent/LLM calls into the Aspire dashboard.
builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddSource(
    AzureOpenAIChatClient.ActivitySourceName,
    "Microsoft.Extensions.AI",
    "*Microsoft.Agents.AI*"));

// DevUI is a developer-only debugger; only wire it up outside of production.
if (builder.Environment.IsDevelopment())
{
    builder.AddDevUI();
    builder.Services.AddOpenAIResponses();
    builder.Services.AddOpenAIConversations();

    // Surface each council agent in the DevUI sidebar (reusing the same instances as the workflow).
    builder.AddAIAgent(CouncilAgents.ModeratorName, (sp, _) => sp.GetRequiredService<CouncilAgents>().Moderator);
    builder.AddAIAgent(CouncilAgents.OptimistName, (sp, _) => sp.GetRequiredService<CouncilAgents>().Optimist);
    builder.AddAIAgent(CouncilAgents.SkepticName, (sp, _) => sp.GetRequiredService<CouncilAgents>().Skeptic);
    builder.AddAIAgent(CouncilAgents.PragmatistName, (sp, _) => sp.GetRequiredService<CouncilAgents>().Pragmatist);
}

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapHub<CouncilHub>("/councilhub");

if (app.Environment.IsDevelopment())
{
    // Order matters: OpenAI endpoints must be mapped before DevUI mounts at /devui.
    app.MapOpenAIResponses();
    app.MapOpenAIConversations();
    app.MapDevUI();

    // The full-roster handoff graph as Mermaid, for slides.
    app.MapGet("/council/graph", (CouncilAgents agents, CouncilWorkflow council) =>
        WorkflowVisualizer.ToMermaidString(council.BuildHandoff(agents.Personas)));
}

app.Run();
