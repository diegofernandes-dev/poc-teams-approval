using Microsoft.Agents.Core.Models;

namespace ApprovalGateway.Proactive;

/// <summary>
/// In-memory POC routing state for a single personal Teams conversation reference.
/// Used by unit tests and local runs without storage account configuration.
/// </summary>
public sealed class InMemoryPocConversationReferenceStore : IPocConversationReferenceStore
{
    private readonly object _gate = new();
    private ConversationReference? _reference;

    public Task SaveAsync(ConversationReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _reference = reference;
        }

        return Task.CompletedTask;
    }

    public Task<ConversationReference?> TryGetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult(_reference);
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _reference = null;
        }

        return Task.CompletedTask;
    }
}
