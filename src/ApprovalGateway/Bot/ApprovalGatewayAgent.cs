using ApprovalGateway.Bot;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;

namespace ApprovalGateway.Bot;

public sealed class ApprovalGatewayAgent : AgentApplication
{
    public const string PocReplyMessage = "Approval Gateway POC is online.";

    private readonly ILogger<ApprovalGatewayAgent> _logger;

    public ApprovalGatewayAgent(AgentApplicationOptions options, ILogger<ApprovalGatewayAgent> logger)
        : base(options)
    {
        _logger = logger;

        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
        OnTurnError(OnTurnErrorAsync);
    }

    private Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        ActivityLogging.LogActivityMetadata(_logger, turnContext.Activity);

        foreach (ChannelAccount member in turnContext.Activity.MembersAdded ?? [])
        {
            if (member.Id != turnContext.Activity.Recipient?.Id)
            {
                return turnContext.SendActivityAsync(MessageFactory.Text(PocReplyMessage), cancellationToken);
            }
        }

        return Task.CompletedTask;
    }

    private Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        ActivityLogging.LogActivityMetadata(_logger, turnContext.Activity);
        return turnContext.SendActivityAsync(MessageFactory.Text(PocReplyMessage), cancellationToken);
    }

    private async Task OnTurnErrorAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(
            exception,
            "Unhandled agent error. ActivityType={ActivityType} ActivityId={ActivityId}",
            turnContext.Activity.Type,
            turnContext.Activity.Id);

        var endOfConversation = Activity.CreateEndOfConversationActivity();
        endOfConversation.Code = EndOfConversationCodes.Error;
        endOfConversation.Text = "An unexpected error occurred while processing the activity.";
        await turnContext.SendActivityAsync(endOfConversation, cancellationToken);
    }
}
