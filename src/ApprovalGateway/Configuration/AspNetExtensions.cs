// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// Adapted from https://github.com/microsoft/Agents/blob/main/samples/dotnet/quickstart/AspNetExtensions.cs
//
// Why this file exists:
// Microsoft.Agents.Hosting.AspNetCore 1.6.150 (and later 1.7.x) does not ship
// AddAgentAspNetAuthentication. Inbound JWT validation for Azure Bot Service
// traffic remains sample-owned application code. This copy is trimmed to the
// Azure Public Cloud + SingleTenant Bot deployment used by this POC; see the
// removal notes below the class for each omitted branch.

using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Validators;

namespace ApprovalGateway.Configuration;

public static class AspNetExtensions
{
    private static readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> OpenIdMetadataCache = new();

    /// <summary>
    /// Adds JWT bearer validation for Azure Bot Service and Entra-issued agent tokens
    /// on Azure Public Cloud.
    /// </summary>
    public static void AddAgentAspNetAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        string tokenValidationSectionName = "TokenValidation")
    {
        var tokenValidationSection = configuration.GetSection(tokenValidationSectionName);
        if (!tokenValidationSection.Exists())
        {
            throw new ArgumentException(
                $"Configuration section '{tokenValidationSectionName}' is missing. Token validation requires a valid configuration section.");
        }

        services.AddAgentAspNetAuthentication(tokenValidationSection.Get<TokenValidationOptions>()!);
    }

    public static void AddAgentAspNetAuthentication(this IServiceCollection services, TokenValidationOptions validationOptions)
    {
        AssertionHelpers.ThrowIfNull(validationOptions, nameof(validationOptions));

        if (validationOptions.Audiences == null || validationOptions.Audiences.Count == 0)
        {
            throw new ArgumentException($"{nameof(TokenValidationOptions)}:Audiences requires at least one ClientId");
        }

        foreach (var audience in validationOptions.Audiences)
        {
            if (!Guid.TryParse(audience, out _))
            {
                throw new ArgumentException($"{nameof(TokenValidationOptions)}:Audiences values must be a GUID");
            }
        }

        // Public-cloud ABS inbound issuers. Matches the official quickstart defaults when
        // IsGov=false and AzureBotServiceOnly=false:
        // - Bot Framework ABS issuer (classic ABS tokens)
        // - Microsoft Bot Service Entra tenants (ABS channel tokens migrating to Entra)
        // - Tenant-specific issuers when TenantId is configured (SingleTenant bot)
        var validIssuers = new List<string>
        {
            AuthenticationConstants.BotFrameworkTokenIssuer,
            "https://sts.windows.net/d6d49420-f39b-4df7-a1dc-d59a935871db/",
            "https://login.microsoftonline.com/d6d49420-f39b-4df7-a1dc-d59a935871db/v2.0",
            "https://sts.windows.net/f8cdef31-a31e-4b4a-93e4-5f571e91255a/",
            "https://login.microsoftonline.com/f8cdef31-a31e-4b4a-93e4-5f571e91255a/v2.0",
            "https://sts.windows.net/69e9b82d-4842-4902-8d1e-abc5b98a55e8/",
            "https://login.microsoftonline.com/69e9b82d-4842-4902-8d1e-abc5b98a55e8/v2.0",
        };

        if (!string.IsNullOrEmpty(validationOptions.TenantId) && Guid.TryParse(validationOptions.TenantId, out _))
        {
            validIssuers.Add(string.Format(CultureInfo.InvariantCulture, AuthenticationConstants.ValidTokenIssuerUrlTemplateV1, validationOptions.TenantId));
            validIssuers.Add(string.Format(CultureInfo.InvariantCulture, AuthenticationConstants.ValidTokenIssuerUrlTemplateV2, validationOptions.TenantId));
        }

        // Hardcoded to Public Cloud defaults (equivalent to sample when IsGov=false and
        // AzureBotServiceOpenIdMetadataUrl / OpenIdMetadataUrl are unset).
        const string azureBotServiceOpenIdMetadataUrl = AuthenticationConstants.PublicAzureBotServiceOpenIdMetadataUrl;
        const string openIdMetadataUrl = AuthenticationConstants.PublicOpenIdMetadataUrl;
        var openIdMetadataRefresh = BaseConfigurationManager.DefaultAutomaticRefreshInterval;

        _ = services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5),
                    ValidIssuers = validIssuers,
                    ValidAudiences = validationOptions.Audiences,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                };

                options.TokenValidationParameters.EnableAadSigningKeyIssuerValidation();

                options.Events = new JwtBearerEvents
                {
                    // ABS tokens and Entra tokens use different OpenID metadata endpoints.
                    // AzureBotServiceTokenHandling remains enabled (sample default = true).
                    OnMessageReceived = context =>
                    {
                        var authorizationHeader = context.Request.Headers.Authorization.ToString();
                        if (string.IsNullOrEmpty(authorizationHeader))
                        {
                            context.Options.TokenValidationParameters.ConfigurationManager ??=
                                options.ConfigurationManager as BaseConfigurationManager;
                            return Task.CompletedTask;
                        }

                        var parts = authorizationHeader.Split(' ');
                        if (parts.Length != 2 || parts[0] != "Bearer")
                        {
                            context.Options.TokenValidationParameters.ConfigurationManager ??=
                                options.ConfigurationManager as BaseConfigurationManager;
                            return Task.CompletedTask;
                        }

                        var token = new JsonWebToken(parts[1]);
                        var issuer = token.Issuer;

                        if (IsBotFrameworkIssuer(issuer))
                        {
                            context.Options.TokenValidationParameters.ConfigurationManager = OpenIdMetadataCache.GetOrAdd(
                                azureBotServiceOpenIdMetadataUrl,
                                _ => new ConfigurationManager<OpenIdConnectConfiguration>(
                                    azureBotServiceOpenIdMetadataUrl,
                                    new OpenIdConnectConfigurationRetriever(),
                                    new HttpClient())
                                {
                                    AutomaticRefreshInterval = openIdMetadataRefresh
                                });
                        }
                        else
                        {
                            context.Options.TokenValidationParameters.ConfigurationManager = OpenIdMetadataCache.GetOrAdd(
                                openIdMetadataUrl,
                                _ => new ConfigurationManager<OpenIdConnectConfiguration>(
                                    openIdMetadataUrl,
                                    new OpenIdConnectConfigurationRetriever(),
                                    new HttpClient())
                                {
                                    AutomaticRefreshInterval = openIdMetadataRefresh
                                });
                        }

                        return Task.CompletedTask;
                    },
                };
            });
    }

    private static bool IsBotFrameworkIssuer(string issuer)
    {
        // Public Cloud ABS issuer only. Gov/China ABS issuers are unreachable for this
        // East US 2 / Azure Public deployment and are not in ValidIssuers.
        return AuthenticationConstants.BotFrameworkTokenIssuer.Equals(issuer, StringComparison.OrdinalIgnoreCase);
    }

    public sealed class TokenValidationOptions
    {
        /// <summary>
        /// Bot Microsoft App ID(s). Must contain at least one GUID audience.
        /// </summary>
        public IList<string>? Audiences { get; set; }

        /// <summary>
        /// Entra tenant ID of the SingleTenant bot. When set, tenant-specific v1/v2 issuers are accepted.
        /// </summary>
        public string? TenantId { get; set; }
    }
}

