using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VenueMapper.Models;

namespace VenueMapper.Services;

public class PartakeApiService : IDisposable
{
    private const string Endpoint = "https://api.partake.gg/";
    private const int CacheTtlMinutes = 15;

    private readonly HttpClient http;
    private readonly IPluginLog log;

    private readonly Dictionary<int, List<VenueEvent>> eventsByTeam = new();
    private readonly Dictionary<int, DateTime> fetchTimes = new();
    private readonly Dictionary<int, string?> errorByTeam = new();
    private readonly HashSet<int> fetching = new();

    public bool IsLoading(int teamId) => fetching.Contains(teamId);
    public string? GetError(int teamId) => errorByTeam.GetValueOrDefault(teamId);

    public PartakeApiService(IPluginLog log)
    {
        this.log = log;
        http = new HttpClient();
        http.DefaultRequestHeaders.Add("User-Agent", "VenueMapper-Dalamud/0.5.0");
    }

    public List<VenueEvent> GetEvents(int teamId)
        => eventsByTeam.GetValueOrDefault(teamId) ?? [];

    public async Task FetchTeamAsync(int teamId)
    {
        if (fetching.Contains(teamId)) return;
        if (fetchTimes.TryGetValue(teamId, out var t) && (DateTime.Now - t).TotalMinutes < CacheTtlMinutes)
            return;

        fetching.Add(teamId);
        errorByTeam[teamId] = null;

        try
        {
            var query = new JObject
            {
                ["query"] = @"
                    query($teamId: Int!) {
                        events(game: ""final-fantasy-xiv"", teamId: $teamId, limit: 10, sortBy: STARTS_AT) {
                            id title location description startsAt endsAt attendeeCount tags
                            team { id name }
                        }
                        activeEvents(game: ""final-fantasy-xiv"", teamId: $teamId) {
                            id title location description startsAt endsAt attendeeCount tags
                            team { id name }
                        }
                    }",
                ["variables"] = new JObject
                {
                    ["teamId"] = teamId,
                },
            };

            var content = new StringContent(query.ToString(), Encoding.UTF8, "application/json");
            var response = await http.PostAsync(Endpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                errorByTeam[teamId] = $"HTTP {response.StatusCode}";
                log.Warning($"[Partake] {response.StatusCode}: {body[..Math.Min(body.Length, 300)]}");
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var root = JObject.Parse(json);

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
            var teams = new HashSet<string>();

            var allRaw = new List<JToken>();
            if (active != null) foreach (var a in active) allRaw.Add(a);
            if (events != null) foreach (var e in events) allRaw.Add(e);

            foreach (var e in allRaw)
                {
                    var eventId = e["id"]?.Value<string>() ?? "";
                    if (!seenIds.Add(eventId)) continue;
                    var startsRaw = e["startsAt"]?.ToString() ?? "";
                    var endsRaw   = e["endsAt"]?.ToString() ?? "";
                    log.Debug($"[Partake] Event raw: title='{e["title"]}' startsAt='{startsRaw}' type={e["startsAt"]?.Type}");

                    DateTime start;
                    if (e["startsAt"]?.Type == JTokenType.Integer)
                        start = DateTimeOffset.FromUnixTimeSeconds(e["startsAt"]!.Value<long>()).UtcDateTime;
                    else if (e["startsAt"]?.Type == JTokenType.Float)
                        start = DateTimeOffset.FromUnixTimeMilliseconds((long)(e["startsAt"]!.Value<double>())).UtcDateTime;
                    else if (!DateTime.TryParse(startsRaw, null, System.Globalization.DateTimeStyles.None, out start))
                        { log.Warning($"[Partake] Cannot parse startsAt: '{startsRaw}'"); continue; }

                    DateTime.TryParse(endsRaw, out var end);

                    var teamName = e["team"]?["name"]?.Value<string>() ?? "";
                    var tId      = e["team"]?["id"]?.Value<int>() ?? 0;
                    teams.Add($"{teamName} (id={tId})");

                    result.Add(new VenueEvent
                    {
                        EventId     = e["id"]?.Value<string>() ?? "",
                        Title       = e["title"]?.Value<string>() ?? "",
                        VenueName   = teamName,
                        VenueId     = tId.ToString(),
                        Host        = e["location"]?.Value<string>() ?? "",
                        StartTime   = start,
                        EndTime     = end,
                        EventType   = ParseTags(e["tags"]),
                        Description = e["description"]?.Value<string>() ?? "",
                    });
                }

            log.Information($"[Partake] Fetched {result.Count} events. Teams: {string.Join(", ", teams.Take(10))}");

            eventsByTeam[teamId] = result.OrderBy(e => e.StartTime).Take(5).ToList();
            fetchTimes[teamId] = DateTime.Now;
        }
        catch (Exception ex)
        {
            errorByTeam[teamId] = ex.Message;
            log.Error($"[Partake] {ex.Message}");
        }
        finally
        {
            fetching.Remove(teamId);
        }
    }

    public void Invalidate(int teamId) { fetchTimes.Remove(teamId); eventsByTeam.Remove(teamId); }

    private static string ParseTags(JToken? tags) => "EVENT";

    public void Dispose() => http.Dispose();
}
