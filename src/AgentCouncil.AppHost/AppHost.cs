var builder = DistributedApplication.CreateBuilder(args);

// Existing Azure OpenAI resource (API-key auth). The connection string lives in AppHost
// user-secrets: ConnectionStrings:openai = "Endpoint=...;Key=...;Deployment=...".
var openai = builder.AddConnectionString("openai");

var agents = builder.AddProject<Projects.AgentCouncil_Agents>("agents")
    .WithReference(openai);

builder.AddProject<Projects.AgentCouncil_Web>("web")
    .WithReference(agents)
    .WaitFor(agents);

builder.Build().Run();
