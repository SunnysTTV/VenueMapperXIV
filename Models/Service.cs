using Newtonsoft.Json;

namespace VenueMapper.Models;

public class Service
{
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("label")]
    public string Label { get; set; } = string.Empty;

    [JsonProperty("icon")]
    public string Icon { get; set; } = string.Empty;

    [JsonProperty("x")]
    public float X { get; set; }

    [JsonProperty("y")]
    public float Y { get; set; }

    [JsonProperty("z")]
    public float Z { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;
}
