using Aspire.Hosting.Azure;
using Azure.Provisioning.AppContainers;

var builder = DistributedApplication.CreateBuilder(args);

// Existing Azure OpenAI resource (API-key auth). The connection string lives in AppHost
// user-secrets: ConnectionStrings:openai = "Endpoint=...;Key=...;Deployment=...".
var openai = builder.AddConnectionString("openai");

builder.AddAzureContainerAppEnvironment("env");

var agents = builder.AddProject<Projects.AgentCouncil_Agents>("agents")
    .WithReference(openai)
    .PublishAsAzureContainerApp(SingleReplica);

if (builder.ExecutionContext.IsPublishMode)
{
    // ponytail: /tmp is writable but ephemeral, so workspace files vanish on restart; mount an Azure Files volume to keep them.
    agents.WithEnvironment("WorkspaceRoot", "/tmp/workspace");
}

builder.AddProject<Projects.AgentCouncil_Web>("web")
    .WithReference(agents)
    .WaitFor(agents)
    .WithExternalHttpEndpoints()
    .PublishAsAzureContainerApp(SingleReplica);

builder.Build().Run();

// ponytail: council sessions and Blazor circuits live in process memory, so exactly one always-on
// replica. Scaling out needs sticky sessions plus a SignalR backplane.
static void SingleReplica(AzureResourceInfrastructure _, ContainerApp app)
{
    app.Template.Scale.MinReplicas = 1;
    app.Template.Scale.MaxReplicas = 1;
}
