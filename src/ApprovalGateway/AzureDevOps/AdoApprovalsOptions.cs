namespace ApprovalGateway.AzureDevOps;

/// <summary>
/// Configuration for Azure DevOps Approvals REST calls.
/// Service PAT is used for read-only gateway operations only — never to apply approvals on behalf of users.
/// </summary>
public sealed class AdoApprovalsOptions
{
    public const string SectionName = "AzureDevOps";

    public string Organization { get; set; } = "diegolab";

    public string Project { get; set; } = "platform-engineering";

    /// <summary>
    /// Personal Access Token used for Approvals read operations (GET). Prefer App Setting / Key Vault; never commit.
    /// </summary>
    public string? Pat { get; set; }

    /// <summary>
    /// Environment whose Approval check defines the authoritative approver list for the POC.
    /// </summary>
    public string EnvironmentName { get; set; } = AdoServiceHookDefaults.TargetEnvironmentName;
}
