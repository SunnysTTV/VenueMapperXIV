using System;
using System.Collections.Generic;
using System.Linq;

namespace VenueMapper.Models;

public static class FloorConfig
{
    public class FloorDefinition
    {
        public string Floor { get; init; } = "";
        public float YMin { get; init; }
        public float YMax { get; init; }
        public string Name { get; init; } = "";
    }

    public static readonly List<FloorDefinition> Floors =
    [
        new() { Floor = "cellar", YMin = -10.0f, YMax = -3.5f, Name = "Cellar" },
        new() { Floor = "ground",    YMin = -3.5f,  YMax = 6.5f,  Name = "Ground Floor" },
        new() { Floor = "second",    YMin = 6.5f,   YMax = 14.0f, Name = "Second Floor" },
    ];

    public static FloorDefinition? DetectFloor(float y)
        => Floors.FirstOrDefault(f => y >= f.YMin && y < f.YMax);

    public static FloorDefinition? GetFloor(string name)
        => Floors.FirstOrDefault(f => f.Floor.Equals(name, StringComparison.OrdinalIgnoreCase));
}
