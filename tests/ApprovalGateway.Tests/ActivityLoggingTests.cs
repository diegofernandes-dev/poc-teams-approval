using System.Text.Json;
using ApprovalGateway.Bot;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;

namespace ApprovalGateway.Tests;

public sealed class ActivityLoggingTests
{
    [Fact]
    public void LogActivityMetadata_WritesStructuredMetadataWithoutFullPayload()
    {
        var logger = new TestLogger();
        var activity = new Activity
        {
            Type = ActivityTypes.Message,
            Id = "activity-123",
            ChannelId = "msteams",
            Conversation = new ConversationAccount { Id = "conversation-456" },
            Properties = new Dictionary<string, JsonElement>
            {
                ["correlationId"] = JsonDocument.Parse("\"corr-789\"").RootElement,
            },
        };

        ActivityLogging.LogActivityMetadata(logger, activity);

        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Information
            && entry.Message.Contains("Processing Bot activity", StringComparison.Ordinal)
            && entry.Message.Contains("activity-123", StringComparison.Ordinal)
            && entry.Message.Contains("conversation-456", StringComparison.Ordinal)
            && entry.Message.Contains("msteams", StringComparison.Ordinal));

        Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains("corr-789", StringComparison.Ordinal));
    }

    private sealed class TestLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
