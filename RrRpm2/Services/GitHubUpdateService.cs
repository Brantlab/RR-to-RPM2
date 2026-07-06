using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;

namespace RrRpm2.Services;

internal sealed class GitHubUpdateService
{
    private const string LatestReleaseApiUrl =
        "https://api.github.com/repos/Brantlab/RR-to-RPM2/releases/latest";

    private static readonly HttpClient HttpClient = CreateHttpClient();

    public async Task<GitHubRelease> GetLatestReleaseAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await HttpClient.GetAsync(
            LatestReleaseApiUrl,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<GitHubReleasePayload>(
            cancellationToken: cancellationToken);

        if (payload is null ||
            string.IsNullOrWhiteSpace(payload.TagName) ||
            string.IsNullOrWhiteSpace(payload.HtmlUrl))
        {
            throw new InvalidOperationException(
                "GitHub returned incomplete release information.");
        }

        var versionText = payload.TagName.Trim().TrimStart('v', 'V').Split('-')[0];
        if (!Version.TryParse(versionText, out var version))
        {
            throw new InvalidOperationException(
                $"The latest GitHub release tag \"{payload.TagName}\" is not a valid version.");
        }

        if (!Uri.TryCreate(payload.HtmlUrl, UriKind.Absolute, out var releaseUri) ||
            releaseUri.Scheme != Uri.UriSchemeHttps ||
            !releaseUri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "GitHub returned an invalid release page address.");
        }

        return new GitHubRelease(payload.TagName, version, releaseUri.AbsoluteUri);
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("RR-to-RPM2", AppInfo.DisplayVersion));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private sealed class GitHubReleasePayload
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }
    }
}

internal sealed record GitHubRelease(string TagName, Version Version, string PageUrl);
