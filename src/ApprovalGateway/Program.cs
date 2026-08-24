using ApprovalGateway.Configuration;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

var configurationOverrides = BotConfiguration.MapMicrosoftAppSettings(builder.Configuration);
if (configurationOverrides.Count > 0)
{
    builder.Configuration.AddInMemoryCollection(configurationOverrides);
}

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Services.AddHttpClient();
builder.AddAgentApplicationOptions();
builder.AddAgent<ApprovalGateway.Bot.ApprovalGatewayAgent>();
builder.Services.AddSingleton<IStorage, MemoryStorage>();
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

builder.UseMiddleware<ApprovalGateway.Functions.BotAuthenticationMiddleware>();

var appId = builder.Configuration[BotConfiguration.MicrosoftAppIdKey]
    ?? builder.Configuration[BotConfiguration.ClientIdKey];
var tenantId = builder.Configuration[BotConfiguration.MicrosoftAppTenantIdKey]
    ?? builder.Configuration[BotConfiguration.TokenValidationTenantIdKey];

var host = builder.Build();

var startupLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ApprovalGateway.Startup");
startupLogger.LogInformation(
    "Approval Gateway starting. MicrosoftAppId={MicrosoftAppId} MicrosoftAppTenantId={MicrosoftAppTenantId}",
    appId,
    tenantId);

host.Run();
