using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Newtonsoft.Json.Linq;

namespace VenueMapper.Services;

public class XivVenuesService : IDisposable
{
    private const string BaseUrl = "http://api.ffxivvenues.com/v1.0/venue/";
    private const int CacheTtlMinutes = 30;

    private readonly HttpClient http;
    private readonly IPluginLog log;

    private readonly ConcurrentDictionary<string, ScheduleInfo?> cache = new();
    private readonly ConcurrentDictionary<string, DateTime> fetchTimes = new();
    private readonly ConcurrentDictionary<string, bool> queued = new();
    private readonly ConcurrentQueue<string> queue = new();
    private bool processing;

    public XivVenuesService(IPluginLog log)
    {
        this.log = log;
        http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(10);
    }

    public ScheduleInfo? GetSchedule(string venueId)
    {
        if (string.IsNullOrEmpty(venueId)) return null;
        cache.TryGetValue(venueId, out var info);
        return info;
    }

    public void RequestSchedule(string venueId)
    {
        if (string.IsNullOrEmpty(venueId)) return;
        if (fetchTimes.TryGetValue(venueId, out var t) && (DateTime.Now - t).TotalMinutes < CacheTtlMinutes) return;
        if (!queued.TryAdd(venueId, true)) return;

        queue.Enqueue(venueId);
        if (!processing)
            _ = ProcessQueue();
    }

    private async Task ProcessQueue()
    {
        processing = true;
        try
        {
            while (queue.TryDequeue(out var venueId))
            {
                var delay = await FetchOne(venueId);
                queued.TryRemove(venueId, out _);
                await Task.Delay(delay);
            }
        }
        finally
        {
            processing = false;
        }
    }

    private async Task<int> FetchOne(string venueId)
    {
        try
        {
            log.Information($"[XIVVenues] Fetching {venueId}");
            var response = await http.GetAsync(BaseUrl + venueId);
            if (!response.IsSuccessStatusCode)
            {
                log.Warning($"[XIVVenues] {venueId}: HTTP {response.StatusCode}");
                if ((int)response.StatusCode == 429)
                    return 15000;
                return 15000;
            }

            var json = await response.Content.ReadAsStringAsync();
            var root = JObject.Parse(json);

            var schedule = root["schedule"] as JArray;
            if (schedule == null || schedule.Count == 0)
            {
                cache[venueId] = null;
                fetchTimes[venueId] = DateTime.Now;
                return 15000;
            }

            var resolution = schedule[0]?["resolution"];
            if (resolution == null)
            {
                cache[venueId] = null;
                fetchTimes[venueId] = DateTime.Now;
                return 15000;
            }

            var isNow = resolution["isNow"]?.Value<bool>() ?? false;
            DateTime? start = null;
            DateTime? end = null;

            if (DateTime.TryParse(resolution["start"]?.ToString(), out var s)) start = s;
            if (DateTime.TryParse(resolution["end"]?.ToString(), out var e)) end = e;

            cache[venueId] = new ScheduleInfo
            {
                IsOpenNow = isNow,
                NextStart = start,
                NextEnd = end,
            };
            fetchTimes[venueId] = DateTime.Now;
            log.Information($"[XIVVenues] {venueId}: isNow={isNow} start={start} end={end}");
        }
        catch (Exception ex)
        {
            log.Debug($"[XIVVenues] Failed for {venueId}: {ex.Message}");
        }
        return 15000;
    }

    public void Dispose() => http.Dispose();

    public class ScheduleInfo
    {
        public bool IsOpenNow { get; set; }
        public DateTime? NextStart { get; set; }
        public DateTime? NextEnd { get; set; }

        public string GetStatusText(bool german)
        {
            if (IsOpenNow)
                return german ? "JETZT OFFEN" : "OPEN NOW";

            if (NextStart == null) return "";

            var diff = NextStart.Value - DateTime.UtcNow;
            if (diff.TotalSeconds < 0) return "";

            if (diff.TotalMinutes < 60)
                return german ? $"Oeffnet in {(int)diff.TotalMinutes}min" : $"Opens in {(int)diff.TotalMinutes}min";
            if (diff.TotalHours < 24)
                return german ? $"Oeffnet in {(int)diff.TotalHours}h" : $"Opens in {(int)diff.TotalHours}h";
            return german ? $"Oeffnet in {(int)diff.TotalDays}d" : $"Opens in {(int)diff.TotalDays}d";
        }
    }
}
