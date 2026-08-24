using ApprovalGateway.Proactive;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ApprovalGateway.Functions;

/// <summary>
/// Temporary POC-only HTTP trigger that sends a proactive Teams personal message.
/// Protected by Function key. Not part of the production approval surface.
/// </summary>
public sealed class PocProactiveFunction
{
    private readonly ILogger<PocProactiveFunction> _logger;
    private readonly PocProactiveMessenger _messenger;

    public PocProactiveFunction(ILogger<PocProactiveFunction> logger, PocProactiveMessenger messenger)
    {
        _logger = logger;
        _messenger = messenger;
    }

    [Function(nameof(PocProactive))]
    public async Task<IActionResult> PocProactive(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "poc/proactive")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        _ = req;
        _logger.LogInformation("Received POC proactive trigger request.");

        PocProactiveSendResult result = await _messenger.SendAsync(cancellationToken);

        return result.Status switch
        {
            PocProactiveSendStatus.Succeeded => new OkObjectResult(new
            {
                status = "ok",
                message = PocProactiveMessenger.ProactiveMessage,
                conversationId = result.ConversationId,
            }),
            PocProactiveSendStatus.NoConversationReference => new NotFoundObjectResult(new
            {
                status = "error",
                error = result.Error,
            }),
            PocProactiveSendStatus.ConfigurationError => new ObjectResult(new
            {
                status = "error",
                error = result.Error,
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError,
            },
            _ => new ObjectResult(new
            {
                status = "error",
                error = "Proactive send failed.",
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError,
            },
        };
    }
}
