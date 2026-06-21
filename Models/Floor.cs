using System.Collections.Generic;
using Newtonsoft.Json;

namespace VenueMapper.Models;

public class Floor
{
    [JsonProperty("floor")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("yMin")]
    public float YMin { get; set; }

    [JsonProperty("yMax")]
    public float YMax { get; set; }

    [JsonProperty("mapId")]
    public uint MapId { get; set; }

    [JsonProperty("services")]
    public List<Service> Services { get; set; } = new();
}
