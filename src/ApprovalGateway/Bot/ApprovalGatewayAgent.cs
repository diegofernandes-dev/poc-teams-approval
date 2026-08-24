using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.AdaptiveCards;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;

namespace ApprovalGateway.Bot;

public sealed class ApprovalGatewayAgent : AgentApplication
{
    public const string PocReplyMessage = "Approval Gateway POC is online.";

    private static readonly Regex AnyVerbPattern = new(".*", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly ILogger<ApprovalGatewayAgent> _logger;

    public ApprovalGatewayAgent(AgentApplicationOptions options, ILogger<ApprovalGatewayAgent> logger)
        : base(options)
    {
        _logger = logger;

        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
        AdaptiveCards.OnActionExecute(PocApprovalCard.ApproveAction, OnApproveActionAsync);
        AdaptiveCards.OnActionExecute(PocApprovalCard.RejectAction, OnRejectActionAsync);
        // Catch-all registered after specific verbs so unknown Action.Execute is safely rejected.
        // Agents SDK 1.6.150 has no rank overload on OnActionExecute; first matching route wins.
        AdaptiveCards.OnActionExecute(AnyVerbPattern, OnUnknownActionExecuteAsync);
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

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        ActivityLogging.LogActivityMetadata(_logger, turnContext.Activity);

        if (IsCardCommand(turnContext.Activity.Text))
        {
            _logger.LogInformation("Adaptive Card requested. Command={Command}", PocApprovalCard.CardCommand);

            var activity = MessageFactory.Attachment(PocApprovalCard.CreateAttachment());
            await turnContext.SendActivityAsync(activity, cancellationToken);

            _logger.LogInformation(
                "Adaptive Card sent. SchemaVersion={SchemaVersion} ActionCount={ActionCount}",
                PocApprovalCard.SchemaVersion,
                2);
            return;
        }

        await turnContext.SendActivityAsync(MessageFactory.Text(PocReplyMessage), cancellationToken);
    }

    private Task<AdaptiveCardInvokeResponse> OnApproveActionAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        object data,
        CancellationToken cancellationToken)
    {
        return HandleKnownActionAsync(turnContext, PocApprovalCard.ApproveAction, data);
    }

    private Task<AdaptiveCardInvokeResponse> OnRejectActionAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        object data,
        CancellationToken cancellationToken)
    {
        return HandleKnownActionAsync(turnContext, PocApprovalCard.RejectAction, data);
    }

    private Task<AdaptiveCardInvokeResponse> OnUnknownActionExecuteAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        object data,
        CancellationToken cancellationToken)
    {
        ActivityLogging.LogActivityMetadata(_logger, turnContext.Activity);

        string? reportedAction = TryReadActionIdentifier(data);
        _logger.LogWarning(
            "Adaptive Card action received with unsupported verb/action. NormalizedAction={NormalizedAction}",
            reportedAction ?? "(none)");

        // Untrusted payload must not drive approval decisions. Reject unknown actions safely.
        return Task.FromResult(AdaptiveCardInvokeResponseFactory.BadRequest("Unsupported Adaptive Card action."));
    }

    private Task<AdaptiveCardInvokeResponse> HandleKnownActionAsync(
        ITurnContext turnContext,
        string expectedAction,
        object data)
    {
        ActivityLogging.LogActivityMetadata(_logger, turnContext.Activity);

        // data.action is untrusted POC input used only for acknowledgement identity checks.
        string? payloadAction = TryReadActionIdentifier(data);
        if (payloadAction is not null &&
            !string.Equals(payloadAction, expectedAction, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Adaptive Card action payload mismatched verb. ExpectedAction={ExpectedAction} PayloadAction={PayloadAction}",
                expectedAction,
                payloadAction);
            return Task.FromResult(AdaptiveCardInvokeResponseFactory.BadRequest("Adaptive Card action payload mismatch."));
        }

        _logger.LogInformation(
            "Adaptive Card action received. NormalizedAction={NormalizedAction}",
            expectedAction);

        return Task.FromResult(
            AdaptiveCardInvokeResponseFactory.Message(PocApprovalCard.AcknowledgementMessage(expectedAction)));
    }

    public static bool IsCardCommand(string? text) =>
        string.Equals(text?.Trim(), PocApprovalCard.CardCommand, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads the minimal untrusted <c>action</c> identifier from Action.Execute data, if present.
    /// </summary>
    public static string? TryReadActionIdentifier(object? data)
    {
        if (data is null)
        {
            return null;
        }

        try
        {
            if (data is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty("action", out JsonElement actionElement) &&
                    actionElement.ValueKind == JsonValueKind.String)
                {
                    return actionElement.GetString();
                }

                return null;
            }

            using var document = JsonDocument.Parse(JsonSerializer.Serialize(data));
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("action", out JsonElement property) &&
                property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
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
