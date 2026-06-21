using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace VenueMapper;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool AutoPullOnStartup { get; set; } = true;

    public string GitHubConfigUrl { get; set; } = "https://raw.githubusercontent.com/SunnysTTV/VenueMapperXIV/main/Resources/venues.json";

    public Dictionary<string, bool> ServiceFilters { get; set; } = new();

    public HashSet<string> FavoriteVenueIds { get; set; } = new();

    public string Language { get; set; } = "EN";

    public bool HasSeenSetup { get; set; }

    public System.Numerics.Vector2? WindowPosition { get; set; }
    public System.Numerics.Vector2? WindowSize { get; set; }

    public void Save()
    {
        VenueMapperPlugin.PluginInterface.SavePluginConfig(this);
    }
}
