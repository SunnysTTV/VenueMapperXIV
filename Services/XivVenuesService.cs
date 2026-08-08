using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VenueMapper.UI;

namespace VenueMapper.Services;

public class XivVenuesService : IDisposable
{
    private const string ListUrl = "http://api.ffxivvenues.com/v1.0/venue";
    private const int CacheTtlMinutes = 30;

    private readonly HttpClient http;
    private readonly IPluginLog log;
    private readonly string cacheFilePath;

    private volatile Dictionary<string, ScheduleInfo?> scheduleById = new();
    private volatile Dictionary<string, string?> bannerById = new();

    private DateTime lastFetch = DateTime.MinValue;
    private bool fetching;

    public XivVenuesService(IPluginLog log, string configDirectory)
    {
        this.log = log;
        http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(20);

        Directory.CreateDirectory(configDirectory);
        cacheFilePath = Path.Combine(configDirectory, "xivvenues_cache.json");
        LoadFromDisk();
    }

    public ScheduleInfo? GetSchedule(string venueId)
    {
        if (string.IsNullOrEmpty(venueId)) return null;
        scheduleById.TryGetValue(venueId, out var info);
        return info;
    }

    public string? GetBannerUri(string venueId)
    {
        if (string.IsNullOrEmpty(venueId)) return null;
        bannerById.TryGetValue(venueId, out var uri);
        return uri;
    }

    public void RequestSchedule(string venueId = "")
    {
        if (fetching) return;
        if ((DateTime.Now - lastFetch).TotalMinutes < CacheTtlMinutes) return;
        fetching = true;
        _ = FetchAllAsync();
    }

    private void LoadFromDisk()
    {
        try
        {
            if (!File.Exists(cacheFilePath)) return;

            var json = File.ReadAllText(cacheFilePath);
            var disk = JsonConvert.DeserializeObject<DiskCache>(json);
            if (disk == null) return;

            scheduleById = disk.Schedules ?? new();
            bannerById = disk.Banners ?? new();
            lastFetch = disk.FetchedAt;
            log.Information($"[XIVVenues] Loaded {scheduleById.Count} schedules / {bannerById.Count} banners from disk cache (fetched {lastFetch:u})");
        }
        catch (Exception ex)
        {
            log.Debug($"[XIVVenues] Disk cache load failed: {ex.Message}");
        }
    }

    private void SaveToDisk()
    {
        try
        {
            var disk = new DiskCache { FetchedAt = lastFetch, Schedules = scheduleById, Banners = bannerById };
            File.WriteAllText(cacheFilePath, JsonConvert.SerializeObject(disk));
        }
        catch (Exception ex)
        {
            log.Debug($"[XIVVenues] Disk cache save failed: {ex.Message}");
        }
    }

    private async Task FetchAllAsync()
    {
        try
        {
            log.Information("[XIVVenues] Fetching full venue list");
            var response = await http.GetAsync(ListUrl);
            if (!response.IsSuccessStatusCode)
            {
                log.Warning($"[XIVVenues] list fetch: HTTP {response.StatusCode}");

                lastFetch = DateTime.Now;
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var arr = JArray.Parse(json);

            var newSchedules = new Dictionary<string, ScheduleInfo?>();
            var newBanners = new Dictionary<string, string?>();

            foreach (var venue in arr)
            {
                var id = venue["id"]?.Value<string>();
                if (string.IsNullOrEmpty(id)) continue;

                var bannerUri = venue["bannerUri"]?.Value<string>();
                if (!string.IsNullOrEmpty(bannerUri))
                    newBanners[id] = bannerUri;

                var resolution = venue["resolution"];

                if (resolution == null || resolution.Type != JTokenType.Object) continue;

                var isNow = resolution["isNow"]?.Value<bool>() ?? false;
                DateTime? start = null;
                DateTime? end = null;
                if (DateTimeOffset.TryParse(resolution["start"]?.ToString(), out var s)) start = s.UtcDateTime;
                if (DateTimeOffset.TryParse(resolution["end"]?.ToString(), out var e)) end = e.UtcDateTime;

                newSchedules[id] = new ScheduleInfo
                {
                    IsOpenNow = isNow,
                    NextStart = start,
                    NextEnd = end,
                };
            }

            scheduleById = newSchedules;
            bannerById = newBanners;
            lastFetch = DateTime.Now;
            SaveToDisk();
            log.Information($"[XIVVenues] Loaded {newSchedules.Count} schedules / {newBanners.Count} banners from {arr.Count} venues");
        }
        catch (Exception ex)
        {
            log.Debug($"[XIVVenues] List fetch failed: {ex.Message}");

            lastFetch = DateTime.Now;
        }
        finally
        {
            fetching = false;
        }
    }

    public void Dispose() => http.Dispose();

    private class DiskCache
    {
        public DateTime FetchedAt { get; set; }
        public Dictionary<string, ScheduleInfo?>? Schedules { get; set; }
        public Dictionary<string, string?>? Banners { get; set; }
    }

    public class ScheduleInfo
    {
        public bool IsOpenNow { get; set; }
        public DateTime? NextStart { get; set; }
        public DateTime? NextEnd { get; set; }

        public string GetStatusText()
        {
            if (IsOpenNow) return Lang.StatusOpenNow;
            if (NextStart == null) return "";
            var diff = NextStart.Value - DateTime.UtcNow;
            if (diff.TotalSeconds < 0) return "";
            if (diff.TotalMinutes < 60) return Lang.StatusOpensInMin((int)diff.TotalMinutes);
            if (diff.TotalHours < 24)   return Lang.StatusOpensInHours((int)diff.TotalHours);
            return Lang.StatusOpensInDays((int)diff.TotalDays);
        }
    }
}
