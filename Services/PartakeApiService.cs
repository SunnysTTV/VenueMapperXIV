using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VenueMapper.Models;

namespace VenueMapper.Services;

public class PartakeApiService : IDisposable
{
    private const string Endpoint = "https://api.partake.gg/";
    private static readonly TimeSpan SuccessTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan FailureBackoff = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan IconRetryAfter = TimeSpan.FromMinutes(30);

    private readonly HttpClient http;
    private readonly IPluginLog log;
    private readonly CancellationTokenSource cts = new();

    private readonly ConcurrentDictionary<int, List<VenueEvent>> eventsByTeam = new();
    private readonly ConcurrentDictionary<int, DateTime> nextFetchAt = new();
    private readonly ConcurrentDictionary<int, string?> errorByTeam = new();
    private readonly ConcurrentDictionary<int, bool> fetching = new();
    private readonly ConcurrentDictionary<int, string?> iconUrlByTeam = new();
    private readonly ConcurrentDictionary<int, DateTime> nextIconFetchAt = new();
    private readonly ConcurrentDictionary<int, bool> fetchingIcon = new();

    public bool IsLoading(int teamId) => fetching.ContainsKey(teamId);
    public bool HasLoaded(int teamId) => eventsByTeam.ContainsKey(teamId);
    public string? GetError(int teamId) => errorByTeam.GetValueOrDefault(teamId);
    public string? GetTeamIconUrl(int teamId) => iconUrlByTeam.GetValueOrDefault(teamId);

    public PartakeApiService(IPluginLog log)
    {
        this.log = log;
        http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        http.DefaultRequestHeaders.Add("User-Agent", $"VenueMapper-Dalamud/{ver?.Major}.{ver?.Minor}.{ver?.Build}");
    }

    public List<VenueEvent> GetEvents(int teamId)
        => eventsByTeam.GetValueOrDefault(teamId) ?? [];

    public void RequestTeam(int teamId)
    {
        if (teamId <= 0 || cts.IsCancellationRequested || fetching.ContainsKey(teamId)) return;
        if (nextFetchAt.TryGetValue(teamId, out var next) && DateTime.UtcNow < next) return;
        _ = FetchTeamAsync(teamId);
    }

