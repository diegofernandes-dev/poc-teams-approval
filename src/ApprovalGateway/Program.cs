using ApprovalGateway.Configuration;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// FunctionsApplicationBuilder does not load appsettings.json by default (unlike WebApplication).
// Use BaseDirectory so Flex Consumption finds the file next to the worker assembly.
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

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
// SDK registers IAgentHttpAdapter via GetService<CloudAdapter>(), which can yield null.
// Prefer GetRequiredService so worker constructor injection fails fast if the adapter is missing.
builder.Services.AddSingleton<IAgentHttpAdapter>(sp => sp.GetRequiredService<CloudAdapter>());
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

builder.UseMiddleware<ApprovalGateway.Functions.BotAuthenticationMiddleware>();

var appId = builder.Configuration[BotConfiguration.MicrosoftAppIdKey]
    ?? builder.Configuration[BotConfiguration.ClientIdKey];
var tenantId = builder.Configuration[BotConfiguration.MicrosoftAppTenantIdKey]
    ?? builder.Configuration[BotConfiguration.TokenValidationTenantIdKey];

var host = builder.Build();

var startupLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ApprovalGateway.Startup");
if (startupLogger.IsEnabled(LogLevel.Information))
{
    startupLogger.LogInformation(
        "Approval Gateway starting. MicrosoftAppId={MicrosoftAppId} MicrosoftAppTenantId={MicrosoftAppTenantId}",
        appId,
        tenantId);
}

host.Run();
