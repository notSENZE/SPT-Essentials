using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using Version = SemanticVersioning.Version;

namespace SPTEssentials.Server.Updates;

[Injectable(InjectionType.Singleton)]
public sealed class UpdateNotificationService(ISptLogger<UpdateNotificationService> logger)
{
    private const string LatestReleaseApiUrl =
        "https://api.github.com/repos/notSENZE/SPT-Essentials/releases/latest";
    private const string ReleasesUrl =
        "https://github.com/notSENZE/SPT-Essentials/releases/latest";

    private static readonly HttpClient HttpClient = CreateHttpClient();

    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            var release = await HttpClient.GetFromJsonAsync<GitHubRelease>(
                LatestReleaseApiUrl,
                cancellationToken);

            if (release?.TagName is null
                || !Version.TryParse(release.TagName.Trim().TrimStart('v', 'V'), true, out var latestVersion)
                || !Version.TryParse(ModInfo.Version, out var installedVersion)
                || latestVersion <= installedVersion)
            {
                return;
            }

            var downloadUrl = string.IsNullOrWhiteSpace(release.HtmlUrl)
                ? ReleasesUrl
                : release.HtmlUrl;

            logger.Warning(
                $"{ModInfo.LogPrefix} Update available: {latestVersion} "
                + $"(installed: {installedVersion}). Download: {downloadUrl}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.Debug($"{ModInfo.LogPrefix} Update check timed out.");
        }
        catch (HttpRequestException exception)
        {
            logger.Debug($"{ModInfo.LogPrefix} Update check failed: {exception.Message}");
        }
        catch (JsonException exception)
        {
            logger.Debug($"{ModInfo.LogPrefix} GitHub returned an invalid update response: {exception.Message}");
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"SPTEssentials/{ModInfo.Version}");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }
    }
}
