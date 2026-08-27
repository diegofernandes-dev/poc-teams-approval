using System.Text;
using System.Text.Json;
using Microsoft.Agents.Core.Models;

namespace ApprovalGateway.AzureDevOps;

/// <summary>
/// Authenticated Teams caller identity extracted from the Bot Framework activity.
/// </summary>
public sealed class TeamsCallerIdentity
{
    public string? AadObjectId { get; init; }

    public string? Name { get; init; }

    public string? UserPrincipalName { get; init; }

    public static TeamsCallerIdentity FromActivity(IActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        ChannelAccount? from = activity.From;
        string? aadObjectId = null;
        string? upn = null;

        if (from?.Properties is not null)
        {
            aadObjectId = TryReadPropertyString(from.Properties, "aadObjectId")
                ?? TryReadPropertyString(from.Properties, "AadObjectId");
            upn = TryReadPropertyString(from.Properties, "email")
                ?? TryReadPropertyString(from.Properties, "userPrincipalName");
        }

        // Fall back to ChannelData (Teams often places aadObjectId there on invokes).
        TryReadFromChannelData(activity.ChannelData, ref aadObjectId, ref upn);

        return new TeamsCallerIdentity
        {
            AadObjectId = aadObjectId,
            Name = from?.Name,
            UserPrincipalName = upn,
        };
    }

    private static void TryReadFromChannelData(object? channelData, ref string? aadObjectId, ref string? upn)
    {
        if (channelData is null)
        {
            return;
        }

        try
        {
            using JsonDocument document = channelData is JsonElement element
                ? JsonDocument.Parse(element.GetRawText())
                : JsonDocument.Parse(JsonSerializer.Serialize(channelData));

            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (aadObjectId is null &&
                root.TryGetProperty("from", out JsonElement fromElement) &&
                fromElement.ValueKind == JsonValueKind.Object)
            {
                aadObjectId = TryGetJsonString(fromElement, "aadObjectId")
                    ?? TryGetJsonString(fromElement, "AadObjectId");
                upn ??= TryGetJsonString(fromElement, "email")
                    ?? TryGetJsonString(fromElement, "userPrincipalName");
            }
        }
        catch (JsonException)
        {
            // Ignore malformed channel data; other identity fields may still match.
        }
    }

    private static string? TryReadPropertyString(IDictionary<string, JsonElement> properties, string name) =>
        properties.TryGetValue(name, out JsonElement element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private static string? TryGetJsonString(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public bool MatchesApprover(AdoIdentityRef approver)
    {
        if (!string.IsNullOrWhiteSpace(AadObjectId))
        {
            if (string.Equals(AadObjectId, approver.Id, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string? decoded = TryDecodeAadObjectId(approver.Descriptor);
            if (string.Equals(AadObjectId, decoded, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(UserPrincipalName) &&
            string.Equals(UserPrincipalName, approver.UniqueName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // POC fallback: Teams often omits AAD OID/UPN on Action.Execute; display name is the
        // remaining trusted-enough signal when it matches an Environment approver exactly.
        if (!string.IsNullOrWhiteSpace(Name) &&
            string.Equals(Name, approver.DisplayName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public bool IsBlocked(IEnumerable<AdoIdentityRef> blockedApprovers) =>
        blockedApprovers.Any(MatchesApprover);

    internal static string? TryDecodeAadObjectId(string? descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor) ||
            !descriptor.StartsWith("aad.", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string payload = descriptor["aad.".Length..];
        try
        {
            string padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            byte[] bytes = Convert.FromBase64String(padded);
            string decoded = Encoding.UTF8.GetString(bytes);
            return Guid.TryParse(decoded, out _) ? decoded : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
