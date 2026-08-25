using System.Security.Claims;
using ApprovalGateway.Configuration;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ApprovalGateway.Proactive;

/// <summary>
/// Temporary POC helper that sends a plain-text proactive Teams personal message
/// using a previously captured ConversationReference. Not for production use.
/// </summary>
public sealed class PocProactiveMessenger
{
    public const string ProactiveMessage = "Proactive Teams notification POC.";

    private readonly IChannelAdapter _adapter;
    private readonly IPocConversationReferenceStore _store;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PocProactiveMessenger> _logger;

    public PocProactiveMessenger(
        IChannelAdapter adapter,
        IPocConversationReferenceStore store,
        IConfiguration configuration,
        ILogger<PocProactiveMessenger> logger)
    {
        _adapter = adapter;
        _store = store;
        _configuration = configuration;
        _logger = logger;
    }

    public Task<PocProactiveSendResult> SendAsync(CancellationToken cancellationToken) =>
        SendTextAsync(ProactiveMessage, cancellationToken);

    public async Task<PocProactiveSendResult> SendTextAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        _logger.LogInformation("POC proactive send requested.");

        ConversationReference? reference = await _store.TryGetAsync(cancellationToken);
        if (reference is null)
        {
            _logger.LogWarning("POC proactive send failed; no conversation reference captured.");
            return PocProactiveSendResult.NoConversationReference();
        }

        string? microsoftAppId = _configuration[BotConfiguration.MicrosoftAppIdKey]
            ?? _configuration[BotConfiguration.ClientIdKey];
        if (string.IsNullOrWhiteSpace(microsoftAppId))
        {
            _logger.LogError("POC proactive send failed; MicrosoftAppId is not configured.");
            return PocProactiveSendResult.ConfigurationError("MicrosoftAppId is not configured.");
        }

        try
        {
            ClaimsIdentity identity = AgentClaims.CreateIdentity(microsoftAppId);
            await _adapter.ContinueConversationAsync(
                identity,
                reference,
                async (turnContext, ct) =>
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(text), ct);
                },
                cancellationToken);

            _logger.LogInformation(
                "POC proactive send succeeded. ConversationId={ConversationId} ChannelId={ChannelId}",
                reference.Conversation?.Id,
                reference.ChannelId);

            return PocProactiveSendResult.Succeeded(reference.Conversation?.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "POC proactive send failed. ConversationId={ConversationId} ChannelId={ChannelId}",
                reference.Conversation?.Id,
                reference.ChannelId);
            return PocProactiveSendResult.Failed(ex.Message);
        }
    }
}

public sealed class PocProactiveSendResult
{
    private PocProactiveSendResult(PocProactiveSendStatus status, string? conversationId, string? error)
    {
        Status = status;
        ConversationId = conversationId;
        Error = error;
    }

    public PocProactiveSendStatus Status { get; }

    public string? ConversationId { get; }

    public string? Error { get; }

    public static PocProactiveSendResult Succeeded(string? conversationId) =>
        new(PocProactiveSendStatus.Succeeded, conversationId, null);

    public static PocProactiveSendResult NoConversationReference() =>
        new(PocProactiveSendStatus.NoConversationReference, null, "No Teams personal conversation reference has been captured yet.");

    public static PocProactiveSendResult ConfigurationError(string message) =>
        new(PocProactiveSendStatus.ConfigurationError, null, message);

    public static PocProactiveSendResult Failed(string message) =>
        new(PocProactiveSendStatus.Failed, null, message);
}

public enum PocProactiveSendStatus
{
    Succeeded,
    NoConversationReference,
    ConfigurationError,
    Failed,
}
