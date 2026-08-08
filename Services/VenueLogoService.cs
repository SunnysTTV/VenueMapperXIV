using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading.Tasks;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;

namespace VenueMapper.Services;

public class VenueLogoService : IDisposable
{
    private readonly HttpClient http = new();
    private readonly IPluginLog log;
    private readonly ITextureProvider textureProvider;

    private readonly ConcurrentDictionary<string, IDalamudTextureWrap?> cache = new();
    private readonly ConcurrentDictionary<string, bool> loading = new();

    public VenueLogoService(ITextureProvider textureProvider, IPluginLog log)
    {
        this.textureProvider = textureProvider;
        this.log = log;
    }

    public IDalamudTextureWrap? Get(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        if (cache.TryGetValue(url, out var wrap)) return wrap;
        if (loading.TryAdd(url, true))
            _ = LoadAsync(url);
        return null;
    }

    private async Task LoadAsync(string url)
    {
        try
        {
            var bytes = await http.GetByteArrayAsync(url);
            var wrap = await textureProvider.CreateFromImageAsync(bytes);
            cache[url] = wrap;
        }
        catch (Exception ex)
        {
            log.Debug($"[VenueLogo] Failed to load {url}: {ex.Message}");
            cache[url] = null;
        }
        finally
        {
            loading.TryRemove(url, out _);
        }
    }

    public void Dispose()
    {
        http.Dispose();
        foreach (var wrap in cache.Values)
            wrap?.Dispose();
    }
}
