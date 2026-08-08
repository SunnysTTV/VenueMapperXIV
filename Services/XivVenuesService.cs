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

// Fetches the FFXIVVenues API's full venue list in a single request and serves individual
// venues' schedule/banner out of the resulting in-memory snapshot, instead of one HTTP request
// per venue. Per the API's own maintainer: a plugin doing 20-30 individual per-venue calls
// (even spread out under the rate limit) turns into thousands of requests at real install
// scale, whereas one bulk call + in-memory LINQ lookups costs the API the same regardless of
// how many venues the plugin ends up tracking.
//
// The result is also persisted to disk (like ConfigManager's venues_cache.json), so a plugin
// reload or game restart doesn't lose the cache and force an immediate re-fetch - the 30-minute
// TTL is measured from the persisted fetch time, not from when this service instance happened
// to be constructed, so it's still just one real API call per 30 minutes regardless of how often
// the plugin itself gets reloaded in between.
public class XivVenuesService : IDisposable
{
    private const string ListUrl = "http://api.ffxivvenues.com/v1.0/venue";
    private const int CacheTtlMinutes = 30;

    private readonly HttpClient http;
    private readonly IPluginLog log;
    private readonly string cacheFilePath;

    // Swapped wholesale on refresh (never mutated in place), so reads from the render thread
    // never observe a partially-updated snapshot - `volatile` just guarantees the swap is
    // visible across threads promptly instead of a reader spinning on a stale cached reference.
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

    /// <summary>Kept as the entry point call sites already use per-venue - now just triggers/refreshes the shared bulk snapshot instead of queuing an individual fetch. venueId is unused (the whole list is always fetched); the parameter is optional so this also works as a plain startup prefetch call.</summary>
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
                // Still counts as "attempted" for cooldown purposes - without this, a failure (e.g.
                // 429 Too Many Requests) left lastFetch unset, so every caller's next RequestSchedule()
                // saw the TTL as still expired and immediately fired another fetch, in a tight retry
                // loop that hammered the API dozens of times per second instead of backing off.
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

                // The venue's own top-level "resolution" is FFXIVVenues' server-computed answer
                // across ALL of the venue's schedule entries combined (a venue can have more than
                // one weekly recurrence, e.g. Mondays AND Fridays) - using schedule[0]'s resolution
                // instead looked only at the FIRST entry, so a venue whose first-listed day wasn't
                // the currently-active one showed the wrong status (e.g. "opens in 3 days" while a
                // later entry in the array was actually open right now).
                var resolution = venue["resolution"];
                // A JSON "resolution": null still comes back as a non-C#-null JValue (Type.Null),
                // not an absent token - indexing into it with resolution["isNow"] then throws
                // ("Cannot access child value on JValue"), aborting the whole fetch's parsing loop.
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
            // Same reasoning as the HTTP-failure branch above - without this, a parse exception
            // (e.g. an unexpected null field) would leave lastFetch unset and retry instantly.
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
