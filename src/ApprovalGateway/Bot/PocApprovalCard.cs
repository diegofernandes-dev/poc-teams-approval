using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Agents.Core.Models;

namespace ApprovalGateway.Bot;

/// <summary>
/// Builds a fake POC Adaptive Card for Teams personal-chat Action.Execute validation.
/// </summary>
/// <remarks>
/// Card <c>data</c>/<c>verb</c> are untrusted client input. This POC may read only the
/// minimal <c>action</c> identifier for acknowledgement. Real approval processing must
/// obtain authenticated user identity, current approval state, authorization, and
/// environment/run correlation from trusted server-side sources — never from the card alone.
/// <para>
/// POC compatibility decision: buttons use <c>Action.Execute</c> only (no
/// <c>Action.Submit</c> fallback) to validate the modern <c>adaptiveCard/action</c> path
/// against the current Teams client. Microsoft documents Submit fallback for maximum
/// compatibility with older Teams clients; that is a production concern, not this slice.
/// </para>
/// </remarks>
public static class PocApprovalCard
{
    public const string SchemaVersion = "1.5";
    public const string ContentType = ContentTypes.AdaptiveCard;
    public const string ApproveAction = "approve";
    public const string RejectAction = "reject";
    public const string CardCommand = "card";

    public static string AcknowledgementMessage(string action) =>
        $"POC action received: {action}";

    public static Attachment CreateAttachment()
    {
        return new Attachment
        {
            ContentType = ContentType,
            Content = CreateCardObject(),
        };
    }

    public static string CreateCardJson() =>
        JsonSerializer.Serialize(CreateCardObject());

    public static JsonObject CreateCardObject()
    {
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
                        Fact("Application", "poc-api"),
                        Fact("Environment", "PRD"),
                        Fact("Run", "#12345"),
                        Fact("Requested by", "POC User"),
                    },
                },
                new JsonObject
                {
                    ["type"] = "ActionSet",
                    ["actions"] = new JsonArray
                    {
                        ExecuteAction("Approve", ApproveAction),
                        ExecuteAction("Reject", RejectAction),
                    },
                },
            },
        };
    }

    private static JsonObject Fact(string title, string value) =>
        new()
        {
            ["title"] = title,
            ["value"] = value,
        };

    private static JsonObject ExecuteAction(string title, string action) =>
        new()
        {
            ["type"] = "Action.Execute",
            ["title"] = title,
            ["verb"] = action,
            ["data"] = new JsonObject
            {
                ["action"] = action,
            },
        };
}
