using System.Security.Claims;
using System.Text;
using ApprovalGateway.AzureDevOps;
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

public sealed class AdoApprovalPendingHandlerTests
{
    [Fact]
    public async Task HandleAsync_WrongEnvironment_IsIgnored()
    {
        var store = new InMemoryPocConversationReferenceStore();
        var handler = CreateHandler(store);

        AdoApprovalPendingHandleResult result = await handler.HandleAsync(
            new AdoApprovalPendingEvent
            {
                EventType = AdoServiceHookDefaults.ApprovalPendingEventType,
                EnvironmentName = "hml-other",
                ApprovalId = "a1",
            },
            CancellationToken.None);

        Assert.Equal("ignored", result.Status);
        Assert.Equal("environment_filter", result.Outcome);
    }

    [Fact]
    public async Task HandleAsync_TargetEnvironmentWithoutConversation_AcceptedSkipped()
    {
        var store = new InMemoryPocConversationReferenceStore();
        var handler = CreateHandler(store);

        AdoApprovalPendingHandleResult result = await handler.HandleAsync(
            CreateTargetEvent(),
            CancellationToken.None);

        Assert.Equal("accepted", result.Status);
        Assert.Equal("notify_skipped_no_conversation", result.Outcome);
    }

    [Fact]
    public async Task HandleAsync_TargetEnvironmentWithConversation_Notifies()
    {
        var store = new InMemoryPocConversationReferenceStore();
        store.Save(CreateReference());

        var adapter = new Mock<IChannelAdapter>(MockBehavior.Strict);
        adapter
            .Setup(a => a.ContinueConversationAsync(
                It.IsAny<ClaimsIdentity>(),
                It.IsAny<ConversationReference>(),
                It.IsAny<AgentCallbackHandler>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(store, adapter.Object);

        AdoApprovalPendingHandleResult result = await handler.HandleAsync(
            CreateTargetEvent(),
            CancellationToken.None);

        Assert.Equal("accepted", result.Status);
        Assert.Equal("notified", result.Outcome);
        Assert.Equal("a:personal", result.ConversationId);
        adapter.VerifyAll();
    }

    private static AdoApprovalPendingEvent CreateTargetEvent() =>
        new()
        {
            EventType = AdoServiceHookDefaults.ApprovalPendingEventType,
            EnvironmentName = AdoServiceHookDefaults.TargetEnvironmentName,
            ApprovalId = "11111111-1111-1111-1111-111111111111",
            StageName = "PRD",
            PipelineName = "poc-teams-approval",
            RunId = 128,
            RunName = "20260825.2",
        };

    private static ConversationReference CreateReference() =>
        new()
        {
            ChannelId = Channels.Msteams,
            ServiceUrl = "https://smba.trafficmanager.net/teams/",
            Conversation = new ConversationAccount
            {
                Id = "a:personal",
                ConversationType = ConversationTypes.Personal,
            },
            Agent = new ChannelAccount { Id = "bot-1" },
        };

    private static AdoApprovalPendingHandler CreateHandler(
        IPocConversationReferenceStore store,
        IChannelAdapter? adapter = null)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MicrosoftAppId"] = "5936429a-7889-45c1-983e-d9064aa7ee84",
            })
            .Build();

        var messenger = new PocProactiveMessenger(
            adapter ?? new Mock<IChannelAdapter>(MockBehavior.Strict).Object,
            store,
            configuration,
            NullLogger<PocProactiveMessenger>.Instance);

        return new AdoApprovalPendingHandler(
            messenger,
            NullLogger<AdoApprovalPendingHandler>.Instance);
    }
}

public sealed class AdoApprovalPendingFunctionTests
{
    [Fact]
    public async Task AdoApprovalPending_InvalidJson_ReturnsBadRequest()
    {
        var function = CreateFunction(new InMemoryPocConversationReferenceStore());
        DefaultHttpContext context = new();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{bad"));

        IActionResult result = await function.AdoApprovalPending(context.Request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task AdoApprovalPending_WrongEnvironment_ReturnsOkIgnored()
    {
        var function = CreateFunction(new InMemoryPocConversationReferenceStore());
        const string json = """
            {
              "eventType": "ms.vss-pipelinechecks-events.approval-pending",
              "resource": {
                "approval": { "id": "a1", "status": "pending" },
                "resource": { "name": "other-env" }
              }
            }
            """;
        DefaultHttpContext context = new();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));

        IActionResult result = await function.AdoApprovalPending(context.Request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        string payload = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("ignored", payload);
        Assert.Contains("environment_filter", payload);
    }

    private static AdoApprovalPendingFunction CreateFunction(IPocConversationReferenceStore store)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MicrosoftAppId"] = "5936429a-7889-45c1-983e-d9064aa7ee84",
            })
            .Build();

        var messenger = new PocProactiveMessenger(
            new Mock<IChannelAdapter>(MockBehavior.Strict).Object,
            store,
            configuration,
            NullLogger<PocProactiveMessenger>.Instance);

        var handler = new AdoApprovalPendingHandler(
            messenger,
            NullLogger<AdoApprovalPendingHandler>.Instance);

        return new AdoApprovalPendingFunction(
            NullLogger<AdoApprovalPendingFunction>.Instance,
            handler);
    }
}
