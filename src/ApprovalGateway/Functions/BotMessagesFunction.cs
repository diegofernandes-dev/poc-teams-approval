using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ApprovalGateway.Functions;

public sealed class BotMessagesFunction
{
    private readonly ILogger<BotMessagesFunction> _logger;

    public BotMessagesFunction(ILogger<BotMessagesFunction> logger)
    {
        _logger = logger;
    }

    [Function(nameof(BotMessages))]
    public async Task<IActionResult> BotMessages(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "messages")] HttpRequest req,
        [FromServices] IAgentHttpAdapter adapter,
        [FromServices] IAgent agent,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received Bot Framework message request.");

        if (!req.HasJsonContentType())
        {
            _logger.LogWarning("Rejected Bot message request with unsupported content type {ContentType}.", req.ContentType);
            return new UnsupportedMediaTypeResult();
        }

        await adapter.ProcessAsync(req, req.HttpContext.Response, agent, cancellationToken);
        return new EmptyResult();
    }
}
