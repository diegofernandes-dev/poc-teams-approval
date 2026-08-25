using ApprovalGateway.Proactive;
using Microsoft.Extensions.Logging;

namespace ApprovalGateway.AzureDevOps;

/// <summary>
/// Handles approval-pending Service Hook notifications for the POC.
/// Azure DevOps remains the approval authority; Teams is notification UI only.
/// </summary>
public sealed class AdoApprovalPendingHandler
{
    private readonly PocProactiveMessenger _messenger;
    private readonly ILogger<AdoApprovalPendingHandler> _logger;

    public AdoApprovalPendingHandler(
        PocProactiveMessenger messenger,
        ILogger<AdoApprovalPendingHandler> logger)
    {
        _messenger = messenger;
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

        _logger.LogInformation(
            "Processing approval-pending. ApprovalId={ApprovalId} Environment={Environment} Stage={Stage} Pipeline={Pipeline} RunId={RunId}",
            pendingEvent.ApprovalId,
            pendingEvent.EnvironmentName,
            pendingEvent.StageName,
            pendingEvent.PipelineName,
            pendingEvent.RunId);

        string text = BuildNotificationText(pendingEvent);
        PocProactiveSendResult sendResult = await _messenger.SendTextAsync(text, cancellationToken);

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

    private static string BuildNotificationText(AdoApprovalPendingEvent pendingEvent)
    {
        string pipeline = pendingEvent.PipelineName ?? "(unknown pipeline)";
        string stage = pendingEvent.StageName ?? "(unknown stage)";
        string run = pendingEvent.RunName
            ?? (pendingEvent.RunId is int id ? $"#{id}" : "(unknown run)");
        string approval = pendingEvent.ApprovalId ?? "(unknown approval)";

        return
            $"Azure DevOps approval pending for {AdoServiceHookDefaults.TargetEnvironmentName}.\n" +
            $"Pipeline: {pipeline}\n" +
            $"Stage: {stage}\n" +
            $"Run: {run}\n" +
            $"ApprovalId: {approval}\n" +
            "Approve or reject in Azure DevOps (Teams decision API not wired yet).";
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
