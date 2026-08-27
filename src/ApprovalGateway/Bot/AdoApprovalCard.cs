using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Agents.Core.Models;

namespace ApprovalGateway.Bot;

/// <summary>
/// Adaptive Card for a real Azure DevOps Environment approval pending notification.
/// Card <c>data.approvalId</c> is an untrusted correlation hint only.
/// </summary>
public static class AdoApprovalCard
{
    public const string SchemaVersion = "1.5";
    public const string ContentType = ContentTypes.AdaptiveCard;
    public const string ApproveAction = "approve";
    public const string RejectAction = "reject";

    public static Attachment CreateAttachment(AdoApprovalCardModel model) =>
        new()
        {
            ContentType = ContentType,
            Content = CreateCardObject(model),
        };

    public static JsonObject CreateCardObject(AdoApprovalCardModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(model.ApprovalId);

        return new JsonObject
        {
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["type"] = "AdaptiveCard",
            ["version"] = SchemaVersion,
            ["body"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = "Production deployment approval",
                    ["weight"] = "Bolder",
                    ["size"] = "Medium",
                    ["wrap"] = true,
                },
                new JsonObject
                {
                    ["type"] = "FactSet",
                    ["facts"] = new JsonArray
                    {
                        Fact("Pipeline", model.PipelineName ?? "(unknown)"),
                        Fact("Environment", model.EnvironmentName ?? "(unknown)"),
                        Fact("Stage", model.StageName ?? "(unknown)"),
                        Fact("Run", model.RunLabel ?? "(unknown)"),
                        Fact("Approval", model.ApprovalId),
                    },
                },
                new JsonObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = "Azure DevOps remains the approval authority. Buttons re-validate state before applying.",
                    ["wrap"] = true,
                    ["isSubtle"] = true,
                    ["size"] = "Small",
                },
                new JsonObject
                {
                    ["type"] = "ActionSet",
                    ["actions"] = new JsonArray
                    {
                        ExecuteAction("Approve", ApproveAction, model.ApprovalId),
                        ExecuteAction("Reject", RejectAction, model.ApprovalId),
                    },
                },
            },
        };
    }

    public static string? TryReadApprovalId(object? data) => TryReadString(data, "approvalId");

    public static string? TryReadAction(object? data) => TryReadString(data, "action");

    private static string? TryReadString(object? data, string propertyName)
    {
        if (data is null)
        {
            return null;
        }

        try
        {
            if (data is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty(propertyName, out JsonElement value) &&
                    value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }

                return null;
            }

            using var document = JsonDocument.Parse(JsonSerializer.Serialize(data));
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty(propertyName, out JsonElement property) &&
                property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static JsonObject Fact(string title, string value) =>
        new()
        {
            ["title"] = title,
            ["value"] = value,
        };

    private static JsonObject ExecuteAction(string title, string action, string approvalId) =>
        new()
        {
            ["type"] = "Action.Execute",
            ["title"] = title,
            ["verb"] = action,
            ["data"] = new JsonObject
            {
                ["action"] = action,
                ["approvalId"] = approvalId,
            },
        };
}

public sealed class AdoApprovalCardModel
{
    public required string ApprovalId { get; init; }

    public string? PipelineName { get; init; }

    public string? EnvironmentName { get; init; }

    public string? StageName { get; init; }

    public string? RunLabel { get; init; }
}
