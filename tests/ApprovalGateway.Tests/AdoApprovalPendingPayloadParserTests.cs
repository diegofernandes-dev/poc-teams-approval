using ApprovalGateway.AzureDevOps;

namespace ApprovalGateway.Tests;

public sealed class AdoApprovalPendingPayloadParserTests
{
    [Fact]
    public void TryParse_ValidPayload_ExtractsFields()
    {
        const string json = """
            {
              "eventType": "ms.vss-pipelinechecks-events.approval-pending",
              "publisherId": "pipelines",
              "message": { "text": "Approval pending for deployment of pipeline run1 to environment prd-teams-poc." },
              "resource": {
                "approval": { "id": "11111111-1111-1111-1111-111111111111", "status": "pending" },
                "stageName": "PRD",
                "pipeline": { "name": "poc-teams-approval" },
                "run": { "id": 128, "name": "20260825.2" },
                "resource": { "type": "environment", "name": "prd-teams-poc" }
              }
            }
            """;

        bool ok = AdoApprovalPendingPayloadParser.TryParse(json, out AdoApprovalPendingEvent? parsed, out string? error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(parsed);
        Assert.Equal(AdoServiceHookDefaults.ApprovalPendingEventType, parsed.EventType);
        Assert.Equal("11111111-1111-1111-1111-111111111111", parsed.ApprovalId);
        Assert.Equal("pending", parsed.ApprovalStatus);
        Assert.Equal("prd-teams-poc", parsed.EnvironmentName);
        Assert.Equal("PRD", parsed.StageName);
        Assert.Equal("poc-teams-approval", parsed.PipelineName);
        Assert.Equal("20260825.2", parsed.RunName);
        Assert.Equal(128, parsed.RunId);
    }

    [Fact]
    public void TryParse_EmptyBody_Fails()
    {
        bool ok = AdoApprovalPendingPayloadParser.TryParse(" ", out AdoApprovalPendingEvent? parsed, out string? error);

        Assert.False(ok);
        Assert.Null(parsed);
        Assert.Equal("Request body is empty.", error);
    }

    [Fact]
    public void TryParse_InvalidJson_Fails()
    {
        bool ok = AdoApprovalPendingPayloadParser.TryParse("{not-json", out AdoApprovalPendingEvent? parsed, out string? error);

        Assert.False(ok);
        Assert.Null(parsed);
        Assert.StartsWith("Invalid JSON:", error);
    }
}
