using System.Security.Claims;
using System.Text.Json;
using ApprovalGateway.Bot;
using ApprovalGateway.Proactive;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.AdaptiveCards;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApprovalGateway.Tests;

public sealed class ApprovalGatewayAgentTests
{
    [Fact]
    public void PocReplyMessage_IsExpectedValue()
    {
        Assert.Equal("Approval Gateway POC is online.", ApprovalGatewayAgent.PocReplyMessage);
    }

    [Fact]
    public async Task OnMessage_RepliesWithPocOnlineText()
    {
        var reply = await ProcessMessageAsync("ping");

        Assert.NotNull(reply);
        Assert.Equal(ApprovalGatewayAgent.PocReplyMessage, reply.Text);
        Assert.True(reply.Attachments is null || reply.Attachments.Count == 0);
    }

    [Fact]
    public async Task OnMessage_CardCommand_ReturnsAdaptiveCardAttachment()
    {
        var reply = await ProcessMessageAsync("card");

        Assert.NotNull(reply);
        Assert.NotNull(reply.Attachments);
        Assert.Single(reply.Attachments);

        Attachment attachment = reply.Attachments[0];
        Assert.Equal(ContentTypes.AdaptiveCard, attachment.ContentType);
        Assert.NotNull(attachment.Content);

        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(attachment.Content));
        Assert.Equal(PocApprovalCard.SchemaVersion, document.RootElement.GetProperty("version").GetString());

        JsonElement actions = FindActionSetActions(document.RootElement);
        Assert.Equal(2, actions.GetArrayLength());

