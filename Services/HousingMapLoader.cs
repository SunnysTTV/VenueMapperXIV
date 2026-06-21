using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace VenueMapper.Services;

public class HousingMapLoader : IDisposable
{
    private readonly IDataManager dataManager;
    private readonly ITextureProvider textureProvider;
    private readonly IPluginLog log;

    private readonly Dictionary<uint, MapInfo> infoCache = new();
    private readonly Dictionary<string, ISharedImmediateTexture> textureCache = new();

    public record MapInfo(string? Path, short OffsetX, short OffsetY, ushort SizeFactor);

    private static readonly (uint Min, uint Max, string Folder)[] HousingDistrictRanges =
    [
        (339,  344,  "h0a0"), // Mist
        (345,  350,  "h0b0"), // Lavender Beds
        (351,  356,  "h0c0"), // The Goblet
        (655,  660,  "h0d0"), // Shirogane
        (665,  670,  "h0e0"), // Empyreum
    ];

    public HousingMapLoader(IDataManager dataManager, ITextureProvider textureProvider, IPluginLog log)
    {
        this.dataManager = dataManager;
        this.textureProvider = textureProvider;
        this.log = log;
    }

    public ISharedImmediateTexture? GetMapTexture(uint territoryId, uint mapId = 0)
    {
        var info = mapId > 0 ? GetMapInfoByMapId(mapId) : null;
        info ??= GetMapInfo(territoryId);
        if (info?.Path == null) return null;

        var cacheKey = $"{territoryId}_{mapId}";
        if (resolvedPathCache.TryGetValue(cacheKey, out var resolvedPath))
        {
            if (resolvedPath == null) return null;
            if (textureCache.TryGetValue(resolvedPath, out var cached))
                return cached;
        }

        var paths = BuildCandidatePaths(info.Path);
        foreach (var path in paths)
        {
            if (textureCache.TryGetValue(path, out var tex))
            {
                resolvedPathCache[cacheKey] = path;
                return tex;
            }

            if (!dataManager.FileExists(path))
            {
                log.Debug($"[MapLoader] Not found: {path}");
                continue;
            }

            log.Information($"[MapLoader] Found map file: {path}");
            try
            {
                var loaded = textureProvider.GetFromGame(path);
                textureCache[path] = loaded;
                resolvedPathCache[cacheKey] = path;
                return loaded;
            }
            catch (Exception ex)
            {
                log.Warning($"[MapLoader] Failed to load '{path}': {ex.Message}");
            }
        }

        log.Warning($"[MapLoader] No map file found. Tried: {string.Join(", ", paths)}");
        resolvedPathCache[cacheKey] = null;
        return null;
    }

    private readonly Dictionary<string, string?> resolvedPathCache = new();

    private static string MapIdToPath(string rawId)
    {
        var slash = rawId.IndexOf('/');
        if (slash > 0)
        {
            var folder = rawId[..slash];   // "h2i3"
            var index  = rawId[(slash + 1)..]; // "01"
            return $"ui/map/{folder}/{index}/{folder}{index}_m.tex";
        }

        return $"ui/map/{rawId}/{rawId}_m.tex";
    }

    private static List<string> BuildCandidatePaths(string primaryPath)
    {
        return [primaryPath];
    }

    public MapInfo? GetMapInfoByMapId(uint mapId)
    {
        var cacheKey = mapId + 1000000u;
        if (infoCache.TryGetValue(cacheKey, out var cached))
            return cached.Path != null ? cached : null;

        try
        {
            var sheet = dataManager.GetExcelSheet<Map>();
            var map = sheet?.GetRow(mapId);
            if (map == null) return null;

            var rawId = map.Value.Id.ToString();
            if (string.IsNullOrWhiteSpace(rawId))
            {
                log.Debug($"[MapLoader] MapId {mapId}: Id is empty");
                return null;
            }

            var path = MapIdToPath(rawId);

            log.Information($"[MapLoader] MapId {mapId} -> '{rawId}' -> {path}");
            var info = new MapInfo(path, map.Value.OffsetX, map.Value.OffsetY, map.Value.SizeFactor);
            infoCache[cacheKey] = info;
            return info;
        }
        catch (Exception ex)
        {
            log.Warning($"[MapLoader] MapId {mapId} lookup failed: {ex.Message}");
            return null;
        }
    }

