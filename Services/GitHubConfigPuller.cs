using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace VenueMapper.Services;

public enum PullResult
{
    Updated,
    Unchanged,
    Failed,
    Skipped,
}

public class GitHubConfigPuller : IDisposable
{
    private readonly HttpClient httpClient;
    private readonly IPluginLog log;
    private readonly ConfigManager configManager;
    private readonly string etagFilePath;

    public GitHubConfigPuller(IPluginLog log, ConfigManager configManager)
    {
        this.log = log;
        this.configManager = configManager;
        httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(15);
        etagFilePath = Path.Combine(configManager.ConfigDirectory, "venues.etag");
    }

    public async Task<PullResult> PullAsync(string url, bool force = false)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            log.Warning("VenueMapper: GitHub config URL is not set, skipping pull.");
            return PullResult.Skipped;
        }

        try
        {
            var previousEtag = force ? null : ReadEtag();

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(previousEtag))
            {
                request.Headers.TryAddWithoutValidation("If-None-Match", previousEtag);
            }

            using var response = await httpClient.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
            {
                log.Information("VenueMapper: Remote config unchanged (304).");
                return PullResult.Unchanged;
            }

            if (!response.IsSuccessStatusCode)
            {
                log.Warning($"VenueMapper: GitHub pull failed with status {response.StatusCode}");
                return PullResult.Failed;
            }

            var content = await response.Content.ReadAsStringAsync();
            configManager.SaveToCache(content);

            var newEtag = response.Headers.ETag?.Tag;
            if (!string.IsNullOrEmpty(newEtag))
            {
                WriteEtag(newEtag);
            }

            log.Information("VenueMapper: Downloaded updated venue config.");
            return PullResult.Updated;
        }
        catch (Exception ex)
        {
            log.Error(ex, "VenueMapper: Failed to pull venue config from GitHub, falling back to cache.");
            return PullResult.Failed;
        }
    }

    private string? ReadEtag()
    {
        try
        {
            return File.Exists(etagFilePath) ? File.ReadAllText(etagFilePath).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private void WriteEtag(string etag)
    {
        try
        {
            File.WriteAllText(etagFilePath, etag);
        }
        catch (Exception ex)
        {
            log.Error(ex, "VenueMapper: Failed to write ETag cache file.");
        }
    }

    public void Dispose() => httpClient.Dispose();
}
