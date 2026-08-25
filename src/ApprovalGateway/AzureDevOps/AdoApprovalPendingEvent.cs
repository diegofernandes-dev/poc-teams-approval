namespace ApprovalGateway.AzureDevOps;

/// <summary>
/// Minimal fields extracted from an Azure DevOps approval-pending Service Hook payload.
/// </summary>
public sealed class AdoApprovalPendingEvent
{
    public required string EventType { get; init; }

    public string? ApprovalId { get; init; }

    public string? ApprovalStatus { get; init; }

    public string? EnvironmentName { get; init; }

    public string? StageName { get; init; }

    public string? PipelineName { get; init; }

    public string? RunName { get; init; }

    public int? RunId { get; init; }

    public string? MessageText { get; init; }
}
