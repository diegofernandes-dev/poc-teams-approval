using System.Text.Json;
using System.Text.Json.Nodes;
using ApprovalGateway.Bot;
using Microsoft.Agents.Core.Models;

namespace ApprovalGateway.Tests;

public sealed class PocApprovalCardTests
{
    [Fact]
    public void CreateAttachment_UsesAdaptiveCardContentTypeAndSchemaVersion()
    {
        Attachment attachment = PocApprovalCard.CreateAttachment();

        Assert.Equal(ContentTypes.AdaptiveCard, attachment.ContentType);
        Assert.NotNull(attachment.Content);

        using JsonDocument document = JsonDocument.Parse(PocApprovalCard.CreateCardJson());
        Assert.Equal(PocApprovalCard.SchemaVersion, document.RootElement.GetProperty("version").GetString());
        Assert.Equal("1.5", document.RootElement.GetProperty("version").GetString());
        Assert.Equal("AdaptiveCard", document.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void CreateCardJson_HasExactlyTwoExecuteActionsWithApproveAndReject()
    {
        using JsonDocument document = JsonDocument.Parse(PocApprovalCard.CreateCardJson());
        JsonElement body = document.RootElement.GetProperty("body");

        JsonElement? actionSet = null;
        foreach (JsonElement item in body.EnumerateArray())
        {
            if (item.TryGetProperty("type", out JsonElement type) &&
                type.GetString() == "ActionSet")
            {
                actionSet = item;
                break;
            }
        }

        Assert.True(actionSet.HasValue);
        JsonElement actions = actionSet.Value.GetProperty("actions");
        Assert.Equal(2, actions.GetArrayLength());

        var verbs = new HashSet<string>(StringComparer.Ordinal);
        var dataActions = new HashSet<string>(StringComparer.Ordinal);

        foreach (JsonElement action in actions.EnumerateArray())
        {
            Assert.Equal("Action.Execute", action.GetProperty("type").GetString());
            Assert.False(action.TryGetProperty("fallback", out _));

            string? verb = action.GetProperty("verb").GetString();
            string? dataAction = action.GetProperty("data").GetProperty("action").GetString();
            Assert.False(string.IsNullOrWhiteSpace(verb));
            Assert.Equal(verb, dataAction);
            verbs.Add(verb!);
            dataActions.Add(dataAction!);
        }

        Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal) { PocApprovalCard.ApproveAction, PocApprovalCard.RejectAction },
            verbs);
        Assert.Equal(verbs, dataActions);
    }

    [Fact]
    public void CreateCardObject_ContainsOnlyFakePocFacts()
    {
        JsonObject card = PocApprovalCard.CreateCardObject();
        string json = card.ToJsonString();

        Assert.Contains("Production deployment approval", json, StringComparison.Ordinal);
        Assert.Contains("poc-api", json, StringComparison.Ordinal);
        Assert.Contains("PRD", json, StringComparison.Ordinal);
        Assert.Contains("#12345", json, StringComparison.Ordinal);
        Assert.Contains("POC User", json, StringComparison.Ordinal);
        Assert.DoesNotContain("tenant", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("5936429a", json, StringComparison.OrdinalIgnoreCase);
    }
}
