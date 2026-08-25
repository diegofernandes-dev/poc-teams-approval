using Microsoft.Agents.Core.Models;

namespace ApprovalGateway.Proactive;

/// <summary>
/// POC store for the last Teams personal conversation reference used for proactive notify.
/// Durable across Function instances when backed by Blob; still not an approver directory.
/// </summary>
public interface IPocConversationReferenceStore
{
    Task SaveAsync(ConversationReference reference, CancellationToken cancellationToken = default);

    Task<ConversationReference?> TryGetAsync(CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