// Removal notes (relative to the official quickstart AspNetExtensions.cs):
//
// IsGov / Gov ValidIssuers / Gov metadata URLs:
//   Unreachable. This POC runs in Azure Public (East US 2). Government cloud
//   issuers and metadata endpoints are never presented by Azure Bot Service here.
//
// China Bot Framework issuer handling:
//   Unreachable. China (Gallatin) ABS issuer is not in the Public ValidIssuers list
//   and this deployment is not on China cloud.
//
// AzureBotServiceOnly:
//   Intentionally not enabled. Official sample default is false. Enabling it would
//   accept only the classic Bot Framework issuer and reject Entra-issued ABS channel
//   tokens from the Microsoft Bot Service tenants listed above.
//
// AllowedCallers + OnTokenValidated:
//   Not configured for this POC (no agent-to-agent callers). With an empty/absent
//   AllowedCallers list the original handler is a no-op for ABS traffic.
//
// Configurable AzureBotServiceOpenIdMetadataUrl / OpenIdMetadataUrl / OpenIdMetadataRefresh:
//   Never set in this POC. Hardcoded Public Cloud defaults are identical to the
//   sample's IsGov=false fallback path.
//
// Configurable AzureBotServiceTokenHandling:
//   Sample default is true and is required so ABS tokens resolve signing keys from
//   login.botframework.com. Hardcoded enabled to preserve that semantics without a
//   config switch that could disable ABS metadata handling.
//
// Configurable ValidIssuers override:
//   Never set in this POC. Public-cloud defaults above match the sample when
//   ValidIssuers is omitted.
