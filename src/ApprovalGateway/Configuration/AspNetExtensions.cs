// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// Adapted from https://github.com/microsoft/Agents/blob/main/samples/dotnet/quickstart/AspNetExtensions.cs

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

        if (validationOptions.ValidIssuers == null || validationOptions.ValidIssuers.Count == 0)
        {
            if (validationOptions.AzureBotServiceOnly)
            {
                validationOptions.ValidIssuers =
                [
                    validationOptions.IsGov
                        ? AuthenticationConstants.GovBotFrameworkTokenIssuer
                        : AuthenticationConstants.BotFrameworkTokenIssuer
                ];
            }
            else if (validationOptions.IsGov)
            {
                validationOptions.ValidIssuers =
                [
                    AuthenticationConstants.GovBotFrameworkTokenIssuer,
                    "https://sts.windows.net/cab8a31a-1906-4287-a0d8-4eef66b95f6e/",
                    "https://login.microsoftonline.us/cab8a31a-1906-4287-a0d8-4eef66b95f6e/v2.0"
                ];

                if (!string.IsNullOrEmpty(validationOptions.TenantId) && Guid.TryParse(validationOptions.TenantId, out _))
                {
                    validationOptions.ValidIssuers.Add(string.Format(CultureInfo.InvariantCulture, AuthenticationConstants.ValidTokenIssuerUrlTemplateV1, validationOptions.TenantId));
                    validationOptions.ValidIssuers.Add(string.Format(CultureInfo.InvariantCulture, AuthenticationConstants.ValidGovernmentTokenIssuerUrlTemplateV2, validationOptions.TenantId));
                }
            }
            else
            {
                validationOptions.ValidIssuers =
                [
                    AuthenticationConstants.BotFrameworkTokenIssuer,
                    "https://sts.windows.net/d6d49420-f39b-4df7-a1dc-d59a935871db/",
                    "https://login.microsoftonline.com/d6d49420-f39b-4df7-a1dc-d59a935871db/v2.0",
                    "https://sts.windows.net/f8cdef31-a31e-4b4a-93e4-5f571e91255a/",
                    "https://login.microsoftonline.com/f8cdef31-a31e-4b4a-93e4-5f571e91255a/v2.0",
                    "https://sts.windows.net/69e9b82d-4842-4902-8d1e-abc5b98a55e8/",
                    "https://login.microsoftonline.com/69e9b82d-4842-4902-8d1e-abc5b98a55e8/v2.0",
                ];

                if (!string.IsNullOrEmpty(validationOptions.TenantId) && Guid.TryParse(validationOptions.TenantId, out _))
                {
                    validationOptions.ValidIssuers.Add(string.Format(CultureInfo.InvariantCulture, AuthenticationConstants.ValidTokenIssuerUrlTemplateV1, validationOptions.TenantId));
                    validationOptions.ValidIssuers.Add(string.Format(CultureInfo.InvariantCulture, AuthenticationConstants.ValidTokenIssuerUrlTemplateV2, validationOptions.TenantId));
                }
            }
        }

        if (string.IsNullOrEmpty(validationOptions.AzureBotServiceOpenIdMetadataUrl))
        {
            validationOptions.AzureBotServiceOpenIdMetadataUrl = validationOptions.IsGov
                ? AuthenticationConstants.GovAzureBotServiceOpenIdMetadataUrl
                : AuthenticationConstants.PublicAzureBotServiceOpenIdMetadataUrl;
        }

        if (string.IsNullOrEmpty(validationOptions.OpenIdMetadataUrl))
        {
            validationOptions.OpenIdMetadataUrl = validationOptions.IsGov
                ? AuthenticationConstants.GovOpenIdMetadataUrl
                : AuthenticationConstants.PublicOpenIdMetadataUrl;
        }

        var openIdMetadataRefresh = validationOptions.OpenIdMetadataRefresh ?? BaseConfigurationManager.DefaultAutomaticRefreshInterval;

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
                    ValidIssuers = validationOptions.ValidIssuers,
                    ValidAudiences = validationOptions.Audiences,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                };

                options.TokenValidationParameters.EnableAadSigningKeyIssuerValidation();

                options.Events = new JwtBearerEvents
                {
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

                        if (validationOptions.AzureBotServiceTokenHandling && IsBotFrameworkIssuer(issuer))
                        {
                            context.Options.TokenValidationParameters.ConfigurationManager = OpenIdMetadataCache.GetOrAdd(
                                validationOptions.AzureBotServiceOpenIdMetadataUrl,
                                _ => new ConfigurationManager<OpenIdConnectConfiguration>(
                                    validationOptions.AzureBotServiceOpenIdMetadataUrl,
                                    new OpenIdConnectConfigurationRetriever(),
                                    new HttpClient())
                                {
                                    AutomaticRefreshInterval = openIdMetadataRefresh
                                });
                        }
                        else
                        {
                            context.Options.TokenValidationParameters.ConfigurationManager = OpenIdMetadataCache.GetOrAdd(
                                validationOptions.OpenIdMetadataUrl,
                                _ => new ConfigurationManager<OpenIdConnectConfiguration>(
                                    validationOptions.OpenIdMetadataUrl,
                                    new OpenIdConnectConfigurationRetriever(),
                                    new HttpClient())
                                {
                                    AutomaticRefreshInterval = openIdMetadataRefresh
                                });
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var issuer = context.Principal?.FindFirst("iss")?.Value;
                        var isBotFrameworkToken = validationOptions.AzureBotServiceTokenHandling
                            && issuer != null
                            && IsBotFrameworkIssuer(issuer);

                        if (!isBotFrameworkToken
                            && validationOptions.AllowedCallers != null
                            && validationOptions.AllowedCallers.Count > 0
                            && !validationOptions.AllowedCallers.Any(c => c.Equals("*", StringComparison.Ordinal)))
                        {
                            var callerAppId = context.Principal?.FindFirst("azp")?.Value
                                ?? context.Principal?.FindFirst("appid")?.Value;

                            if (string.IsNullOrEmpty(callerAppId)
                                || !validationOptions.AllowedCallers.Any(c => c.Equals(callerAppId, StringComparison.OrdinalIgnoreCase)))
                            {
                                context.Fail($"Caller App ID '{callerAppId}' is not in the AllowedCallers list.");
                            }
                        }

                        return Task.CompletedTask;
                    },
                };
            });
    }

    private static bool IsBotFrameworkIssuer(string issuer)
    {
        return AuthenticationConstants.BotFrameworkTokenIssuer.Equals(issuer, StringComparison.OrdinalIgnoreCase)
            || AuthenticationConstants.GovBotFrameworkTokenIssuer.Equals(issuer, StringComparison.OrdinalIgnoreCase)
            || AuthenticationConstants.ChinaBotFrameworkTokenIssuer.Equals(issuer, StringComparison.OrdinalIgnoreCase);
    }

    public sealed class TokenValidationOptions
    {
        public IList<string>? Audiences { get; set; }

        public string? TenantId { get; set; }

        public IList<string>? ValidIssuers { get; set; }

        public bool IsGov { get; set; }

        public bool AzureBotServiceOnly { get; set; }

        public string? AzureBotServiceOpenIdMetadataUrl { get; set; }

        public string? OpenIdMetadataUrl { get; set; }

        public bool AzureBotServiceTokenHandling { get; set; } = true;

        public TimeSpan? OpenIdMetadataRefresh { get; set; }

        public IList<string>? AllowedCallers { get; set; }
    }
}
