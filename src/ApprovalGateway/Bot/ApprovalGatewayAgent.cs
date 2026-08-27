using System.Text.Json;
using System.Text.RegularExpressions;
using ApprovalGateway.AzureDevOps;
using ApprovalGateway.Proactive;
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
    private readonly IPocConversationReferenceStore _conversationReferenceStore;
    private readonly AdoApprovalDecisionService _approvalDecisions;

    public ApprovalGatewayAgent(
        AgentApplicationOptions options,
        ILogger<ApprovalGatewayAgent> logger,
        IPocConversationReferenceStore conversationReferenceStore,
        AdoApprovalDecisionService approvalDecisions)
        : base(options)
    {
        _logger = logger;
        _conversationReferenceStore = conversationReferenceStore;
        _approvalDecisions = approvalDecisions;

        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
        AdaptiveCards.OnActionExecute(PocApprovalCard.ApproveAction, OnApproveActionAsync);
        AdaptiveCards.OnActionExecute(PocApprovalCard.RejectAction, OnRejectActionAsync);
        // Catch-all registered after specific verbs so unknown Action.Execute is safely rejected.
        // Agents SDK 1.6.150 has no rank overload on OnActionExecute; first matching route wins.
        AdaptiveCards.OnActionExecute(AnyVerbPattern, OnUnknownActionExecuteAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
        OnTurnError(OnTurnErrorAsync);
    }

    private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        ActivityLogging.LogActivityMetadata(_logger, turnContext.Activity);
        await CaptureConversationReferenceAsync(turnContext.Activity, cancellationToken);

        foreach (ChannelAccount member in turnContext.Activity.MembersAdded ?? [])
        {
            if (member.Id != turnContext.Activity.Recipient?.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text(PocReplyMessage), cancellationToken);
                return;
            }
        }
    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        ActivityLogging.LogActivityMetadata(_logger, turnContext.Activity);
        await CaptureConversationReferenceAsync(turnContext.Activity, cancellationToken);

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
        return HandleKnownActionAsync(turnContext, PocApprovalCard.ApproveAction, data, cancellationToken);
    }

    private Task<AdaptiveCardInvokeResponse> OnRejectActionAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        object data,
        CancellationToken cancellationToken)
    {
        return HandleKnownActionAsync(turnContext, PocApprovalCard.RejectAction, data, cancellationToken);
    }

    private async Task<AdaptiveCardInvokeResponse> OnUnknownActionExecuteAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        object data,
        CancellationToken cancellationToken)
    {
        ActivityLogging.LogActivityMetadata(_logger, turnContext.Activity);
        await CaptureConversationReferenceAsync(turnContext.Activity, cancellationToken);

        string? reportedAction = TryReadActionIdentifier(data);
        _logger.LogWarning(
            "Adaptive Card action received with unsupported verb/action. NormalizedAction={NormalizedAction}",
            reportedAction ?? "(none)");

        // Untrusted payload must not drive approval decisions. Reject unknown actions safely.
        return AdaptiveCardInvokeResponseFactory.BadRequest("Unsupported Adaptive Card action.");
    }

    private async Task<AdaptiveCardInvokeResponse> HandleKnownActionAsync(
        ITurnContext turnContext,
        string expectedAction,
        object data,
        CancellationToken cancellationToken = default)
    {
        ActivityLogging.LogActivityMetadata(_logger, turnContext.Activity);
        await CaptureConversationReferenceAsync(turnContext.Activity, cancellationToken);

        string? payloadAction = TryReadActionIdentifier(data) ?? AdoApprovalCard.TryReadAction(data);
        if (payloadAction is not null &&
            !string.Equals(payloadAction, expectedAction, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Adaptive Card action payload mismatched verb. ExpectedAction={ExpectedAction} PayloadAction={PayloadAction}",
                expectedAction,
                payloadAction);
            return AdaptiveCardInvokeResponseFactory.BadRequest("Adaptive Card action payload mismatch.");
        }

        string? approvalIdHint = AdoApprovalCard.TryReadApprovalId(data);
        if (!string.IsNullOrWhiteSpace(approvalIdHint))
        {
            TeamsCallerIdentity caller = TeamsCallerIdentity.FromActivity(turnContext.Activity);
            AdoApprovalDecisionResult decision = await _approvalDecisions.DecideAsync(
                approvalIdHint,
                expectedAction,
                caller,
                cancellationToken);

            if (!decision.Succeeded)
            {
                _logger.LogWarning(
                    "ADO approval decision rejected. ApprovalId={ApprovalId} Action={Action} Error={Error}",
                    approvalIdHint,
                    expectedAction,
                    decision.Error);
                return AdaptiveCardInvokeResponseFactory.Message(
                    decision.Error ?? "Unable to apply the approval decision in Azure DevOps.");
            }

            string message = decision.Status == "approved"
                ? "Approved in Azure DevOps."
                : "Rejected in Azure DevOps.";
            return AdaptiveCardInvokeResponseFactory.Message(message);
        }

        // Legacy fake POC card path (no approvalId) — acknowledgement only.
        _logger.LogInformation(
            "Adaptive Card action received. NormalizedAction={NormalizedAction}",
            expectedAction);

        return AdaptiveCardInvokeResponseFactory.Message(PocApprovalCard.AcknowledgementMessage(expectedAction));
    }

    private Task CaptureConversationReferenceAsync(IActivity activity, CancellationToken cancellationToken) =>
        PocConversationReferenceCapture.TryCaptureAsync(
            activity,
            _conversationReferenceStore,
            _logger,
            cancellationToken);

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
