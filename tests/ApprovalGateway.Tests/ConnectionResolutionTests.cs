using ApprovalGateway.Configuration;
using Microsoft.Agents.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ApprovalGateway.Tests;

public sealed class ConnectionResolutionTests
{
    [Fact]
    public void ConfigurationConnections_ResolvesMsalProviderWithExplicitAssemblyAndType()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Connections:ServiceConnection:Assembly"] = "Microsoft.Agents.Authentication.Msal",
                ["Connections:ServiceConnection:Type"] = "MsalAuth",
                ["Connections:ServiceConnection:Settings:AuthType"] = "ClientSecret",
                ["Connections:ServiceConnection:Settings:ClientId"] = "5936429a-7889-45c1-983e-d9064aa7ee84",
                ["Connections:ServiceConnection:Settings:ClientSecret"] = "test-secret-not-used-for-token-call",
                ["Connections:ServiceConnection:Settings:AuthorityEndpoint"] =
                    "https://login.microsoftonline.com/e9dbba09-e7a3-42be-9a2c-f82470024e00",
                ["Connections:ServiceConnection:Settings:Scopes:0"] = "https://api.botframework.com/.default",
                ["ConnectionsMap:0:ServiceUrl"] = "*",
                ["ConnectionsMap:0:Connection"] = "ServiceConnection",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IConfiguration>(configuration);
        using var provider = services.BuildServiceProvider();

        var connections = new ConfigurationConnections(provider, configuration);
        var tokenProvider = connections.GetDefaultConnection();

        Assert.NotNull(tokenProvider);
        Assert.Contains("Msal", tokenProvider.GetType().FullName, StringComparison.Ordinal);
    }
}
