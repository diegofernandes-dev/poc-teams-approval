using System.Text;
using ApprovalGateway.AzureDevOps;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ApprovalGateway.Functions;

/// <summary>
/// Receives Azure DevOps Service Hook notifications for Environment approval-pending.
/// Protected by Function key. Does not approve or reject in Azure DevOps.
/// </summary>
public sealed class AdoApprovalPendingFunction
{
    private readonly ILogger<AdoApprovalPendingFunction> _logger;
    private readonly AdoApprovalPendingHandler _handler;

    public AdoApprovalPendingFunction(
        ILogger<AdoApprovalPendingFunction> logger,
        AdoApprovalPendingHandler handler)
    {
        _logger = logger;
        _handler = handler;
    }

    [Function(nameof(AdoApprovalPending))]
    public async Task<IActionResult> AdoApprovalPending(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "webhooks/ado/approval-pending")]
        HttpRequest req,
        CancellationToken cancellationToken)
    {
        string body;
        using (var reader = new StreamReader(req.Body, Encoding.UTF8))
        {
            body = await reader.ReadToEndAsync(cancellationToken);
        }

        _logger.LogInformation("Received Azure DevOps approval-pending Service Hook POST.");

        if (!AdoApprovalPendingPayloadParser.TryParse(body, out AdoApprovalPendingEvent? pendingEvent, out string? error)
            || pendingEvent is null)
        {
            _logger.LogWarning("Rejected approval-pending payload. Error={Error}", error);
            return new BadRequestObjectResult(new
            {
                status = "error",
                error = error ?? "Unable to parse request body.",
            });
        }

        AdoApprovalPendingHandleResult result = await _handler.HandleAsync(pendingEvent, cancellationToken);

        // Always acknowledge accepted/ignored payloads with 200 so Service Hook delivery
        // succeeds even when Teams notification fails — approval stays pending in Azure DevOps.
        return new OkObjectResult(new
        {
            status = result.Status,
            outcome = result.Outcome,
            approvalId = pendingEvent.ApprovalId,
            environment = pendingEvent.EnvironmentName,
            conversationId = result.ConversationId,
            error = result.Error,
        });
    }
}
