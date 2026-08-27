using ApprovalGateway.AzureDevOps;
using ApprovalGateway.Bot;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ApprovalGateway.Tests;

public sealed class TeamsCallerIdentityTests
{
    [Fact]
    public void TryDecodeAadObjectId_DecodesDescriptor()
    {
        string? oid = TeamsCallerIdentity.TryDecodeAadObjectId(
            "aad.ZGQ2MzYzZDMtNzU4ZC03YTIwLTk4NTktMDY3YzQ0MDJmN2Jm");

        Assert.Equal("dd6363d3-758d-7a20-9859-067c4402f7bf", oid);
    }

    [Fact]
    public void MatchesApprover_ByDecodedDescriptor()
    {
        var caller = new TeamsCallerIdentity
        {
            AadObjectId = "dd6363d3-758d-7a20-9859-067c4402f7bf",
        };

        var approver = new AdoIdentityRef
        {
            UniqueName = "diego.fernandes@outlook.com",
            Descriptor = "aad.ZGQ2MzYzZDMtNzU4ZC03YTIwLTk4NTktMDY3YzQ0MDJmN2Jm",
        };

        Assert.True(caller.MatchesApprover(approver));
    }
}

public sealed class AdoApprovalDecisionServiceTests
{
    [Fact]
    public async Task DecideAsync_NotConfigured_FailsSafely()
    {
        var client = new Mock<IAdoApprovalsClient>(MockBehavior.Strict);
        client.SetupGet(c => c.IsConfigured).Returns(false);
        var service = new AdoApprovalDecisionService(
            client.Object,
            Options.Create(new AdoApprovalsOptions()),
            NullLogger<AdoApprovalDecisionService>.Instance);

        AdoApprovalDecisionResult result = await service.DecideAsync(
            Guid.NewGuid().ToString(),
            "approve",
            new TeamsCallerIdentity { AadObjectId = Guid.NewGuid().ToString() },
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("not configured", result.Error);
    }

    [Fact]
    public async Task DecideAsync_NotPending_FailsWithoutUpdate()
    {
        string approvalId = Guid.NewGuid().ToString();
        var client = new Mock<IAdoApprovalsClient>(MockBehavior.Strict);
        client.SetupGet(c => c.IsConfigured).Returns(true);
        client.Setup(c => c.GetApprovalAsync(approvalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdoApprovalDetails { Id = approvalId, Status = "approved" });

        var service = new AdoApprovalDecisionService(
            client.Object,
            Options.Create(new AdoApprovalsOptions()),
            NullLogger<AdoApprovalDecisionService>.Instance);

        AdoApprovalDecisionResult result = await service.DecideAsync(
            approvalId,
            "approve",
            new TeamsCallerIdentity { AadObjectId = Guid.NewGuid().ToString() },
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("no longer pending", result.Error);
        client.Verify(c => c.UpdateApprovalAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DecideAsync_AssignedApprover_UpdatesAdo()
    {
        string approvalId = Guid.NewGuid().ToString();
        string aadObjectId = "dd6363d3-758d-7a20-9859-067c4402f7bf";
        var client = new Mock<IAdoApprovalsClient>(MockBehavior.Strict);
        client.SetupGet(c => c.IsConfigured).Returns(true);
        client.Setup(c => c.GetApprovalAsync(approvalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdoApprovalDetails
            {
                Id = approvalId,
                Status = "pending",
                BlockedApprovers = [],
            });
        client.Setup(c => c.GetEnvironmentApprovalPolicyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdoEnvironmentApprovalPolicy
            {
                EnvironmentName = "prd-teams-poc",
                Approvers =
                [
                    new AdoIdentityRef
                    {
                        UniqueName = "diego.fernandes@outlook.com",
                        Descriptor = "aad.ZGQ2MzYzZDMtNzU4ZC03YTIwLTk4NTktMDY3YzQ0MDJmN2Jm",
                    },
                ],
            });
        client.Setup(c => c.UpdateApprovalAsync(approvalId, "approved", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AdoApprovalUpdateResult.Ok());

        var service = new AdoApprovalDecisionService(
            client.Object,
            Options.Create(new AdoApprovalsOptions()),
            NullLogger<AdoApprovalDecisionService>.Instance);

        AdoApprovalDecisionResult result = await service.DecideAsync(
            approvalId,
            "approve",
            new TeamsCallerIdentity { AadObjectId = aadObjectId },
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("approved", result.Status);
    }
}

public sealed class AdoApprovalCardTests
{
    [Fact]
    public void CreateAttachment_IncludesApprovalIdInActionData()
    {
        var attachment = AdoApprovalCard.CreateAttachment(new AdoApprovalCardModel
        {
            ApprovalId = "11111111-1111-1111-1111-111111111111",
            PipelineName = "poc-teams-approval",
            EnvironmentName = "prd-teams-poc",
            StageName = "PRD",
            RunLabel = "20260825.5",
        });

        Assert.Equal(AdoApprovalCard.ContentType, attachment.ContentType);
        string json = System.Text.Json.JsonSerializer.Serialize(attachment.Content);
        Assert.Contains("11111111-1111-1111-1111-111111111111", json);
        Assert.Contains("prd-teams-poc", json);
    }

    [Fact]
    public void TryReadApprovalId_ReadsHint()
    {
        Assert.Equal(
            "11111111-1111-1111-1111-111111111111",
            AdoApprovalCard.TryReadApprovalId(new
            {
                action = "approve",
                approvalId = "11111111-1111-1111-1111-111111111111",
            }));
    }
}
