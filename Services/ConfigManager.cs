using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VenueMapper.Models;

namespace VenueMapper.Services;

public class ConfigManager
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
    };

    private readonly IPluginLog log;
    private readonly string configDirectory;
    private readonly string cacheFilePath;

    private volatile VenueConfig? config;
    public VenueConfig? Config { get => config; private set => config = value; }

    public ConfigManager(IPluginLog log, string configDirectory)
    {
        this.log = log;
        this.configDirectory = configDirectory;
        cacheFilePath = Path.Combine(configDirectory, "venues_cache.json");
        localFestivalsPath = Path.Combine(configDirectory, "festivals_local.json");

        Directory.CreateDirectory(configDirectory);
    }

    public void Load(string bundledResourcePath)
    {
        if (File.Exists(cacheFilePath) && TryLoadFromFile(cacheFilePath))
            return;

        TryLoadFromFile(bundledResourcePath);
    }

    private bool TryLoadFromFile(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            if (!TryParse(json, out var parsed, out var error))
            {
                log.Error($"Failed to load venue config from {path}: {error}");
                return false;
            }

            ApplyLocalFestivals(parsed!);
            Config = parsed;
            return true;
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to load venue config from {path}");
            return false;
        }
    }

    private readonly object cacheLock = new();

    public bool SaveToCache(string json) => SaveToCache(json, out _);

    public bool SaveToCache(string json, out System.Threading.Tasks.Task<bool> written)
    {
        written = System.Threading.Tasks.Task.FromResult(false);
        lock (cacheLock)
        {
            if (!TryParse(json, out var parsed, out var error))
            {
                log.Error($"Downloaded venue config rejected, keeping the current one: {error}");
                return false;
            }

            written = SafeFile.WriteAllTextAsync(cacheFilePath, json);

            ApplyLocalFestivals(parsed!);
            Config = parsed;
            return true;
        }
    }

    private readonly string localFestivalsPath;

    private List<Festival> remoteFestivals = new();
    private readonly object localLock = new();

    private List<Festival> ReadLocalFestivals()
    {
        if (!File.Exists(localFestivalsPath)) return new List<Festival>();
        var token = JToken.Parse(File.ReadAllText(localFestivalsPath));
        var array = token as JArray ?? token["festivals"] as JArray;
        return array?.ToObject<List<Festival>>(JsonSerializer.Create(JsonSettings)) ?? new List<Festival>();
    }

    private List<Festival>? localFestivals;

    private List<Festival> LocalFestivals() => localFestivals ??= ReadLocalFestivals();

    private void ApplyLocalFestivals(VenueConfig cfg)
    {
        lock (localLock)
        {
            localFestivals = null;
            remoteFestivals = cfg.Festivals.ToList();
            cfg.Festivals = Combined(true);
        }
    }

    private Festival? demoFestival;
    public Festival? DemoFestival => demoFestival;

    public void SetDemoFestival(Festival? festival)
    {
        lock (localLock)
        {
            demoFestival = festival;
            var current = Config;
            if (current != null)
                current.Festivals = Combined(false);
        }
    }

    private List<Festival> Combined(bool logMerge)
    {
        var merged = remoteFestivals.ToList();
        try
        {
            var local = LocalFestivals();
            if (local.Count > 0)
            {
                merged = Merge(remoteFestivals, local);
                if (logMerge) log.Information("VenueMapper: merged local festivals from festivals_local.json");
            }
        }
        catch (Exception ex)
        {
            log.Warning($"VenueMapper: could not read festivals_local.json: {ex.Message}");
        }
        if (demoFestival == null) return merged;
        return merged.Where(f => !f.IsLocalPreview && f.FestivalId != demoFestival.FestivalId).Append(demoFestival).ToList();
    }

    private static List<Festival> Merge(List<Festival> remote, List<Festival> local)
    {
        NormalizeFestivals(local);
        foreach (var festival in local)
            festival.IsLocalPreview = true;
        var ids = local.Select(f => f.FestivalId).ToHashSet();
        return remote.Where(f => !ids.Contains(f.FestivalId)).Concat(local).ToList();
    }

    public bool HasLocalFestival(string festivalId)
        => Config?.Festivals.Any(f => f.IsLocalPreview && f.FestivalId == festivalId) == true;

    public bool SaveLocalFestival(Festival festival)
        => UpdateLocalFestivals(list =>
        {
            list.RemoveAll(f => f.FestivalId == festival.FestivalId);
            list.Add(festival);
        });

    public bool RemoveLocalFestival(string festivalId)
        => UpdateLocalFestivals(list => list.RemoveAll(f => f.FestivalId == festivalId));

    private bool UpdateLocalFestivals(Action<List<Festival>> change)
    {
        lock (localLock)
        {
            try
            {
                var list = LocalFestivals().ToList();
                change(list);
                if (list.Count == 0)
                    SafeFile.Delete(localFestivalsPath);
                else
                    SafeFile.WriteAllText(localFestivalsPath, JsonConvert.SerializeObject(list, Formatting.Indented, JsonSettings));
                localFestivals = list;

                var current = Config;
                if (current != null)
                    current.Festivals = Combined(false);
                return true;
            }
            catch (Exception ex)
            {
                log.Warning($"VenueMapper: could not update festivals_local.json: {ex.Message}");
                return false;
            }
        }
    }

    private static bool TryParse(string json, out VenueConfig? parsed, out string? error)
    {
        parsed = null;
        try
        {
            parsed = JsonConvert.DeserializeObject<VenueConfig>(json, JsonSettings);
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }

        if (parsed == null)
        {
            error = "empty document";
            return false;
        }

        try
        {
            Normalize(parsed);
        }
        catch (Exception ex)
        {
            error = $"invalid structure: {ex.Message}";
            parsed = null;
            return false;
        }

        if (parsed.Venues.Count == 0)
        {
            error = "no venues";
            parsed = null;
            return false;
        }

        error = null;
        return true;
    }

    private static void Normalize(VenueConfig cfg)
    {
        cfg.Venues.RemoveAll(v => v == null || string.IsNullOrEmpty(v.VenueId));
        foreach (var venue in cfg.Venues)
        {
            venue.Floors.RemoveAll(f => f == null);
            foreach (var floor in venue.Floors)
                floor.Services.RemoveAll(s => s == null);
            venue.OwnerIdHashes.RemoveAll(string.IsNullOrEmpty);
            venue.Rules ??= new List<VenueRule>();
            venue.RulesDe ??= new List<VenueRule>();
            venue.Rules.RemoveAll(r => r == null || string.IsNullOrWhiteSpace(r.Text));
            venue.RulesDe.RemoveAll(r => r == null || string.IsNullOrWhiteSpace(r.Text));

            if (!venue.Floors.Any(f => string.Equals(f.Name, "ground", StringComparison.OrdinalIgnoreCase)))
                venue.Floors.Insert(0, new Floor { Name = "ground", YMin = -3.5f, YMax = 6.5f });
        }

        NormalizeFestivals(cfg.Festivals);
    }

    private static void NormalizeFestivals(List<Festival> festivals)
    {
        festivals.RemoveAll(f => f == null || string.IsNullOrEmpty(f.FestivalId) || string.IsNullOrEmpty(f.Name) || !HasValidLocation(f));
        foreach (var festival in festivals)
        {
            festival.Services.RemoveAll(s => s == null);
            festival.Days ??= new List<FestivalDay>();
            festival.Days.RemoveAll(d => d == null);
            foreach (var day in festival.Days)
            {
                day.Start = ToUtc(day.Start);
                day.End = ToUtc(day.End);
                if (day.End <= day.Start)
                    day.End = day.Start.AddHours(4);
            }
            festival.Days.Sort((a, b) => a.Start.CompareTo(b.Start));
            if (festival.Days.Count > 0)
            {
                festival.Start = festival.Days[0].Start;
                festival.End = festival.Days.Max(d => d.End);
            }
            festival.Start = ToUtc(festival.Start);
            festival.End = ToUtc(festival.End);
            if (festival.End <= festival.Start)
                festival.End = festival.Start.AddHours(4);
            festival.Location.Houses ??= new List<FestivalHouse>();
            festival.Location.Houses.RemoveAll(h => h == null || h.Plot is < 1 or > 60);
            if (festival.Location.Radius <= 0)
                festival.Location.Radius = 50f;
        }
    }

    private static bool HasValidLocation(Festival festival)
    {
        var location = festival.Location;
        if (location == null) return false;
        return festival.IsWard
            ? location.Ward > 0 && HousingData.ParseDistrict(location.District) != HousingDistrict.Unknown
            : location.TerritoryId != 0;
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc   => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _                  => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    public string CacheFilePath => cacheFilePath;
    public string ConfigDirectory => configDirectory;
}
