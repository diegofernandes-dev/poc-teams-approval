using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ApprovalGateway.Functions;

public sealed class BotMessagesFunction
{
    private readonly ILogger<BotMessagesFunction> _logger;
    private readonly IAgentHttpAdapter _adapter;
    private readonly IAgent _agent;

    public BotMessagesFunction(
        ILogger<BotMessagesFunction> logger,
        IAgentHttpAdapter adapter,
        IAgent agent)
    {
        _logger = logger;
        _adapter = adapter;
        _agent = agent;
    }

    [Function(nameof(BotMessages))]
    public async Task<IActionResult> BotMessages(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "messages")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received Bot Framework message request.");

        if (!req.HasJsonContentType())
        {
            _logger.LogWarning("Rejected Bot message request with unsupported content type {ContentType}.", req.ContentType);
            return new UnsupportedMediaTypeResult();
        }

        // Isolated worker resolves DI via constructor injection, not [FromServices].
        // HttpContext is provided by ConfigureFunctionsWebApplication / AspNetCore integration.
        var httpContext = req.HttpContext
            ?? throw new InvalidOperationException("HttpRequest.HttpContext is null; AspNetCore Functions integration is required.");

        await _adapter.ProcessAsync(httpContext.Request, httpContext.Response, _agent, cancellationToken);
        return new EmptyResult();
    }
}
