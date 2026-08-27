namespace ApprovalGateway.AzureDevOps;

public static class AdoApprovalUrls
{
    public static string? BuildRunResultsUrl(string organization, string project, int? runId)
    {
        if (string.IsNullOrWhiteSpace(organization) ||
            string.IsNullOrWhiteSpace(project) ||
            runId is not int id ||
            id <= 0)
        {
            return null;
        }

        return $"https://dev.azure.com/{organization}/{Uri.EscapeDataString(project)}/_build/results?buildId={id}";
    }
}
