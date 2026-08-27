using ApprovalGateway.Bot;
using ApprovalGateway.Proactive;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApprovalGateway.AzureDevOps;

/// <summary>
/// Handles approval-pending Service Hook notifications for the POC.
/// Azure DevOps remains the approval authority; Teams is notification UI only.
/// </summary>
public sealed class AdoApprovalPendingHandler
{
    private readonly PocProactiveMessenger _messenger;
    private readonly AdoApprovalsOptions _options;
    private readonly ILogger<AdoApprovalPendingHandler> _logger;

    public AdoApprovalPendingHandler(
        PocProactiveMessenger messenger,
        IOptions<AdoApprovalsOptions> options,
        ILogger<AdoApprovalPendingHandler> logger)
    {
        _messenger = messenger;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AdoApprovalPendingHandleResult> HandleAsync(
        AdoApprovalPendingEvent pendingEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pendingEvent);

        if (!string.Equals(
                pendingEvent.EventType,
                AdoServiceHookDefaults.ApprovalPendingEventType,
                StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "Ignoring Service Hook event with unexpected type. EventType={EventType}",
                pendingEvent.EventType);
            return AdoApprovalPendingHandleResult.Ignored("unexpected_event_type");
        }

        if (!string.Equals(
                pendingEvent.EnvironmentName,
                AdoServiceHookDefaults.TargetEnvironmentName,
                StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "Ignoring approval-pending for non-target environment. Environment={Environment} ApprovalId={ApprovalId}",
                pendingEvent.EnvironmentName,
                pendingEvent.ApprovalId);
            return AdoApprovalPendingHandleResult.Ignored("environment_filter");
        }

        if (string.IsNullOrWhiteSpace(pendingEvent.ApprovalId))
        {
            _logger.LogWarning("approval-pending missing ApprovalId; skipping Teams notification.");
            return AdoApprovalPendingHandleResult.Accepted("notify_skipped_missing_approval_id", null);
        }

        _logger.LogInformation(
            "Processing approval-pending. ApprovalId={ApprovalId} Environment={Environment} Stage={Stage} Pipeline={Pipeline} RunId={RunId}",
            pendingEvent.ApprovalId,
            pendingEvent.EnvironmentName,
            pendingEvent.StageName,
            pendingEvent.PipelineName,
            pendingEvent.RunId);

        var cardModel = new AdoApprovalCardModel
        {
            ApprovalId = pendingEvent.ApprovalId,
            PipelineName = pendingEvent.PipelineName,
            EnvironmentName = pendingEvent.EnvironmentName ?? AdoServiceHookDefaults.TargetEnvironmentName,
            StageName = pendingEvent.StageName,
            RunLabel = pendingEvent.RunName
                ?? (pendingEvent.RunId is int id ? $"#{id}" : null),
            ApprovalUrl = AdoApprovalUrls.BuildRunResultsUrl(
                _options.Organization,
                _options.Project,
                pendingEvent.RunId),
        };

        PocProactiveSendResult sendResult = await _messenger.SendAttachmentAsync(
            AdoApprovalCard.CreateAttachment(cardModel),
            cancellationToken);

        return sendResult.Status switch
        {
            PocProactiveSendStatus.Succeeded =>
                AdoApprovalPendingHandleResult.Accepted("notified", sendResult.ConversationId),
            PocProactiveSendStatus.NoConversationReference =>
                AdoApprovalPendingHandleResult.Accepted("notify_skipped_no_conversation", null),
            // Teams/gateway failure must leave Azure DevOps approval pending (never fail open).
            _ => AdoApprovalPendingHandleResult.Accepted("notify_failed", null, sendResult.Error),
        };
    }
}

public sealed class AdoApprovalPendingHandleResult
{
    private AdoApprovalPendingHandleResult(
        string status,
        string outcome,
        string? conversationId,
        string? error)
    {
        Status = status;
        Outcome = outcome;
        ConversationId = conversationId;
        Error = error;
    }

    public string Status { get; }

    public string Outcome { get; }

    public string? ConversationId { get; }

    public string? Error { get; }

    public static AdoApprovalPendingHandleResult Ignored(string outcome) =>
        new("ignored", outcome, null, null);

    public static AdoApprovalPendingHandleResult Accepted(
        string outcome,
        string? conversationId,
        string? error = null) =>
        new("accepted", outcome, conversationId, error);
}
