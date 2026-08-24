using System.Text;
using ApprovalGateway.Configuration;
using Microsoft.Agents.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace ApprovalGateway.Tests;

public sealed class AspNetExtensionsTests
{
    private const string BotAppId = "5936429a-7889-45c1-983e-d9064aa7ee84";
    private const string TenantId = "e9dbba09-e7a3-42be-9a2c-f82470024e00";

    [Fact]
    public void AddAgentAspNetAuthentication_ThrowsWhenAudiencesMissing()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TokenValidation:TenantId"] = TenantId,
            })
            .Build();

        var exception = Assert.Throws<ArgumentException>(() =>
            services.AddAgentAspNetAuthentication(configuration));

        Assert.Contains("Audiences", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddAgentAspNetAuthentication_ConfiguresPublicCloudValidationSemantics()
    {
        var options = ResolveJwtBearerOptions();

        Assert.True(options.TokenValidationParameters.ValidateIssuer);
        Assert.True(options.TokenValidationParameters.ValidateAudience);
        Assert.True(options.TokenValidationParameters.ValidateLifetime);
        Assert.True(options.TokenValidationParameters.ValidateIssuerSigningKey);
        Assert.True(options.TokenValidationParameters.RequireSignedTokens);
        Assert.Equal([BotAppId], options.TokenValidationParameters.ValidAudiences);

        var issuers = options.TokenValidationParameters.ValidIssuers!.ToList();
        Assert.Contains(AuthenticationConstants.BotFrameworkTokenIssuer, issuers);
        Assert.Contains("https://sts.windows.net/d6d49420-f39b-4df7-a1dc-d59a935871db/", issuers);
        Assert.Contains("https://login.microsoftonline.com/d6d49420-f39b-4df7-a1dc-d59a935871db/v2.0", issuers);
        Assert.Contains("https://sts.windows.net/f8cdef31-a31e-4b4a-93e4-5f571e91255a/", issuers);
        Assert.Contains("https://login.microsoftonline.com/f8cdef31-a31e-4b4a-93e4-5f571e91255a/v2.0", issuers);
        Assert.Contains("https://sts.windows.net/69e9b82d-4842-4902-8d1e-abc5b98a55e8/", issuers);
        Assert.Contains("https://login.microsoftonline.com/69e9b82d-4842-4902-8d1e-abc5b98a55e8/v2.0", issuers);
        Assert.Contains($"https://sts.windows.net/{TenantId}/", issuers);
        Assert.Contains($"https://login.microsoftonline.com/{TenantId}/v2.0", issuers);
    }

    [Fact]
    public async Task OnMessageReceived_BotFrameworkIssuer_UsesAbsOpenIdMetadata()
    {
        var options = ResolveJwtBearerOptions();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = $"Bearer {CreateUnsignedJwt(AuthenticationConstants.BotFrameworkTokenIssuer)}";

        var messageContext = new MessageReceivedContext(
            httpContext,
            new AuthenticationScheme(
                JwtBearerDefaults.AuthenticationScheme,
                displayName: null,
                handlerType: typeof(JwtBearerHandler)),
            options);

        await options.Events.MessageReceived(messageContext);

        var metadataAddress = GetMetadataAddress(options.TokenValidationParameters.ConfigurationManager);
        Assert.Equal(AuthenticationConstants.PublicAzureBotServiceOpenIdMetadataUrl, metadataAddress);
    }

    [Fact]
    public async Task OnMessageReceived_EntraIssuer_UsesEntraOpenIdMetadata()
    {
        var options = ResolveJwtBearerOptions();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization =
            $"Bearer {CreateUnsignedJwt($"https://login.microsoftonline.com/{TenantId}/v2.0")}";

        var messageContext = new MessageReceivedContext(
            httpContext,
            new AuthenticationScheme(
                JwtBearerDefaults.AuthenticationScheme,
                displayName: null,
                handlerType: typeof(JwtBearerHandler)),
            options);

        await options.Events.MessageReceived(messageContext);

        var metadataAddress = GetMetadataAddress(options.TokenValidationParameters.ConfigurationManager);
        Assert.Equal(AuthenticationConstants.PublicOpenIdMetadataUrl, metadataAddress);
    }

    private static JwtBearerOptions ResolveJwtBearerOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TokenValidation:Audiences:0"] = BotAppId,
                ["TokenValidation:TenantId"] = TenantId,
            })
            .Build();

        services.AddAgentAspNetAuthentication(configuration);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
    }

    private static string GetMetadataAddress(BaseConfigurationManager? configurationManager)
    {
        Assert.NotNull(configurationManager);
        var typed = Assert.IsAssignableFrom<ConfigurationManager<OpenIdConnectConfiguration>>(configurationManager);
        return typed.MetadataAddress;
    }

    private static string CreateUnsignedJwt(string issuer)
    {
        static string Encode(string value) =>
            Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(value));

        var header = Encode("""{"alg":"none","typ":"JWT"}""");
        var payload = Encode($$"""{"iss":"{{issuer}}"}""");
        return $"{header}.{payload}.";
    }
}
