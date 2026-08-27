using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApprovalGateway.AzureDevOps;

public interface IAdoApprovalsClient
{
    bool IsConfigured { get; }

    Task<AdoApprovalDetails?> GetApprovalAsync(string approvalId, CancellationToken cancellationToken);

    Task<AdoEnvironmentApprovalPolicy?> GetEnvironmentApprovalPolicyAsync(CancellationToken cancellationToken);

    Task<AdoApprovalUpdateResult> UpdateApprovalAsync(
        string approvalId,
        string status,
        string comment,
        CancellationToken cancellationToken);
}

public sealed class AdoApprovalsClient : IAdoApprovalsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly AdoApprovalsOptions _options;
    private readonly ILogger<AdoApprovalsClient> _logger;

    public AdoApprovalsClient(
        HttpClient http,
        IOptions<AdoApprovalsOptions> options,
        ILogger<AdoApprovalsClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Organization)
        && !string.IsNullOrWhiteSpace(_options.Project)
        && !string.IsNullOrWhiteSpace(_options.Pat);

    public async Task<AdoApprovalDetails?> GetApprovalAsync(string approvalId, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalId);

        using HttpRequestMessage request = CreateRequest(
            HttpMethod.Get,
            $"https://dev.azure.com/{_options.Organization}/{Uri.EscapeDataString(_options.Project)}/_apis/pipelines/approvals/{Uri.EscapeDataString(approvalId)}?api-version=7.1");

        using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "ADO GetApproval failed. ApprovalId={ApprovalId} StatusCode={StatusCode} Body={Body}",
                approvalId,
                (int)response.StatusCode,
                Truncate(body));
            return null;
        }

        return JsonSerializer.Deserialize<AdoApprovalDetails>(body, JsonOptions);
    }

    public async Task<AdoEnvironmentApprovalPolicy?> GetEnvironmentApprovalPolicyAsync(
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        // Resolve environment id by name, then load Approval check settings (authoritative approver list).
        using HttpRequestMessage envRequest = CreateRequest(
            HttpMethod.Get,
            $"https://dev.azure.com/{_options.Organization}/{Uri.EscapeDataString(_options.Project)}/_apis/distributedtask/environments?name={Uri.EscapeDataString(_options.EnvironmentName)}&api-version=7.1-preview.1");

        using HttpResponseMessage envResponse = await _http.SendAsync(envRequest, cancellationToken);
        string envBody = await envResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!envResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "ADO list environments failed. StatusCode={StatusCode} Body={Body}",
                (int)envResponse.StatusCode,
                Truncate(envBody));
            return null;
        }

        using JsonDocument envDoc = JsonDocument.Parse(envBody);
        JsonElement env = default;
        if (envDoc.RootElement.TryGetProperty("value", out JsonElement values))
        {
            foreach (JsonElement item in values.EnumerateArray())
            {
                if (string.Equals(
                        item.GetPropertyOrDefault("name"),
                        _options.EnvironmentName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    env = item;
                    break;
                }
            }
        }

        if (env.ValueKind != JsonValueKind.Object || !env.TryGetProperty("id", out JsonElement idElement))
        {
            _logger.LogWarning("ADO environment not found. Environment={Environment}", _options.EnvironmentName);
            return null;
        }

        string environmentId = idElement.ValueKind == JsonValueKind.Number
            ? idElement.GetInt32().ToString()
            : idElement.GetString() ?? string.Empty;

        using HttpRequestMessage checksRequest = CreateRequest(
            HttpMethod.Get,
            $"https://dev.azure.com/{_options.Organization}/{Uri.EscapeDataString(_options.Project)}/_apis/pipelines/checks/configurations?resourceType=environment&resourceId={Uri.EscapeDataString(environmentId)}&api-version=7.1-preview.1");

        using HttpResponseMessage checksResponse = await _http.SendAsync(checksRequest, cancellationToken);
        string checksBody = await checksResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!checksResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "ADO list check configurations failed. StatusCode={StatusCode} Body={Body}",
                (int)checksResponse.StatusCode,
                Truncate(checksBody));
            return null;
        }

        using JsonDocument checksDoc = JsonDocument.Parse(checksBody);
        int? approvalCheckId = null;
        if (checksDoc.RootElement.TryGetProperty("value", out JsonElement checkValues))
        {
            foreach (JsonElement check in checkValues.EnumerateArray())
            {
                string? typeName = null;
                if (check.TryGetProperty("type", out JsonElement type) &&
                    type.TryGetProperty("name", out JsonElement typeNameElement))
                {
                    typeName = typeNameElement.GetString();
                }

                if (string.Equals(typeName, "Approval", StringComparison.OrdinalIgnoreCase)
                    && check.TryGetProperty("id", out JsonElement checkId))
                {
                    approvalCheckId = checkId.GetInt32();
                    break;
                }
            }
        }

        if (approvalCheckId is null)
        {
            _logger.LogWarning(
                "No Approval check found on environment. Environment={Environment} Id={EnvironmentId}",
                _options.EnvironmentName,
                environmentId);
            return null;
        }

        using HttpRequestMessage detailRequest = CreateRequest(
            HttpMethod.Get,
            $"https://dev.azure.com/{_options.Organization}/{Uri.EscapeDataString(_options.Project)}/_apis/pipelines/checks/configurations/{approvalCheckId}?%24expand=settings&api-version=7.1-preview.1");

        using HttpResponseMessage detailResponse = await _http.SendAsync(detailRequest, cancellationToken);
        string detailBody = await detailResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!detailResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "ADO get approval check failed. CheckId={CheckId} StatusCode={StatusCode} Body={Body}",
                approvalCheckId,
                (int)detailResponse.StatusCode,
                Truncate(detailBody));
            return null;
        }

        AdoCheckConfiguration? configuration =
            JsonSerializer.Deserialize<AdoCheckConfiguration>(detailBody, JsonOptions);
        AdoApprovalCheckSettings? settings = configuration?.Settings;
        if (settings is null)
        {
            return null;
        }

        return new AdoEnvironmentApprovalPolicy
        {
            EnvironmentName = _options.EnvironmentName,
            RequesterCannotBeApprover = settings.RequesterCannotBeApprover,
            Approvers = settings.Approvers ?? [],
        };
    }

    public async Task<AdoApprovalUpdateResult> UpdateApprovalAsync(
        string approvalId,
        string status,
        string comment,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        var payload = new[]
        {
            new
            {
                approvalId,
                status,
                comment,
            },
        };

        using HttpRequestMessage request = CreateRequest(
            HttpMethod.Patch,
            $"https://dev.azure.com/{_options.Organization}/{Uri.EscapeDataString(_options.Project)}/_apis/pipelines/approvals?api-version=7.1");
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "ADO UpdateApproval failed. ApprovalId={ApprovalId} Status={Status} StatusCode={StatusCode} Body={Body}",
                approvalId,
                status,
                (int)response.StatusCode,
                Truncate(body));
            return AdoApprovalUpdateResult.Failed(Truncate(body));
        }

        return AdoApprovalUpdateResult.Ok();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        byte[] token = Encoding.ASCII.GetBytes($":{_options.Pat}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(token));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Azure DevOps Approvals client is not configured. Set AzureDevOps__Organization, AzureDevOps__Project, and AzureDevOps__Pat.");
        }
    }

    private static string Truncate(string value) =>
        value.Length <= 400 ? value : value[..400] + "...";
}

