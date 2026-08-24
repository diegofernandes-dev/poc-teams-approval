using ApprovalGateway.Configuration;
using Microsoft.Extensions.Configuration;

namespace ApprovalGateway.Tests;

public sealed class BotConfigurationTests
{
    [Fact]
    public void MapMicrosoftAppSettings_MapsBotCredentialsToAgentsSdkKeys()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [BotConfiguration.MicrosoftAppIdKey] = "5936429a-7889-45c1-983e-d9064aa7ee84",
                [BotConfiguration.MicrosoftAppTenantIdKey] = "e9dbba09-e7a3-42be-9a2c-f82470024e00",
                [BotConfiguration.MicrosoftAppPasswordKey] = "test-secret",
            })
            .Build();

        var overrides = BotConfiguration.MapMicrosoftAppSettings(configuration);

        Assert.Equal("5936429a-7889-45c1-983e-d9064aa7ee84", overrides[BotConfiguration.ClientIdKey]);
        Assert.Equal("5936429a-7889-45c1-983e-d9064aa7ee84", overrides[BotConfiguration.TokenValidationAudienceKey]);
        Assert.Equal("e9dbba09-e7a3-42be-9a2c-f82470024e00", overrides[BotConfiguration.TokenValidationTenantIdKey]);
        Assert.Equal(
            "https://login.microsoftonline.com/e9dbba09-e7a3-42be-9a2c-f82470024e00",
            overrides[BotConfiguration.AuthorityEndpointKey]);
        Assert.Equal("test-secret", overrides[BotConfiguration.ClientSecretKey]);
        Assert.Equal(BotConfiguration.DefaultBotFrameworkScope, overrides[BotConfiguration.ScopesKey]);
    }

    [Fact]
    public void MapMicrosoftAppSettings_DoesNotOverrideExistingScope()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [BotConfiguration.MicrosoftAppIdKey] = "5936429a-7889-45c1-983e-d9064aa7ee84",
                [BotConfiguration.ScopesKey] = "https://example.com/.default",
            })
            .Build();

        var overrides = BotConfiguration.MapMicrosoftAppSettings(configuration);

        Assert.False(overrides.ContainsKey(BotConfiguration.ScopesKey));
    }

    [Fact]
    public void MapMicrosoftAppSettings_SkipsEmptyValues()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var overrides = BotConfiguration.MapMicrosoftAppSettings(configuration);

        Assert.Empty(overrides);
    }
}
