using System;
using Newtonsoft.Json;

namespace VenueMapper.Models;

public class VenueEvent
{
    [JsonProperty("eventId")]
    public string EventId { get; set; } = string.Empty;

    [JsonProperty("venueId")]
    public string VenueId { get; set; } = string.Empty;

    [JsonProperty("venueName")]
    public string VenueName { get; set; } = string.Empty;

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("startTime")]
    public DateTime StartTime { get; set; }

    [JsonProperty("endTime")]
    public DateTime EndTime { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("eventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonProperty("host")]
    public string Host { get; set; } = string.Empty;

    [JsonProperty("link")]
    public string Link { get; set; } = string.Empty;
}
