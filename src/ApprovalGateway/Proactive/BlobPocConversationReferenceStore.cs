using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;

namespace ApprovalGateway.Proactive;

/// <summary>
/// Persists the last Teams personal ConversationReference in Azure Blob Storage so
/// Service Hook notifications can reach a different Flex Consumption instance.
/// Stores only routing fields — never message bodies, tokens, or secrets.
/// </summary>
public sealed class BlobPocConversationReferenceStore : IPocConversationReferenceStore
{
    public const string DefaultContainerName = "poc-conversation-refs";
    public const string DefaultBlobName = "personal/latest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly BlobContainerClient _container;
    private readonly BlobClient _blob;
    private readonly ILogger<BlobPocConversationReferenceStore> _logger;
    private int _containerEnsured;

    public BlobPocConversationReferenceStore(
        string storageAccountName,
        ILogger<BlobPocConversationReferenceStore> logger,
        string? containerName = null,
        string? blobName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageAccountName);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var service = new BlobServiceClient(
            new Uri($"https://{storageAccountName}.blob.core.windows.net"),
            new DefaultAzureCredential());

        _container = service.GetBlobContainerClient(containerName ?? DefaultContainerName);
        _blob = _container.GetBlobClient(blobName ?? DefaultBlobName);
    }

    internal BlobPocConversationReferenceStore(
        BlobContainerClient containerClient,
        BlobClient blobClient,
        ILogger<BlobPocConversationReferenceStore> logger)
    {
        _container = containerClient ?? throw new ArgumentNullException(nameof(containerClient));
        _blob = blobClient ?? throw new ArgumentNullException(nameof(blobClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SaveAsync(ConversationReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        await EnsureContainerAsync(cancellationToken);

        PocStoredConversationReference stored = PocStoredConversationReference.From(reference);
        await using MemoryStream stream = new();
        await JsonSerializer.SerializeAsync(stream, stored, JsonOptions, cancellationToken);
        stream.Position = 0;

        await _blob.UploadAsync(stream, overwrite: true, cancellationToken);

        _logger.LogInformation(
            "POC conversation reference persisted to blob. ConversationId={ConversationId} Blob={BlobUri}",
            stored.ConversationId,
            _blob.Uri.GetLeftPart(UriPartial.Path));
    }

    public async Task<ConversationReference?> TryGetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Response<BlobDownloadResult> response = await _blob.DownloadContentAsync(cancellationToken);
            BinaryData content = response.Value.Content;
            PocStoredConversationReference? stored =
                content.ToObjectFromJson<PocStoredConversationReference>(JsonOptions);

            if (stored is null || string.IsNullOrWhiteSpace(stored.ConversationId))
            {
                return null;
            }

            return stored.ToConversationReference();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // already absent
        }
    }

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _containerEnsured, 1, 0) == 1)
        {
            return;
        }

        await _container.CreateIfNotExistsAsync(
            PublicAccessType.None,
            cancellationToken: cancellationToken);
    }
}

/// <summary>
/// Minimal durable projection of ConversationReference routing fields for the POC.
/// </summary>
internal sealed class PocStoredConversationReference
{
    public string? ChannelId { get; set; }

    public string? ServiceUrl { get; set; }

    public string? ConversationId { get; set; }

    public string? ConversationType { get; set; }

    public string? TenantId { get; set; }

    public string? AgentId { get; set; }

    public string? UserId { get; set; }

    public string? Locale { get; set; }

    public static PocStoredConversationReference From(ConversationReference reference) =>
        new()
        {
            ChannelId = reference.ChannelId,
            ServiceUrl = reference.ServiceUrl,
            ConversationId = reference.Conversation?.Id,
            ConversationType = reference.Conversation?.ConversationType,
            TenantId = reference.Conversation?.TenantId,
            AgentId = reference.Agent?.Id,
            UserId = reference.User?.Id,
            Locale = reference.Locale,
        };

    public ConversationReference ToConversationReference() =>
        new()
        {
            ChannelId = ChannelId,
            ServiceUrl = ServiceUrl,
            Locale = Locale,
            Conversation = new ConversationAccount
            {
                Id = ConversationId,
                ConversationType = ConversationType,
                TenantId = TenantId,
            },
            Agent = string.IsNullOrWhiteSpace(AgentId) ? null : new ChannelAccount { Id = AgentId },
            User = string.IsNullOrWhiteSpace(UserId) ? null : new ChannelAccount { Id = UserId },
        };
}
