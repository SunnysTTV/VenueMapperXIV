using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace VenueMapper.Models;

public class Festival
{
    [JsonProperty("festivalId")]
    public string FestivalId { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("organizer")]
    public string Organizer { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("datacenter")]
    public string Datacenter { get; set; } = string.Empty;

    [JsonProperty("server")]
    public string Server { get; set; } = string.Empty;

    [JsonProperty("nsfw")]
    public bool Nsfw { get; set; }

    [JsonProperty("start")]
    public DateTime Start { get; set; }

    [JsonProperty("end")]
    public DateTime End { get; set; }

    [JsonProperty("location")]
    public FestivalLocation Location { get; set; } = new();

    [JsonProperty("colors")]
    public VenueColors? Colors { get; set; }

    [JsonProperty("links")]
    public VenueSocialLinks? Links { get; set; }

    [JsonProperty("services")]
    public List<Service> Services { get; set; } = new();

    [JsonProperty("days")]
    public List<FestivalDay> Days { get; set; } = new();

    [JsonIgnore]
    public bool IsLocalPreview { get; set; }

    [JsonIgnore]
    public bool IsWard => string.Equals(Location.Type, FestivalLocation.WardType, StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public IReadOnlyList<FestivalDay> Schedule => Days.Count > 0 ? Days : [new FestivalDay { Start = Start, End = End }];

    [JsonIgnore]
    public bool MultiDay => Days.Count > 1;

    public FestivalDay? CurrentDay(DateTime utcNow) => Schedule.FirstOrDefault(d => d.Start <= utcNow && utcNow < d.End);

    public FestivalDay? NextDay(DateTime utcNow) => Schedule.Where(d => d.Start > utcNow).OrderBy(d => d.Start).FirstOrDefault();

    public FestivalDay? CurrentOrNextDay(DateTime utcNow) => CurrentDay(utcNow) ?? NextDay(utcNow);

    public int DayNumber(FestivalDay day)
    {
        var schedule = Schedule;
        for (var i = 0; i < schedule.Count; i++)
            if (ReferenceEquals(schedule[i], day) || (schedule[i].Start == day.Start && schedule[i].End == day.End))
                return i + 1;
        return 1;
    }

    public bool IsLive(DateTime utcNow) => CurrentDay(utcNow) != null;

    public bool IsOver(DateTime utcNow) => End <= utcNow;
}

public class FestivalDay
{
    [JsonProperty("start")]
    public DateTime Start { get; set; }

    [JsonProperty("end")]
    public DateTime End { get; set; }
}

public class FestivalHouse
{
    [JsonProperty("plot")]
    public int Plot { get; set; }

    [JsonProperty("label")]
    public string Label { get; set; } = string.Empty;
}

public class FestivalLocation
{
    public const string WardType = "ward";
    public const string WorldType = "world";

    [JsonProperty("type")]
    public string Type { get; set; } = WorldType;

    [JsonProperty("district")]
    public string District { get; set; } = string.Empty;

    [JsonProperty("ward")]
    public int Ward { get; set; }

    [JsonProperty("territoryId")]
    public uint TerritoryId { get; set; }

    [JsonProperty("mapId")]
    public uint MapId { get; set; }

    [JsonProperty("x")]
    public float X { get; set; }

    [JsonProperty("y")]
    public float Y { get; set; }

    [JsonProperty("z")]
    public float Z { get; set; }

    [JsonProperty("radius")]
    public float Radius { get; set; } = 50f;

    [JsonProperty("aetheryteId")]
    public uint AetheryteId { get; set; }

    [JsonProperty("placeName")]
    public string PlaceName { get; set; } = string.Empty;

    [JsonProperty("houses")]
    public List<FestivalHouse> Houses { get; set; } = new();
}
