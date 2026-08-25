using System.Security.Claims;
using ApprovalGateway.Functions;
using ApprovalGateway.Proactive;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ApprovalGateway.Tests;

public sealed class PocProactiveFunctionTests
{
    [Fact]
    public async Task PocProactive_NoConversationReference_ReturnsNotFound()
    {
        var store = new InMemoryPocConversationReferenceStore();
        var function = new PocProactiveFunction(
            NullLogger<PocProactiveFunction>.Instance,
            CreateMessenger(store));

        IActionResult result = await function.PocProactive(
            new DefaultHttpContext().Request,
            CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    [Fact]
    public async Task PocProactive_WithCapturedReference_ReturnsOk()
    {
        var store = new InMemoryPocConversationReferenceStore();
        await store.SaveAsync(new ConversationReference
        {
            ChannelId = Channels.Msteams,
            ServiceUrl = "https://smba.trafficmanager.net/teams/",
            Conversation = new ConversationAccount
            {
                Id = "a:personal",
                ConversationType = ConversationTypes.Personal,
            },
            Agent = new ChannelAccount { Id = "bot-1" },
        });

        var adapter = new Mock<IChannelAdapter>(MockBehavior.Strict);
        adapter
            .Setup(a => a.ContinueConversationAsync(
                It.IsAny<ClaimsIdentity>(),
                It.IsAny<ConversationReference>(),
                It.IsAny<AgentCallbackHandler>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var function = new PocProactiveFunction(
            NullLogger<PocProactiveFunction>.Instance,
            CreateMessenger(store, adapter.Object));

        IActionResult result = await function.PocProactive(
            new DefaultHttpContext().Request,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
    }

    private static PocProactiveMessenger CreateMessenger(
        IPocConversationReferenceStore store,
        IChannelAdapter? adapter = null)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MicrosoftAppId"] = "5936429a-7889-45c1-983e-d9064aa7ee84",
            })
            .Build();

        return new PocProactiveMessenger(
            adapter ?? new Mock<IChannelAdapter>(MockBehavior.Strict).Object,
            store,
            configuration,
            NullLogger<PocProactiveMessenger>.Instance);
    }
}
