using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApprovalGateway.AzureDevOps;

/// <summary>
/// Applies Approve/Reject against Azure DevOps after re-reading approval state and Environment policy.
/// Card payload is treated as an untrusted correlation hint only.
/// </summary>
public sealed class AdoApprovalDecisionService
{
    private readonly IAdoApprovalsClient _client;
    private readonly AdoApprovalsOptions _options;
    private readonly ILogger<AdoApprovalDecisionService> _logger;

    public AdoApprovalDecisionService(
        IAdoApprovalsClient client,
        IOptions<AdoApprovalsOptions> options,
        ILogger<AdoApprovalDecisionService> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AdoApprovalDecisionResult> DecideAsync(
        string? approvalIdHint,
        string action,
        TeamsCallerIdentity caller,
        CancellationToken cancellationToken)
    {
        if (!_client.IsConfigured)
        {
            return AdoApprovalDecisionResult.Failed(
                "Azure DevOps is not configured on the gateway (missing PAT/org/project).");
        }

        if (string.IsNullOrWhiteSpace(approvalIdHint) || !Guid.TryParse(approvalIdHint, out _))
        {
            return AdoApprovalDecisionResult.Failed("Missing or invalid approval correlation.");
        }

        string adoStatus = action switch
        {
            "approve" => "approved",
            "reject" => "rejected",
            _ => string.Empty,
        };
        if (adoStatus.Length == 0)
        {
            return AdoApprovalDecisionResult.Failed("Unsupported approval action.");
        }

        AdoApprovalDetails? approval = await _client.GetApprovalAsync(approvalIdHint, cancellationToken);
        if (approval is null)
        {
            return AdoApprovalDecisionResult.Failed("Unable to read approval from Azure DevOps.");
        }

        if (!string.Equals(approval.Status, "pending", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "Approval is no longer pending. ApprovalId={ApprovalId} Status={Status}",
                approval.Id,
                approval.Status);
            return AdoApprovalDecisionResult.Failed($"Approval is no longer pending (status: {approval.Status}).");
        }

        if (caller.IsBlocked(approval.BlockedApprovers))
        {
            _logger.LogWarning(
                "Caller is blocked from approving this run. ApprovalId={ApprovalId} AadObjectId={AadObjectId}",
                approval.Id,
                caller.AadObjectId);
            return AdoApprovalDecisionResult.Failed("You are blocked from approving this run in Azure DevOps.");
        }

        AdoEnvironmentApprovalPolicy? policy =
            await _client.GetEnvironmentApprovalPolicyAsync(cancellationToken);
        if (policy is null || policy.Approvers.Count == 0)
        {
            return AdoApprovalDecisionResult.Failed(
                "Unable to read Environment approval policy from Azure DevOps.");
        }

        bool isAssignedApprover = policy.Approvers.Any(caller.MatchesApprover);
        if (!isAssignedApprover)
        {
            // Prefer step-level assignedApprover when ADO returns it.
            isAssignedApprover = approval.Steps.Any(step =>
                step.AssignedApprover is not null && caller.MatchesApprover(step.AssignedApprover));
        }

        if (!isAssignedApprover)
        {
            _logger.LogWarning(
                "Caller is not an Environment approver. ApprovalId={ApprovalId} AadObjectId={AadObjectId} Name={Name}",
                approval.Id,
                caller.AadObjectId,
                caller.Name);
            return AdoApprovalDecisionResult.Failed("You are not an assigned approver for this Environment.");
        }

        string comment = action == "approve"
            ? "Approved from Microsoft Teams (Approval Gateway POC)."
            : "Rejected from Microsoft Teams (Approval Gateway POC).";

        AdoApprovalUpdateResult update = await _client.UpdateApprovalAsync(
            approvalIdHint,
            adoStatus,
            comment,
            cancellationToken);

        if (!update.Succeeded)
        {
            return AdoApprovalDecisionResult.Failed(
                update.Error ?? "Azure DevOps rejected the approval update.");
        }

        _logger.LogInformation(
            "Azure DevOps approval updated from Teams. ApprovalId={ApprovalId} Status={Status} AadObjectId={AadObjectId}",
            approvalIdHint,
            adoStatus,
            caller.AadObjectId);

        return AdoApprovalDecisionResult.Ok(adoStatus);
    }
}

public sealed class AdoApprovalDecisionResult
{
    private AdoApprovalDecisionResult(bool succeeded, string? status, string? error)
    {
        Succeeded = succeeded;
        Status = status;
        Error = error;
    }

    public bool Succeeded { get; }

    public string? Status { get; }

    public string? Error { get; }

    public static AdoApprovalDecisionResult Ok(string status) => new(true, status, null);

    public static AdoApprovalDecisionResult Failed(string error) => new(false, null, error);
}
