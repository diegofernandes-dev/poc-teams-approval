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
            if (from.Properties.TryGetValue("aadObjectId", out JsonElement aadElement) &&
                aadElement.ValueKind == JsonValueKind.String)
            {
                aadObjectId = aadElement.GetString();
            }

            if (from.Properties.TryGetValue("email", out JsonElement emailElement) &&
                emailElement.ValueKind == JsonValueKind.String)
            {
                upn = emailElement.GetString();
            }
        }

        // Agents SDK ChannelAccount may expose AadObjectId as a first-class field via JSON.
        aadObjectId ??= TryGetStringProperty(from, "aadObjectId");

        return new TeamsCallerIdentity
        {
            AadObjectId = aadObjectId,
            Name = from?.Name,
            UserPrincipalName = upn,
        };
    }

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

        if (!string.IsNullOrWhiteSpace(Name) &&
            string.Equals(Name, approver.DisplayName, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(approver.UniqueName))
        {
            // Display-name-only match is weak; still require UniqueName present on ADO side
            // and treat as last-resort POC signal when UPN/OID are absent from the activity.
            return false;
        }

        return false;
    }

    public bool IsBlocked(IEnumerable<AdoIdentityRef> blockedApprovers) =>
        blockedApprovers.Any(MatchesApprover);

    private static string? TryGetStringProperty(ChannelAccount? account, string name)
    {
        if (account?.Properties is null ||
            !account.Properties.TryGetValue(name, out JsonElement element) ||
            element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return element.GetString();
    }

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
