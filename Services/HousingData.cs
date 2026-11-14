using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace VenueMapper.Services;

public enum HousingDistrict { Unknown, Mist, LavenderBeds, Goblet, Shirogane, Empyreum }

public sealed class HousingData
{
    private static readonly HousingDistrict[] Order =
    [
        HousingDistrict.Mist, HousingDistrict.LavenderBeds, HousingDistrict.Goblet,
        HousingDistrict.Shirogane, HousingDistrict.Empyreum,
    ];

    private static readonly Dictionary<string, HousingDistrict> BgFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sea_s1"] = HousingDistrict.Mist,
        ["fst_f1"] = HousingDistrict.LavenderBeds,
        ["wil_w1"] = HousingDistrict.Goblet,
        ["est_e1"] = HousingDistrict.Shirogane,
        ["roc_r1"] = HousingDistrict.Empyreum,
    };

    private readonly IDataManager data;
    private readonly IPluginLog log;
    private readonly Dictionary<uint, HousingDistrict> districtByTerritory = new();
    private readonly Dictionary<uint, string?> floorByMap = new();
    private readonly Dictionary<uint, Dictionary<string, uint>> floorMapsByTerritory = new();
    private readonly Dictionary<HousingDistrict, byte[]> plotSizes = new();

    public HousingData(IDataManager data, IPluginLog log)
    {
        this.data = data;
        this.log = log;
    }

    public static uint WardTerritory(HousingDistrict district) => district switch
    {
        HousingDistrict.Mist         => 339,
        HousingDistrict.LavenderBeds => 340,
        HousingDistrict.Goblet       => 341,
        HousingDistrict.Shirogane    => 641,
        HousingDistrict.Empyreum     => 979,
        _                            => 0,
    };

    public static bool IsWardTerritory(uint territoryId) => territoryId is 339 or 340 or 341 or 641 or 979;

    public static string EnglishName(HousingDistrict district) => district switch
    {
        HousingDistrict.Mist         => "Mist",
        HousingDistrict.LavenderBeds => "Lavender Beds",
        HousingDistrict.Goblet       => "The Goblet",
        HousingDistrict.Shirogane    => "Shirogane",
        HousingDistrict.Empyreum     => "Empyreum",
        _                            => "",
    };

    public static int ComboIndex(HousingDistrict district) => Array.IndexOf(Order, district);

    public static HousingDistrict FromComboIndex(int index)
        => index >= 0 && index < Order.Length ? Order[index] : HousingDistrict.Unknown;

    public static HousingDistrict ParseDistrict(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return HousingDistrict.Unknown;
        foreach (var part in text.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Contains("Lavender", StringComparison.OrdinalIgnoreCase)) return HousingDistrict.LavenderBeds;
            if (part.Contains("Goblet", StringComparison.OrdinalIgnoreCase)) return HousingDistrict.Goblet;
            if (part.Contains("Shirogane", StringComparison.OrdinalIgnoreCase)) return HousingDistrict.Shirogane;
            if (part.Contains("Empyreum", StringComparison.OrdinalIgnoreCase)) return HousingDistrict.Empyreum;
            if (part.Contains("Mist", StringComparison.OrdinalIgnoreCase)) return HousingDistrict.Mist;
        }
        return HousingDistrict.Unknown;
    }

    public static string? FloorKeyFromPlaceName(string? englishName) => englishName switch
    {
        "Ground Floor" => "ground",
        "Second Floor" => "second",
        "Cellarage"    => "cellar",
        _              => null,
    };

    public HousingDistrict DistrictForTerritory(uint territoryId)
    {
        if (territoryId == 0) return HousingDistrict.Unknown;
        foreach (var district in Order)
            if (WardTerritory(district) == territoryId) return district;
        if (districtByTerritory.TryGetValue(territoryId, out var cached)) return cached;

        var result = HousingDistrict.Unknown;
        try
        {
            if (data.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var row))
            {
                var parts = row.Bg.ExtractText().Split('/');
                if (parts.Length > 1 && BgFolders.TryGetValue(parts[1], out var district))
                    result = district;
            }
        }
        catch (Exception ex)
        {
            log.Debug($"[VenueMapper] District lookup failed for territory {territoryId}: {ex.Message}");
        }

        districtByTerritory[territoryId] = result;
        return result;
    }

    public string? FloorForMap(uint mapId)
    {
        if (mapId == 0) return null;
        if (floorByMap.TryGetValue(mapId, out var cached)) return cached;

        string? floor = null;
        try
        {
            if (data.GetExcelSheet<Map>().TryGetRow(mapId, out var map)
                && data.GetExcelSheet<PlaceName>(ClientLanguage.English).TryGetRow(map.PlaceNameSub.RowId, out var sub))
                floor = FloorKeyFromPlaceName(sub.Name.ExtractText());
        }
        catch (Exception ex)
        {
            log.Debug($"[VenueMapper] Floor lookup failed for map {mapId}: {ex.Message}");
        }

        floorByMap[mapId] = floor;
        return floor;
    }

    public uint FloorMapId(uint territoryId, string? floorName)
    {
        if (territoryId == 0 || string.IsNullOrEmpty(floorName)) return 0;
        if (!floorMapsByTerritory.TryGetValue(territoryId, out var maps))
        {
            maps = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var map in data.GetExcelSheet<Map>())
                {
                    if (map.TerritoryType.RowId != territoryId) continue;
                    var floor = FloorForMap(map.RowId);
                    if (floor != null) maps.TryAdd(floor, map.RowId);
                }
            }
            catch (Exception ex)
            {
                log.Debug($"[VenueMapper] Floor map scan failed for territory {territoryId}: {ex.Message}");
            }
            floorMapsByTerritory[territoryId] = maps;
        }
        return maps.TryGetValue(floorName, out var id) ? id : 0;
    }

    public int PlotSize(HousingDistrict district, int plot)
    {
        var index = ComboIndex(district);
        if (index < 0 || plot < 1) return -1;
        if (!plotSizes.TryGetValue(district, out var sizes))
        {
            sizes = [];
            try
            {
                if (data.GetExcelSheet<HousingLandSet>().TryGetRow((uint)index, out var row))
                {
                    sizes = new byte[row.LandSet.Count];
                    for (var i = 0; i < sizes.Length; i++)
                        sizes[i] = row.LandSet[i].PlotSize;
                }
            }
            catch (Exception ex)
            {
                log.Debug($"[VenueMapper] Plot size lookup failed for {district}: {ex.Message}");
            }
            plotSizes[district] = sizes;
        }
        return plot <= sizes.Length ? sizes[plot - 1] : -1;
    }

    public const string GardenFloor = "garden";

    public static bool IsGarden(string? floorName) => string.Equals(floorName, GardenFloor, StringComparison.OrdinalIgnoreCase);

    public int NearestPlot(HousingDistrict district, float x, float z)
    {
        var territoryId = WardTerritory(district);
        if (territoryId == 0) return 0;
        try
        {
            if (!data.GetSubrowExcelSheet<HousingMapMarkerInfo>().TryGetRow(territoryId, out var markers))
                return 0;
            var best = 0;
            var bestDist = float.MaxValue;
            for (var i = 0; i < Math.Min(60, markers.Count); i++)
            {
                var m = markers[i];
                var dist = (m.X - x) * (m.X - x) + (m.Z - z) * (m.Z - z);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = i + 1;
                }
            }
            return best;
        }
        catch (Exception ex)
        {
            log.Debug($"[VenueMapper] Nearest plot lookup failed: {ex.Message}");
            return 0;
        }
    }

    private readonly Dictionary<uint, string?> loadingImages = new();

    public string? LoadingImagePath(uint territoryId)
    {
        if (territoryId == 0) return null;
        if (loadingImages.TryGetValue(territoryId, out var cached)) return cached;

        string? path = null;
        try
        {
            if (data.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var row))
            {
                var name = row.LoadingImage.ValueNullable?.FileName.ExtractText();
                if (!string.IsNullOrEmpty(name))
                    path = $"ui/loadingimage/{name}_hr1.tex";
            }
        }
        catch (Exception ex)
        {
            log.Debug($"[VenueMapper] Loading image lookup failed for {territoryId}: {ex.Message}");
        }
        loadingImages[territoryId] = path;
        return path;
    }

    private readonly Dictionary<uint, string> territoryNames = new();

    public string TerritoryName(uint territoryId)
    {
        if (territoryId == 0) return "";
        if (territoryNames.TryGetValue(territoryId, out var cached)) return cached;

        var name = "";
        try
        {
            if (data.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var row))
                name = row.PlaceName.ValueNullable?.Name.ExtractText() ?? "";
        }
        catch (Exception ex)
        {
            log.Debug($"[VenueMapper] Territory name lookup failed for {territoryId}: {ex.Message}");
        }
        territoryNames[territoryId] = name;
        return name;
    }

    public uint DefaultMapId(uint territoryId)
    {
        if (territoryId == 0) return 0;
        try
        {
            if (data.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var row))
                return row.Map.RowId;
        }
        catch (Exception ex)
        {
            log.Debug($"[VenueMapper] Default map lookup failed for {territoryId}: {ex.Message}");
        }
        return 0;
    }

    public (uint Id, string Name) NearestAetheryte(uint territoryId, float x, float z)
    {
        if (territoryId == 0) return (0, "");
        try
        {
            (uint Id, string Name) best = (0, "");
            var bestDist = float.MaxValue;
            foreach (var aetheryte in data.GetExcelSheet<Aetheryte>())
            {
                if (!aetheryte.IsAetheryte || aetheryte.Invisible || aetheryte.Territory.RowId != territoryId) continue;
                var name = aetheryte.PlaceName.ValueNullable?.Name.ExtractText() ?? "";
                if (string.IsNullOrEmpty(name)) continue;

                var dist = float.MaxValue - 1f;
                if (aetheryte.Level.Count > 0 && aetheryte.Level[0].ValueNullable is { } level)
                    dist = (level.X - x) * (level.X - x) + (level.Z - z) * (level.Z - z);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = (aetheryte.RowId, name);
                }
            }
            return best;
        }
        catch (Exception ex)
        {
            log.Debug($"[VenueMapper] Aetheryte lookup failed for {territoryId}: {ex.Message}");
            return (0, "");
        }
    }

    public string AetheryteName(uint aetheryteId)
    {
        if (aetheryteId == 0) return "";
        try
        {
            if (data.GetExcelSheet<Aetheryte>().TryGetRow(aetheryteId, out var row))
                return row.PlaceName.ValueNullable?.Name.ExtractText() ?? "";
        }
        catch (Exception ex)
        {
            log.Debug($"[VenueMapper] Aetheryte name lookup failed for {aetheryteId}: {ex.Message}");
        }
        return "";
    }

    public bool TryGetPlotMapLink(HousingDistrict district, int plot, out uint territoryId, out uint mapId, out Vector3 position)
    {
        territoryId = WardTerritory(district);
        mapId = 0;
        position = default;
        if (territoryId == 0 || plot < 1 || plot > 60) return false;

        try
        {
            if (!data.GetSubrowExcelSheet<HousingMapMarkerInfo>().TryGetSubrow(territoryId, (ushort)(plot - 1), out var marker))
                return false;
            mapId = marker.Map.RowId;
            position = new Vector3(marker.X, marker.Y, marker.Z);
            return mapId != 0;
        }
        catch (Exception ex)
        {
            log.Debug($"[VenueMapper] Plot marker lookup failed for {district} plot {plot}: {ex.Message}");
            return false;
        }
    }
}
