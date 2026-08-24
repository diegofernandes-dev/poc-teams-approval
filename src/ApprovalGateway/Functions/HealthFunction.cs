using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace ApprovalGateway.Functions;

public sealed class HealthFunction
{
    [Function(nameof(Health))]
    public IActionResult Health(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest req)
    {
        return new ContentResult
        {
            Content = JsonSerializer.Serialize(new { status = "ok" }),
            ContentType = "application/json",
            StatusCode = (int)HttpStatusCode.OK,
        };
    }
}
