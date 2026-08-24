using System.Security.Claims;
using ApprovalGateway.Bot;
using Microsoft.Agents.Builder.App;
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
        var adapter = new TestAdapter();
        var options = new AgentApplicationOptions(() => new TurnState());
        var agent = new ApprovalGatewayAgent(options, NullLogger<ApprovalGatewayAgent>.Instance);
        var activity = MessageFactory.Text("ping");
        activity.From = new ChannelAccount { Id = "user-1" };
        activity.Recipient = new ChannelAccount { Id = "bot-1" };
        activity.Conversation = new ConversationAccount { Id = "conversation-1" };
        activity.ChannelId = Channels.Test;

        await adapter.ProcessActivityAsync(
            new ClaimsIdentity(),
            activity,
            async (turnContext, cancellationToken) => await agent.OnTurnAsync(turnContext, cancellationToken),
            CancellationToken.None);

        var reply = adapter.GetNextReply();
        Assert.NotNull(reply);
        Assert.Equal(ApprovalGatewayAgent.PocReplyMessage, reply.Text);
    }
}
