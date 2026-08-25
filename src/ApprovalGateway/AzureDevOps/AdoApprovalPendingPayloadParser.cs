using System.Text.Json;

namespace ApprovalGateway.AzureDevOps;

/// <summary>
/// Parses Azure DevOps approval-pending Service Hook JSON into a small typed view.
/// Tolerates partial payloads; missing fields remain null.
/// </summary>
public static class AdoApprovalPendingPayloadParser
{
    public static bool TryParse(string json, out AdoApprovalPendingEvent? parsed, out string? error)
    {
        parsed = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Request body is empty.";
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            string? eventType = GetString(root, "eventType");
            JsonElement resource = GetObject(root, "resource");
            JsonElement approval = GetObject(resource, "approval");
            JsonElement envResource = GetObject(resource, "resource");
            JsonElement pipeline = GetObject(resource, "pipeline");
            JsonElement run = GetObject(resource, "run");
            JsonElement message = GetObject(root, "message");

            parsed = new AdoApprovalPendingEvent
            {
                EventType = eventType ?? string.Empty,
                ApprovalId = GetString(approval, "id"),
                ApprovalStatus = GetString(approval, "status"),
                EnvironmentName = GetString(envResource, "name")
                    ?? GetString(resource, "environmentName"),
                StageName = GetString(resource, "stageName")
                    ?? GetString(GetObject(resource, "stage"), "name"),
                PipelineName = GetString(pipeline, "name"),
                RunName = GetString(run, "name"),
                RunId = GetInt(run, "id"),
                MessageText = GetString(message, "text"),
            };

            return true;
        }
        catch (JsonException ex)
        {
            error = $"Invalid JSON: {ex.Message}";
            return false;
        }
    }

    private static JsonElement GetObject(JsonElement parent, string name)
    {
        if (parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement child)
            && child.ValueKind == JsonValueKind.Object)
        {
            return child;
        }

        return default;
    }

    private static string? GetString(JsonElement parent, string name)
    {
        if (parent.ValueKind != JsonValueKind.Object
            || !parent.TryGetProperty(name, out JsonElement child))
        {
            return null;
        }

        return child.ValueKind switch
        {
            JsonValueKind.String => child.GetString(),
            JsonValueKind.Number => child.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
    }

    private static int? GetInt(JsonElement parent, string name)
    {
        if (parent.ValueKind != JsonValueKind.Object
            || !parent.TryGetProperty(name, out JsonElement child))
        {
            return null;
        }

        if (child.ValueKind == JsonValueKind.Number && child.TryGetInt32(out int value))
        {
            return value;
        }

        if (child.ValueKind == JsonValueKind.String
            && int.TryParse(child.GetString(), out int parsed))
        {
            return parsed;
        }

        return null;
    }
}
