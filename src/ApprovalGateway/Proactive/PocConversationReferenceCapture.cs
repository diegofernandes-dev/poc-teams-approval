using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;

namespace ApprovalGateway.Proactive;

/// <summary>
/// Captures the minimum Teams personal conversation routing identity for POC proactive messaging.
/// Stores only ConversationReference technical fields — never message bodies, tokens, or secrets.
/// </summary>
public static class PocConversationReferenceCapture
{
    public static async Task<bool> TryCaptureAsync(
        IActivity activity,
        IPocConversationReferenceStore store,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(logger);

        if (!IsPersonalTeamsActivity(activity))
        {
            return false;
        }

        ConversationReference reference = activity.GetConversationReference();
        if (string.IsNullOrWhiteSpace(reference.Conversation?.Id) ||
            string.IsNullOrWhiteSpace(reference.ServiceUrl) ||
            string.IsNullOrWhiteSpace(reference.Agent?.Id))
        {
            logger.LogWarning(
                "Skipped POC conversation reference capture; missing ConversationId, ServiceUrl, or Agent Id. ChannelId={ChannelId}",
                activity.ChannelId);
            return false;
        }

        await store.SaveAsync(reference, cancellationToken);

        logger.LogInformation(
            "POC conversation reference captured/updated. ConversationId={ConversationId} ChannelId={ChannelId} ServiceUrlHost={ServiceUrlHost}",
            reference.Conversation.Id,
            reference.ChannelId,
            TryGetHost(reference.ServiceUrl));

        return true;
    }

    public static bool IsPersonalTeamsActivity(IActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        if (!string.Equals(activity.ChannelId, Channels.Msteams, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        ConversationAccount? conversation = activity.Conversation;
        if (conversation is null)
        {
            return false;
        }

        if (conversation.IsGroup == true)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(conversation.ConversationType) &&
            !string.Equals(conversation.ConversationType, ConversationTypes.Personal, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static string? TryGetHost(string? serviceUrl)
    {
        if (string.IsNullOrWhiteSpace(serviceUrl))
        {
            return null;
        }

        return Uri.TryCreate(serviceUrl, UriKind.Absolute, out Uri? uri) ? uri.Host : null;
    }
}