internal static class JsonElementExtensions
{
    public static string? GetPropertyOrDefault(this JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

public sealed class AdoApprovalDetails
{
    public string? Id { get; set; }

    public string? Status { get; set; }

    public List<AdoApprovalStep> Steps { get; set; } = [];

    public List<AdoIdentityRef> BlockedApprovers { get; set; } = [];
}

public sealed class AdoApprovalStep
{
    public string? Status { get; set; }

    public AdoIdentityRef? AssignedApprover { get; set; }
}

public sealed class AdoIdentityRef
{
    public string? Id { get; set; }

    public string? DisplayName { get; set; }

    public string? UniqueName { get; set; }

    public string? Descriptor { get; set; }
}

public sealed class AdoEnvironmentApprovalPolicy
{
    public required string EnvironmentName { get; init; }

    public bool RequesterCannotBeApprover { get; init; }

    public IReadOnlyList<AdoIdentityRef> Approvers { get; init; } = [];
}

internal sealed class AdoCheckConfiguration
{
    public AdoApprovalCheckSettings? Settings { get; set; }
}

internal sealed class AdoApprovalCheckSettings
{
    public bool RequesterCannotBeApprover { get; set; }

    public List<AdoIdentityRef>? Approvers { get; set; }
}

public sealed class AdoApprovalUpdateResult
{
    private AdoApprovalUpdateResult(bool succeeded, string? error)
    {
        Succeeded = succeeded;
        Error = error;
    }

    public bool Succeeded { get; }

    public string? Error { get; }

    public static AdoApprovalUpdateResult Ok() => new(true, null);

    public static AdoApprovalUpdateResult Failed(string error) => new(false, error);
}
