using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;

namespace ApprovalGateway.Bot;

public static class ActivityLogging
{
    public static void LogActivityMetadata(ILogger logger, IActivity activity)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(activity);

        string? correlationId = null;
        if (activity.Properties?.TryGetValue("correlationId", out var correlationValue) == true)
        {
            correlationId = correlationValue.ToString();
        }

        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["ActivityId"] = activity.Id,
            ["ConversationId"] = activity.Conversation?.Id,
            ["ChannelId"] = activity.ChannelId,
            ["CorrelationId"] = correlationId,
        }))
        {
            logger.LogInformation(
                "Processing Bot activity. Type={ActivityType} ActivityId={ActivityId} ConversationId={ConversationId} ChannelId={ChannelId}",
                activity.Type,
                activity.Id,
                activity.Conversation?.Id,
                activity.ChannelId);
        }
    }
}
