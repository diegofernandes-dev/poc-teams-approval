using System.Text.Json;
using ApprovalGateway.Functions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ApprovalGateway.Tests;

public sealed class HealthFunctionTests
{
    [Fact]
    public void Health_ReturnsOkStatusPayload()
    {
        var function = new HealthFunction();
        var context = new DefaultHttpContext();

        var result = function.Health(context.Request);

        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal(200, contentResult.StatusCode);
        Assert.Equal("application/json", contentResult.ContentType);

        using var document = JsonDocument.Parse(contentResult.Content!);
        Assert.Equal("ok", document.RootElement.GetProperty("status").GetString());
    }
}
