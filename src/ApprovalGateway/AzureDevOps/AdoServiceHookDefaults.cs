namespace ApprovalGateway.AzureDevOps;

/// <summary>
/// Constants for the Azure DevOps Service Hook approval-pending slice.
/// </summary>
public static class AdoServiceHookDefaults
{
    /// <summary>
    /// Service Hook publisher for YAML pipeline Environment approvals.
    /// </summary>
    public const string PublisherId = "pipelines";

    /// <summary>
    /// Event fired when a stage waits on an Environment approval.
    /// </summary>
    public const string ApprovalPendingEventType = "ms.vss-pipelinechecks-events.approval-pending";

    /// <summary>
    /// Only this Environment should produce Teams notifications in the POC.
    /// </summary>
    public const string TargetEnvironmentName = "prd-teams-poc";
}
