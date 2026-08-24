using ApprovalGateway.Functions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ApprovalGateway.Tests;

public sealed class BotMessagesFunctionTests
{
    [Fact]
    public async Task BotMessages_UnsupportedContentType_ReturnsUnsupportedMediaType()
    {
        var function = new BotMessagesFunction(
            NullLogger<BotMessagesFunction>.Instance,
            Mock.Of<Microsoft.Agents.Hosting.AspNetCore.IAgentHttpAdapter>(),
            Mock.Of<Microsoft.Agents.Builder.IAgent>());
        var context = new DefaultHttpContext();
        context.Request.ContentType = "text/plain";
        context.Request.Body = new MemoryStream("hello"u8.ToArray());

        var result = await function.BotMessages(context.Request, CancellationToken.None);

        Assert.IsType<UnsupportedMediaTypeResult>(result);
    }
}
