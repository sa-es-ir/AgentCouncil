namespace AgentCouncil.AppHost.Tests;

public class AppHostModelTests
{
    // Builds the app model only — nothing starts, so no Azure OpenAI secret is needed.
    [Fact]
    public async Task AppHostDeclaresCouncilResources()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AgentCouncil_AppHost>(
            TestContext.Current.CancellationToken);

        var names = appHost.Resources.Select(r => r.Name).ToList();

        Assert.Contains("openai", names);
        Assert.Contains("agents", names);
        Assert.Contains("web", names);
    }
}
