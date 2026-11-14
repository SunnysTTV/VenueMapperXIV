using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;

namespace VenueMapper.Services;

public class VenueLogoService : IDisposable
{
    private const long MaxImageBytes = 4 * 1024 * 1024;

    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly IPluginLog log;
    private readonly ITextureProvider textureProvider;
    private readonly CancellationTokenSource cts = new();

    private readonly ConcurrentDictionary<string, IDalamudTextureWrap?> cache = new();
    private readonly ConcurrentDictionary<string, bool> loading = new();

    public VenueLogoService(ITextureProvider textureProvider, IPluginLog log)
    {
        this.textureProvider = textureProvider;
        this.log = log;
    }

    private const int MaxAttempts = 3;
    private static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(5);

    private sealed record Failure(int Attempts, DateTime RetryAt);
    private readonly ConcurrentDictionary<string, Failure> failures = new();

    public IDalamudTextureWrap? Get(string? url)
    {
        if (string.IsNullOrEmpty(url) || cts.IsCancellationRequested) return null;
        if (cache.TryGetValue(url, out var wrap))
        {
            if (wrap != null) return wrap;
            if (!failures.TryGetValue(url, out var failure) || failure.Attempts >= MaxAttempts || DateTime.UtcNow < failure.RetryAt)
                return null;
            cache.TryRemove(url, out _);
        }
        if (!IsSecureUrl(url))
        {
            cache[url] = null;
            return null;
        }
        if (loading.TryAdd(url, true))
            _ = LoadAsync(url);
        return null;
    }

    private static bool IsSecureUrl(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps;

    private async Task LoadAsync(string url)
    {
        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxImageBytes)
                throw new InvalidDataException($"image too large ({response.Content.Headers.ContentLength} bytes)");

            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, cts.Token)) > 0)
            {
                if (buffer.Length + read > MaxImageBytes)
                    throw new InvalidDataException("image too large");
                buffer.Write(chunk, 0, read);
            }

            var wrap = await textureProvider.CreateFromImageAsync(buffer.ToArray(), cancellationToken: cts.Token);
            cache[url] = wrap;
            failures.TryRemove(url, out _);
            if (cts.IsCancellationRequested && cache.TryRemove(url, out var late))
                late?.Dispose();
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            log.Debug($"[VenueLogo] Failed to load {url}: {ex.Message}");
            failures.AddOrUpdate(url, _ => new Failure(1, DateTime.UtcNow + RetryAfter),
                (_, old) => new Failure(old.Attempts + 1, DateTime.UtcNow + RetryAfter));
            cache[url] = null;
        }
        finally
        {
            loading.TryRemove(url, out _);
        }
    }

    public void Dispose()
    {
        cts.Cancel();
        http.Dispose();
        foreach (var wrap in cache.Values)
            wrap?.Dispose();
        cache.Clear();
    }
}
