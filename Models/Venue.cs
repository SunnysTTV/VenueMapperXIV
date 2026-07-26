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

    [JsonProperty("houseSize")]
    public string HouseSize { get; set; } = "L";

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

    [JsonProperty("ownerIdHashes")]
    public List<string> OwnerIdHashes { get; set; } = new();

    [JsonProperty("nsfw")]
    public bool Nsfw { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;
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
        if (hex.Length < 6
            || !int.TryParse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var ri)
            || !int.TryParse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var gi)
            || !int.TryParse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bi))
        {
            return new Vector4(0f, 0.94f, 1f, 1f);
        }
        return new Vector4(ri / 255f, gi / 255f, bi / 255f, 1f);
    }
}
