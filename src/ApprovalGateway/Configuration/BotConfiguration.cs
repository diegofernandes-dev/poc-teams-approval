using Microsoft.Extensions.Configuration;

namespace ApprovalGateway.Configuration;

public static class BotConfiguration
{
    public const string MicrosoftAppIdKey = "MicrosoftAppId";
    public const string MicrosoftAppTenantIdKey = "MicrosoftAppTenantId";
    public const string MicrosoftAppPasswordKey = "MicrosoftAppPassword";

    public const string ClientIdKey = "Connections:ServiceConnection:Settings:ClientId";
    public const string ClientSecretKey = "Connections:ServiceConnection:Settings:ClientSecret";
    public const string AuthorityEndpointKey = "Connections:ServiceConnection:Settings:AuthorityEndpoint";
    public const string ScopesKey = "Connections:ServiceConnection:Settings:Scopes:0";
    public const string DefaultBotFrameworkScope = "https://api.botframework.com/.default";
    public const string TokenValidationTenantIdKey = "TokenValidation:TenantId";
    public const string TokenValidationAudienceKey = "TokenValidation:Audiences:0";

    public static Dictionary<string, string?> MapMicrosoftAppSettings(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var overrides = new Dictionary<string, string?>();

        var appId = configuration[MicrosoftAppIdKey];
        if (!string.IsNullOrWhiteSpace(appId))
        {
            overrides[ClientIdKey] = appId;
            overrides[TokenValidationAudienceKey] = appId;
        }

        var tenantId = configuration[MicrosoftAppTenantIdKey];
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            overrides[TokenValidationTenantIdKey] = tenantId;
            overrides[AuthorityEndpointKey] = $"https://login.microsoftonline.com/{tenantId}";
        }

        var password = configuration[MicrosoftAppPasswordKey];
        if (!string.IsNullOrWhiteSpace(password))
        {
            overrides[ClientSecretKey] = password;
        }

        // MSAL client-credentials auth requires at least one scope. Ensure the Bot Framework
        // default even when appsettings.json was not loaded by the Functions host.
        var existingScope = configuration[ScopesKey];
        if (string.IsNullOrWhiteSpace(existingScope)
            && (!string.IsNullOrWhiteSpace(appId) || !string.IsNullOrWhiteSpace(configuration[ClientIdKey])))
        {
            overrides[ScopesKey] = DefaultBotFrameworkScope;
        }

        return overrides;
    }
}
