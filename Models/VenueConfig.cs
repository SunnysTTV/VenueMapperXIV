using System.Collections.Generic;
using Newtonsoft.Json;

namespace VenueMapper.Models;

public class VenueConfig
{
    [JsonProperty("venues")]
    public List<Venue> Venues { get; set; } = new();
}
