using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using VenueMapper.Services;

namespace VenueMapper;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool AutoPullOnStartup { get; set; } = true;

    public string GitHubConfigUrl { get; set; } = "https://raw.githubusercontent.com/SunnysTTV/VenueMapperXIV/main/Resources/venues.json";

    public Dictionary<string, bool> ServiceFilters { get; set; } = new();

    public HashSet<string> FavoriteVenueIds { get; set; } = new();

    public HashSet<string> HiddenVenueIds { get; set; } = new();

    public Dictionary<string, EasterEggState> EasterEggs { get; set; } = new();

    public ToastCorner ToastPosition { get; set; } = ToastCorner.TopRight;
    public bool NotificationsEnabled { get; set; } = true;
    public int MaxVisibleToasts { get; set; } = 4;
    public bool SuppressInCombat { get; set; } = true;
    public float ToastDurationMultiplier { get; set; } = 1.0f;

    public string LastSeenVersion { get; set; } = "";

    public bool EventReminders { get; set; } = false;
    public bool EventRemindersFavoritesOnly { get; set; }
    public int EventReminderMinutes { get; set; } = 15;

    public string Language { get; set; } = "EN";

    public bool HasSeenSetup { get; set; }
    public bool PendingForcedSetup { get; set; }

    public System.Numerics.Vector2? WindowPosition { get; set; }
    public System.Numerics.Vector2? WindowSize { get; set; }

    public string DefaultTab { get; set; } = "remember";
    public string? LastActiveTab { get; set; }

    public bool BoostOpenVenues { get; set; } = true;

    public void Save()
    {
        VenueMapperPlugin.PluginInterface.SavePluginConfig(this);
    }
}
