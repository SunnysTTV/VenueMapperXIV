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
    private readonly System.Threading.CancellationTokenSource cts = new();

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

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            log.Warning("VenueMapper: GitHub config URL is not an https address, skipping pull.");
            return PullResult.Failed;
        }

        try
        {
            var previousEtag = force ? null : ReadEtag();

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (!string.IsNullOrEmpty(previousEtag))
            {
                request.Headers.TryAddWithoutValidation("If-None-Match", previousEtag);
            }

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

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

            if (response.Content.Headers.ContentLength > MaxConfigBytes)
            {
                log.Warning($"VenueMapper: venue config is too large ({response.Content.Headers.ContentLength} bytes), keeping the current one.");
                return PullResult.Failed;
            }

            var content = await ReadLimitedAsync(response.Content);
            if (content == null)
            {
                log.Warning("VenueMapper: venue config is too large, keeping the current one.");
                return PullResult.Failed;
            }

            if (!configManager.SaveToCache(content, out var written))
                return PullResult.Failed;

            var newEtag = response.Headers.ETag?.Tag;
            if (await written && !string.IsNullOrEmpty(newEtag))
                WriteEtag(newEtag);
            else
                ClearEtag();

            log.Information("VenueMapper: Downloaded updated venue config.");
            return PullResult.Updated;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return PullResult.Skipped;
        }
        catch (Exception ex)
        {
            log.Error(ex, "VenueMapper: Failed to pull venue config from GitHub, falling back to cache.");
            return PullResult.Failed;
        }
    }

    private const long MaxConfigBytes = 4 * 1024 * 1024;

    private async Task<string?> ReadLimitedAsync(HttpContent content)
    {
        await using var stream = await content.ReadAsStreamAsync(cts.Token);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cts.Token)) > 0)
        {
            if (buffer.Length + read > MaxConfigBytes) return null;
            buffer.Write(chunk, 0, read);
        }
        var bytes = buffer.ToArray();
        var offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        return System.Text.Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);
    }

    private void ClearEtag()
    {
        try
        {
            if (File.Exists(etagFilePath)) File.Delete(etagFilePath);
        }
        catch (Exception ex)
        {
            log.Debug($"VenueMapper: Failed to remove ETag cache file: {ex.Message}");
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
            SafeFile.WriteAllText(etagFilePath, etag);
        }
        catch (Exception ex)
        {
            log.Error(ex, "VenueMapper: Failed to write ETag cache file.");
        }
    }

    public void Dispose()
    {
        cts.Cancel();
        httpClient.Dispose();
    }
}