    public MapInfo GetMapInfo(uint territoryId)
    {
        if (infoCache.TryGetValue(territoryId, out var cached))
            return cached;

        foreach (var (min, max, folder) in HousingDistrictRanges)
        {
            if (territoryId >= min && territoryId <= max)
            {
                var houPath = $"bgcommon/hou/{folder}/s0_0_0.tex";
                log.Debug($"[MapLoader] Territory {territoryId} -> bgcommon/hou path: {houPath}");
                var result = new MapInfo(houPath, 0, 0, 100);
                infoCache[territoryId] = result;
                return result;
            }
        }

        try
        {
            var sheet = dataManager.GetExcelSheet<TerritoryType>();
            var row = sheet?.GetRow(territoryId);
            if (row == null)
            {
                log.Warning($"[MapLoader] Territory {territoryId}: no Lumina row");
                infoCache[territoryId] = new MapInfo(null, 0, 0, 100);
                return infoCache[territoryId];
            }

            var map   = row.Value.Map.Value;
            var rawId = map.Id.ToString();

            log.Information($"[MapLoader] Territory {territoryId}: Map.RowId={map.RowId}, Map.Id='{rawId}', " +
                            $"SizeFactor={map.SizeFactor}, OffsetX={map.OffsetX}, OffsetY={map.OffsetY}, " +
                            $"Bg='{row.Value.Bg}'");

            if (string.IsNullOrWhiteSpace(rawId))
            {
                var bg = row.Value.Bg.ToString();
                log.Information($"[MapLoader] Empty Map.Id - trying Bg-based lookup. Bg='{bg}'");

                if (map.RowId > 0)
                {
                    var mapRowPath = $"ui/map/{map.RowId:D4}/{map.RowId:D4}_00_m.tex";
                    log.Information($"[MapLoader] Trying RowId-based path: {mapRowPath}");
                }

                if (!string.IsNullOrEmpty(bg))
                {
                    var parts = bg.Split('/');
                    foreach (var part in parts)
                    {
                        if (part.StartsWith("hou_in", StringComparison.OrdinalIgnoreCase) ||
                            part.Contains("house", StringComparison.OrdinalIgnoreCase))
                        {
                            var tryPath = $"ui/map/{part}/{part}_00_m.tex";
                            log.Information($"[MapLoader] Trying Bg-derived path: {tryPath}");
                            var bgResult = new MapInfo(tryPath, map.OffsetX, map.OffsetY,
                                map.SizeFactor > 0 ? map.SizeFactor : (ushort)100);
                            infoCache[territoryId] = bgResult;
                            return bgResult;
                        }
                    }
                }

                log.Warning($"[MapLoader] No map path derivable for territory {territoryId}");
                infoCache[territoryId] = new MapInfo(null, 0, 0, 100);
                return infoCache[territoryId];
            }

            var path = MapIdToPath(rawId);

            log.Debug($"[MapLoader] Territory {territoryId} -> {path}");
            var info = new MapInfo(path, map.OffsetX, map.OffsetY, map.SizeFactor);
            infoCache[territoryId] = info;
            return info;
        }
        catch (Exception ex)
        {
            log.Warning($"[MapLoader] Lumina lookup failed for territory {territoryId}: {ex.Message}");
            infoCache[territoryId] = new MapInfo(null, 0, 0, 100);
            return infoCache[territoryId];
        }
    }

    public List<(uint MapId, string Index)> DiscoverSiblingMaps(uint knownMapId)
    {
        var result = new List<(uint MapId, string Index)>();
        try
        {
            var sheet = dataManager.GetExcelSheet<Map>();
            if (sheet == null) return result;

            var knownRow = sheet.GetRow(knownMapId);
            var knownId = knownRow.Id.ToString();
            if (string.IsNullOrEmpty(knownId)) return result;
            var slash = knownId.IndexOf('/');
            if (slash <= 0) return result;

            var prefix = knownId[..slash]; // e.g. "h2i3"

            var start = knownMapId > 20 ? knownMapId - 20 : 0u;
            for (var rowId = start; rowId <= knownMapId + 20; rowId++)
            {
                try
                {
                    var row = sheet.GetRow(rowId);
                    var id = row.Id.ToString();
                    if (string.IsNullOrEmpty(id)) continue;
                    if (id.StartsWith(prefix + "/", StringComparison.Ordinal))
                    {
                        var idx = id[(prefix.Length + 1)..];
                        result.Add((MapId: rowId, Index: idx));
                    }
                }
                catch { }
            }

            result.Sort((a, b) => string.Compare(a.Index, b.Index, StringComparison.Ordinal));
            log.Information($"[MapLoader] Discovered {result.Count} sibling maps for prefix '{prefix}': {string.Join(", ", result.Select(r => $"{r.MapId}={prefix}/{r.Index}"))}");
        }
        catch (Exception ex)
        {
            log.Warning($"[MapLoader] DiscoverSiblingMaps failed: {ex.Message}");
        }

        return result;
    }

    public static float WorldToUV(float world, short offset, ushort sizeFactor)
    {
        var pixel = world * (sizeFactor / 100f) + 1024f + offset;
        return pixel / 2048f;
    }

    public void Dispose()
    {
        textureCache.Clear();
    }
}