    public async Task FetchTeamAsync(int teamId)
    {
        if (cts.IsCancellationRequested) return;
        if (nextFetchAt.TryGetValue(teamId, out var next) && DateTime.UtcNow < next) return;
        if (!fetching.TryAdd(teamId, true)) return;

        errorByTeam[teamId] = null;
        var succeeded = false;

        try
        {
            var query = new JObject
            {
                ["query"] = @"
                    query($teamId: Int!) {
                        events(game: ""final-fantasy-xiv"", teamId: $teamId, limit: 2, sortBy: STARTS_AT) {
                            id title location description startsAt endsAt attendeeCount tags
                            team { id name iconUrl }
                        }
                        activeEvents(game: ""final-fantasy-xiv"", teamId: $teamId) {
                            id title location description startsAt endsAt attendeeCount tags
                            team { id name iconUrl }
                        }
                    }",
                ["variables"] = new JObject
                {
                    ["teamId"] = teamId,
                },
            };

            using var content = new StringContent(query.ToString(), Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(Endpoint, content, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cts.Token);
                errorByTeam[teamId] = $"HTTP {response.StatusCode}";
                log.Warning($"[Partake] {response.StatusCode}: {body[..Math.Min(body.Length, 300)]}");
                return;
            }

            var root = ParseJson(await response.Content.ReadAsStringAsync(cts.Token));

            if (root["errors"] is JArray errs && errs.Count > 0)
            {
                errorByTeam[teamId] = errs[0]?["message"]?.Value<string>() ?? "GraphQL error";
                log.Warning($"[Partake] GraphQL: {errorByTeam[teamId]}");
                return;
            }

            var events = root["data"]?["events"];
            var active = root["data"]?["activeEvents"];
            log.Information($"[Partake] Team {teamId}: {events?.Count() ?? 0} upcoming, {active?.Count() ?? 0} active");

            var result = new List<VenueEvent>();
            var seenIds = new HashSet<string>();

            var allRaw = new List<JToken>();
            if (active != null) foreach (var a in active) allRaw.Add(a);
            if (events != null) foreach (var e in events) allRaw.Add(e);

            foreach (var e in allRaw)
            {
                var eventId = e["id"]?.ToString() ?? "";
                if (!seenIds.Add(eventId)) continue;

                var start = ParseUtc(e["startsAt"]);
                if (start == null)
                {
                    log.Warning($"[Partake] Cannot parse startsAt: '{e["startsAt"]}'");
                    continue;
                }

                var teamName = e["team"]?["name"]?.Value<string>() ?? "";
                var tId      = e["team"]?["id"]?.Value<int>() ?? 0;

                var iconUrl = e["team"]?["iconUrl"]?.Value<string>();
                if (!string.IsNullOrEmpty(iconUrl))
                    iconUrlByTeam[tId] = iconUrl;

                result.Add(new VenueEvent
                {
                    EventId     = eventId,
                    Title       = e["title"]?.Value<string>() ?? "",
                    VenueName   = teamName,
                    VenueId     = tId.ToString(),
                    Host        = e["location"]?.Value<string>() ?? "",
                    StartTime   = start.Value,
                    EndTime     = ParseUtc(e["endsAt"]) ?? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),
                    Description = e["description"]?.Value<string>() ?? "",
                });
            }

            eventsByTeam[teamId] = result.OrderBy(e => e.StartTime).Take(1).ToList();
            succeeded = true;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            errorByTeam[teamId] = ex.Message;
            log.Error($"[Partake] {ex.Message}");
        }
        finally
        {
            nextFetchAt[teamId] = DateTime.UtcNow + (succeeded ? SuccessTtl : FailureBackoff);
            fetching.TryRemove(teamId, out _);
        }
    }

    public async Task FetchTeamIconAsync(int teamId)
    {
        if (cts.IsCancellationRequested) return;
        if (iconUrlByTeam.ContainsKey(teamId)) return;
        if (nextIconFetchAt.TryGetValue(teamId, out var next) && DateTime.UtcNow < next) return;
        if (!fetchingIcon.TryAdd(teamId, true)) return;

        try
        {
            var query = new JObject
            {
                ["query"] = "query($id: Int!) { team(id: $id) { iconUrl } }",
                ["variables"] = new JObject { ["id"] = teamId },
            };

            using var content = new StringContent(query.ToString(), Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(Endpoint, content, cts.Token);
            if (!response.IsSuccessStatusCode) return;

            var root = ParseJson(await response.Content.ReadAsStringAsync(cts.Token));
            var iconUrl = root["data"]?["team"]?["iconUrl"]?.Value<string>();
            if (!string.IsNullOrEmpty(iconUrl))
                iconUrlByTeam[teamId] = iconUrl;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            log.Debug($"[Partake] Team icon fetch failed: {ex.Message}");
        }
        finally
        {
            nextIconFetchAt[teamId] = DateTime.UtcNow + IconRetryAfter;
            fetchingIcon.TryRemove(teamId, out _);
        }
    }

    public void Invalidate(int teamId)
    {
        nextFetchAt.TryRemove(teamId, out _);
        eventsByTeam.TryRemove(teamId, out _);
    }

    private static JObject ParseJson(string json)
    {
        using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
        return JObject.Load(reader);
    }

    private static DateTime? ParseUtc(JToken? token)
    {
        if (token == null) return null;
        switch (token.Type)
        {
            case JTokenType.Integer:
                return DateTimeOffset.FromUnixTimeSeconds(token.Value<long>()).UtcDateTime;
            case JTokenType.Float:
                return DateTimeOffset.FromUnixTimeMilliseconds((long)token.Value<double>()).UtcDateTime;
            case JTokenType.Date:
                return token.Value<DateTime>().ToUniversalTime();
            case JTokenType.String:
                return DateTimeOffset.TryParse(token.Value<string>(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var dto)
                    ? dto.UtcDateTime
                    : null;
            default:
                return null;
        }
    }

    public void Dispose()
    {
        cts.Cancel();
        http.Dispose();
    }
}
