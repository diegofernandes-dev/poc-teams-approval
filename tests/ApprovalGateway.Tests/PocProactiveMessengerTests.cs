using System.Security.Claims;
using ApprovalGateway.Configuration;
using ApprovalGateway.Proactive;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ApprovalGateway.Tests;

public sealed class PocProactiveMessengerTests
{
    private const string AppId = "5936429a-7889-45c1-983e-d9064aa7ee84";

    [Fact]
    public async Task SendAsync_NoCapturedConversation_ReturnsSafeErrorWithoutCallingAdapter()
    {
        var adapter = new Mock<IChannelAdapter>(MockBehavior.Strict);
        var store = new InMemoryPocConversationReferenceStore();
        var messenger = CreateMessenger(adapter.Object, store);

        PocProactiveSendResult result = await messenger.SendAsync(CancellationToken.None);

        Assert.Equal(PocProactiveSendStatus.NoConversationReference, result.Status);
        Assert.Contains("No Teams personal conversation reference", result.Error);
        adapter.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendAsync_UsesStoredConversationReference()
    {
        var reference = CreatePersonalReference();
        var store = new InMemoryPocConversationReferenceStore();
        store.Save(reference);

        ConversationReference? continuedReference = null;
        string? continuedAudience = null;
        var adapter = new Mock<IChannelAdapter>(MockBehavior.Strict);
        adapter
            .Setup(a => a.ContinueConversationAsync(
                It.IsAny<ClaimsIdentity>(),
                It.IsAny<ConversationReference>(),
                It.IsAny<AgentCallbackHandler>(),
                It.IsAny<CancellationToken>()))
            .Returns<ClaimsIdentity, ConversationReference, AgentCallbackHandler, CancellationToken>(
                async (identity, conversationReference, callback, cancellationToken) =>
                {
                    continuedAudience = identity.FindFirst("aud")?.Value;
                    continuedReference = conversationReference;
                    var turnContext = new Mock<ITurnContext>(MockBehavior.Strict);
                    turnContext
                        .Setup(c => c.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new ResourceResponse { Id = "proactive-1" });
                    await callback(turnContext.Object, cancellationToken);
                });

        var messenger = CreateMessenger(adapter.Object, store);
        PocProactiveSendResult result = await messenger.SendAsync(CancellationToken.None);

        Assert.Equal(PocProactiveSendStatus.Succeeded, result.Status);
        Assert.Equal(reference.Conversation!.Id, result.ConversationId);
        Assert.Equal(AppId, continuedAudience);
        Assert.Same(reference, continuedReference);
        adapter.Verify(
            a => a.ContinueConversationAsync(
                It.IsAny<ClaimsIdentity>(),
                reference,
                It.IsAny<AgentCallbackHandler>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_SendsExpectedProactiveText()
    {
        var reference = CreatePersonalReference();
        var store = new InMemoryPocConversationReferenceStore();
        store.Save(reference);

        IActivity? sentActivity = null;
        var adapter = new Mock<IChannelAdapter>(MockBehavior.Strict);
        adapter
            .Setup(a => a.ContinueConversationAsync(
                It.IsAny<ClaimsIdentity>(),
                It.IsAny<ConversationReference>(),
                It.IsAny<AgentCallbackHandler>(),
                It.IsAny<CancellationToken>()))
            .Returns<ClaimsIdentity, ConversationReference, AgentCallbackHandler, CancellationToken>(
                async (_, _, callback, cancellationToken) =>
                {
                    var turnContext = new Mock<ITurnContext>(MockBehavior.Strict);
                    turnContext
                        .Setup(c => c.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                        .Callback<IActivity, CancellationToken>((activity, _) => sentActivity = activity)
                        .ReturnsAsync(new ResourceResponse { Id = "proactive-1" });
                    await callback(turnContext.Object, cancellationToken);
                });

        var messenger = CreateMessenger(adapter.Object, store);
        PocProactiveSendResult result = await messenger.SendAsync(CancellationToken.None);

        Assert.Equal(PocProactiveSendStatus.Succeeded, result.Status);
        Assert.NotNull(sentActivity);
        Assert.Equal(PocProactiveMessenger.ProactiveMessage, sentActivity.Text);
    }

    [Fact]
    public void ProactiveMessage_IsExpectedValue()
    {
        Assert.Equal("Proactive Teams notification POC.", PocProactiveMessenger.ProactiveMessage);
    }

    [Fact]
    public void TryCapture_PersonalTeams_StoresRoutingFieldsOnly()
    {
        var store = new InMemoryPocConversationReferenceStore();
        var activity = MessageFactory.Text("secret user text that must not be stored as a body");
        activity.From = new ChannelAccount { Id = "user-1" };
        activity.Recipient = new ChannelAccount { Id = "bot-1" };
        activity.Conversation = new ConversationAccount
        {
            Id = "a:personal-1",
            ConversationType = ConversationTypes.Personal,
            IsGroup = false,
        };
        activity.ChannelId = Channels.Msteams;
        activity.ServiceUrl = "https://smba.trafficmanager.net/amer/";

        bool captured = PocConversationReferenceCapture.TryCapture(
            activity,
            store,
            NullLogger.Instance);

        Assert.True(captured);
        Assert.True(store.TryGet(out ConversationReference? reference));
        Assert.NotNull(reference);
        Assert.Equal("a:personal-1", reference.Conversation?.Id);
        Assert.Equal("https://smba.trafficmanager.net/amer/", reference.ServiceUrl);
        Assert.Equal("bot-1", reference.Agent?.Id);
        Assert.Equal(Channels.Msteams, reference.ChannelId);
        Assert.DoesNotContain("secret user text", reference.Conversation?.Id ?? string.Empty);
    }

    [Fact]
    public void TryCapture_GroupConversation_IsIgnored()
    {
        var store = new InMemoryPocConversationReferenceStore();
        var activity = MessageFactory.Text("hello");
        activity.From = new ChannelAccount { Id = "user-1" };
        activity.Recipient = new ChannelAccount { Id = "bot-1" };
        activity.Conversation = new ConversationAccount
        {
            Id = "19:channel@thread.tacv2",
            ConversationType = "channel",
            IsGroup = true,
        };
        activity.ChannelId = Channels.Msteams;
        activity.ServiceUrl = "https://smba.trafficmanager.net/teams/";

        bool captured = PocConversationReferenceCapture.TryCapture(
            activity,
            store,
            NullLogger.Instance);

        Assert.False(captured);
        Assert.False(store.TryGet(out _));
    }

    private static PocProactiveMessenger CreateMessenger(
        IChannelAdapter adapter,
        IPocConversationReferenceStore store)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [BotConfiguration.MicrosoftAppIdKey] = AppId,
            })
            .Build();

        return new PocProactiveMessenger(
            adapter,
            store,
            configuration,
            NullLogger<PocProactiveMessenger>.Instance);
    }

    private static ConversationReference CreatePersonalReference() =>
        new()
        {
            ChannelId = Channels.Msteams,
            ServiceUrl = "https://smba.trafficmanager.net/teams/",
            Conversation = new ConversationAccount
            {
                Id = "a:personal-conversation",
                ConversationType = ConversationTypes.Personal,
                IsGroup = false,
            },
            Agent = new ChannelAccount { Id = "28:bot" },
            User = new ChannelAccount { Id = "29:user" },
        };
}