        var identifiers = actions.EnumerateArray()
            .Select(a => a.GetProperty("data").GetProperty("action").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal) { PocApprovalCard.ApproveAction, PocApprovalCard.RejectAction },
            identifiers);
    }

    [Theory]
    [InlineData("CARD")]
    [InlineData(" Card ")]
    public async Task OnMessage_CardCommand_IsCaseInsensitiveAndTrimmed(string text)
    {
        var reply = await ProcessMessageAsync(text);

        Assert.NotNull(reply);
        Assert.NotNull(reply.Attachments);
        Assert.Single(reply.Attachments);
        Assert.Equal(ContentTypes.AdaptiveCard, reply.Attachments[0].ContentType);
    }

    [Fact]
    public async Task OnActionExecute_Approve_ReturnsAcknowledgementMessage()
    {
        var invokeResponse = await ProcessActionExecuteAsync(PocApprovalCard.ApproveAction);

        Assert.NotNull(invokeResponse);
        Assert.Equal(200, invokeResponse.StatusCode);
        Assert.Equal(ContentTypes.Message, invokeResponse.Type);
        Assert.Equal(PocApprovalCard.AcknowledgementMessage(PocApprovalCard.ApproveAction), invokeResponse.Value?.ToString());
    }

    [Fact]
    public async Task OnActionExecute_Reject_ReturnsAcknowledgementMessage()
    {
        var invokeResponse = await ProcessActionExecuteAsync(PocApprovalCard.RejectAction);

        Assert.NotNull(invokeResponse);
        Assert.Equal(200, invokeResponse.StatusCode);
        Assert.Equal(ContentTypes.Message, invokeResponse.Type);
        Assert.Equal(PocApprovalCard.AcknowledgementMessage(PocApprovalCard.RejectAction), invokeResponse.Value?.ToString());
    }

    [Fact]
    public async Task OnActionExecute_UnknownVerb_ReturnsBadRequest()
    {
        var invokeResponse = await ProcessActionExecuteAsync("explode");

        Assert.NotNull(invokeResponse);
        Assert.Equal(400, invokeResponse.StatusCode);
        Assert.Equal(ContentTypes.Error, invokeResponse.Type);
        Assert.NotEqual(
            PocApprovalCard.AcknowledgementMessage(PocApprovalCard.ApproveAction),
            invokeResponse.Value?.ToString());
        Assert.NotEqual(
            PocApprovalCard.AcknowledgementMessage(PocApprovalCard.RejectAction),
            invokeResponse.Value?.ToString());
    }

    [Fact]
    public void TryReadActionIdentifier_ReadsMinimalActionField()
    {
        Assert.Equal("approve", ApprovalGatewayAgent.TryReadActionIdentifier(new { action = "approve" }));
        Assert.Null(ApprovalGatewayAgent.TryReadActionIdentifier(new { other = "x" }));
        Assert.Null(ApprovalGatewayAgent.TryReadActionIdentifier(null));
    }

    [Fact]
    public async Task OnMessage_PersonalTeams_CapturesConversationReference()
    {
        var store = new InMemoryPocConversationReferenceStore();
        var adapter = new TestAdapter();
        var agent = CreateAgent(store);
        var activity = MessageFactory.Text("hello");
        PopulatePersonalTeamsConversation(activity);

        await adapter.ProcessActivityAsync(
            new ClaimsIdentity(),
            activity,
            async (turnContext, cancellationToken) => await agent.OnTurnAsync(turnContext, cancellationToken),
            CancellationToken.None);

        ConversationReference? reference = await store.TryGetAsync();
        Assert.NotNull(reference);
        Assert.False(string.IsNullOrWhiteSpace(reference.Conversation?.Id));
        Assert.False(string.IsNullOrWhiteSpace(reference.ServiceUrl));
        Assert.False(string.IsNullOrWhiteSpace(reference.Agent?.Id));
        Assert.Equal(Channels.Msteams, reference.ChannelId);
    }

    [Fact]
    public async Task OnMessage_NonTeamsChannel_DoesNotCaptureConversationReference()
    {
        var store = new InMemoryPocConversationReferenceStore();
        _ = await ProcessMessageAsync("ping", store);

        Assert.Null(await store.TryGetAsync());
    }

    private static async Task<IActivity?> ProcessMessageAsync(
        string text,
        IPocConversationReferenceStore? store = null)
    {
        var adapter = new TestAdapter();
        var agent = CreateAgent(store);
        var activity = MessageFactory.Text(text);
        PopulateConversation(activity);

        await adapter.ProcessActivityAsync(
            new ClaimsIdentity(),
            activity,
            async (turnContext, cancellationToken) => await agent.OnTurnAsync(turnContext, cancellationToken),
            CancellationToken.None);

        return adapter.GetNextReply();
    }

    private static async Task<AdaptiveCardInvokeResponse?> ProcessActionExecuteAsync(string verb)
    {
        var adapter = new TestAdapter();
        var agent = CreateAgent();
        var activity = new Activity
        {
            Type = ActivityTypes.Invoke,
            Name = AdaptiveCardsInvokeNames.ACTION_INVOKE_NAME,
            Value = new AdaptiveCardInvokeValue
            {
                Action = new AdaptiveCardInvokeAction
                {
                    Type = "Action.Execute",
                    Verb = verb,
                    Data = new { action = verb },
                },
                Trigger = "manual",
            },
        };
        PopulateConversation(activity);

        await adapter.ProcessActivityAsync(
            new ClaimsIdentity(),
            activity,
            async (turnContext, cancellationToken) => await agent.OnTurnAsync(turnContext, cancellationToken),
            CancellationToken.None);

        var reply = adapter.GetNextReply();
        Assert.NotNull(reply);
        Assert.Equal(ActivityTypes.InvokeResponse, reply.Type);

        var envelope = JsonSerializer.Deserialize<InvokeResponse>(JsonSerializer.Serialize(reply.Value));
        Assert.NotNull(envelope);
        Assert.NotNull(envelope.Body);

        // Agents SDK 1.6.150 mirrors AdaptiveCardInvokeResponse.StatusCode onto the InvokeResponse envelope.
        return JsonSerializer.Deserialize<AdaptiveCardInvokeResponse>(JsonSerializer.Serialize(envelope.Body));
    }

    private static ApprovalGatewayAgent CreateAgent(IPocConversationReferenceStore? store = null)
    {
        var options = new AgentApplicationOptions(() => new TurnState());
        return new ApprovalGatewayAgent(
            options,
            NullLogger<ApprovalGatewayAgent>.Instance,
            store ?? new InMemoryPocConversationReferenceStore());
    }

    private static void PopulateConversation(IActivity activity)
    {
        activity.From = new ChannelAccount { Id = "user-1" };
        activity.Recipient = new ChannelAccount { Id = "bot-1" };
        activity.Conversation = new ConversationAccount { Id = "conversation-1" };
        activity.ChannelId = Channels.Test;
    }

    private static void PopulatePersonalTeamsConversation(IActivity activity)
    {
        activity.From = new ChannelAccount { Id = "user-1" };
        activity.Recipient = new ChannelAccount { Id = "bot-1" };
        activity.Conversation = new ConversationAccount
        {
            Id = "personal-conversation-1",
            ConversationType = ConversationTypes.Personal,
            IsGroup = false,
        };
        activity.ChannelId = Channels.Msteams;
        activity.ServiceUrl = "https://smba.trafficmanager.net/teams/";
    }

    private static JsonElement FindActionSetActions(JsonElement root)
    {
        foreach (JsonElement item in root.GetProperty("body").EnumerateArray())
        {
            if (item.TryGetProperty("type", out JsonElement type) && type.GetString() == "ActionSet")
            {
                return item.GetProperty("actions");
            }
        }

        throw new InvalidOperationException("ActionSet not found in Adaptive Card body.");
    }
}
