using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Newtonsoft.Json;

namespace VenueMapper.Models;

public class Venue
{
    [JsonProperty("venueId")]
    public string VenueId { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("address")]
    public string Address { get; set; } = string.Empty;

    [JsonProperty("datacenter")]
    public string Datacenter { get; set; } = string.Empty;

    [JsonProperty("territoryIds")]
    public List<uint> TerritoryIds { get; set; } = new();

    [JsonProperty("ward")]
    public int Ward { get; set; }

    [JsonProperty("plot")]
    public int Plot { get; set; }

    [JsonProperty("teamId")]
    public int TeamId { get; set; }

    [JsonProperty("availableFloors")]
    public List<string> AvailableFloors { get; set; } = ["ground", "second", "cellar"];

    [JsonProperty("colors")]
    public VenueColors? Colors { get; set; }

    [JsonProperty("links")]
    public VenueSocialLinks? Links { get; set; }

    [JsonProperty("floors")]
    public List<Floor> Floors { get; set; } = new();
}

public class VenueSocialLinks
{
    [JsonProperty("discord")]
    public string Discord { get; set; } = string.Empty;

    [JsonProperty("partake")]
    public string Partake { get; set; } = string.Empty;

    [JsonProperty("website")]
    public string Website { get; set; } = string.Empty;

    [JsonProperty("ffxivvenues")]
    public string FfxivVenues { get; set; } = string.Empty;

    public bool HasAny => !string.IsNullOrEmpty(Discord) || !string.IsNullOrEmpty(Partake) ||
                          !string.IsNullOrEmpty(FfxivVenues) || !string.IsNullOrEmpty(Website);
}

public class VenueColors
{
    [JsonProperty("primary")]
    public string Primary { get; set; } = "#00f0ff";

    [JsonProperty("accent")]
    public string Accent { get; set; } = "#9d4edd";

    [JsonProperty("secondary")]
    public string Secondary { get; set; } = "#ff00aa";

    public Vector4 PrimaryVec => HexToVec4(Primary);
    public Vector4 AccentVec  => HexToVec4(Accent);
    public Vector4 SecondaryVec => HexToVec4(Secondary);

    private static Vector4 HexToVec4(string hex)
    {
        hex = (hex ?? "#00f0ff").TrimStart('#');
        if (hex.Length < 6) hex = "00f0ff";
        var r = int.Parse(hex[..2], NumberStyles.HexNumber) / 255f;
        var g = int.Parse(hex[2..4], NumberStyles.HexNumber) / 255f;
        var b = int.Parse(hex[4..6], NumberStyles.HexNumber) / 255f;
        return new Vector4(r, g, b, 1f);
    }
}
