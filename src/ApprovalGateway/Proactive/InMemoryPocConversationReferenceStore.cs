using Microsoft.Agents.Core.Models;

namespace ApprovalGateway.Proactive;

/// <summary>
/// In-memory POC routing state for a single personal Teams conversation reference.
/// Lost on process restart, cold start, or scale-out. Do not use as production persistence.
/// </summary>
public sealed class InMemoryPocConversationReferenceStore : IPocConversationReferenceStore
{
    private readonly object _gate = new();
    private ConversationReference? _reference;

    public void Save(ConversationReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        lock (_gate)
        {
            _reference = reference;
        }
    }

    public bool TryGet(out ConversationReference? reference)
    {
        lock (_gate)
        {
            reference = _reference;
            return reference is not null;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _reference = null;
        }
    }
}
