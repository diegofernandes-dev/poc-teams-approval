using Microsoft.Agents.Core.Models;

namespace ApprovalGateway.Proactive;

/// <summary>
/// Temporary POC-only store for the last Teams personal conversation reference.
/// Not authoritative; not durable across restarts/cold starts/scale-out.
/// </summary>
public interface IPocConversationReferenceStore
{
    void Save(ConversationReference reference);

    bool TryGet(out ConversationReference? reference);

    void Clear();
}
