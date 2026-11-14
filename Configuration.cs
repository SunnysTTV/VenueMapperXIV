using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using VenueMapper.Services;

namespace VenueMapper;

public enum VenueSignsMode { All, Open, Favorites, Off }

public enum GameMapMode { Off, Native, Overlay, Both }

public enum HouseRulesMode { EveryVisit, FirstVisit, Off }

public enum GuideAnimation { Arrow, Sparkles, Pulse, Dots }

public enum GuideColorMode { Single, Venue }

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

    public bool PassportEnabled { get; set; } = true;

    public HouseRulesMode HouseRules { get; set; } = HouseRulesMode.FirstVisit;
    public HashSet<string> RulesSeen { get; set; } = new();
    public HashSet<string> RulesMuted { get; set; } = new();

    public bool ChatLinks { get; set; } = true;
    public bool PartyFinderAds { get; set; } = true;
    public bool PlacardNames { get; set; } = true;
    public ChatLinkChannels ChatLinkChannels { get; set; } = ChatLinkChannels.All;
    public Dictionary<string, PassportBook> Passports { get; set; } = new();

    public ToastCorner ToastPosition { get; set; } = ToastCorner.TopRight;
    public bool NotificationsEnabled { get; set; } = true;
    public int MaxVisibleToasts { get; set; } = 4;
    public bool SuppressInCombat { get; set; } = true;
    public float ToastDurationMultiplier { get; set; } = 1.0f;

    public string LastSeenVersion { get; set; } = "";

    public bool EventReminders { get; set; } = false;
    public bool EventRemindersFavoritesOnly { get; set; }
    public int EventReminderMinutes { get; set; } = 15;
    public bool FavoriteOpenNotify { get; set; } = true;
    public bool NotifyBanners { get; set; } = true;

    public string Language { get; set; } = "EN";

    public bool HasSeenSetup { get; set; }
    public bool PendingForcedSetup { get; set; }
    public bool PendingWhatsNew { get; set; }

    public System.Numerics.Vector2? WindowPosition { get; set; }
    public System.Numerics.Vector2? WindowSize { get; set; }

    public string DefaultTab { get; set; } = "remember";
    public string? LastActiveTab { get; set; }

    public bool BoostOpenVenues { get; set; } = true;
    public bool Markers3DEnabled { get; set; } = true;
    public MarkerNamesMode MarkerNames { get; set; } = MarkerNamesMode.Near;
    public bool MarkerThroughWalls { get; set; } = true;
    public bool MarkerVenueColors { get; set; } = false;
    public VenueSignsMode VenueSigns { get; set; } = VenueSignsMode.All;
    public int SettingsRevision { get; set; }
    public float WardMapZoom { get; set; } = 2.5f;
    public GuideAnimation GuideAnimation { get; set; } = GuideAnimation.Arrow;
    public GuideColorMode GuideColor { get; set; } = GuideColorMode.Venue;
    public System.Numerics.Vector3 GuideSingleColor { get; set; } = new(0.337f, 0.839f, 0.933f);
    public bool MarkersBehindUi { get; set; } = true;

    public const float WholeWardRange = 5000f;
    public float VenueSignRange { get; set; } = 100f;
    public GameMapMode GameMapNames { get; set; } = GameMapMode.Off;
    public bool HideMarkersInOwnVenue { get; set; } = false;
    public bool AutoOpenOnVenueEnter { get; set; } = true;
    public bool AutoOpenOwnVenue { get; set; } = true;
    public bool ShowQuickPopupOnEnter { get; set; } = false;

    public float MarkerSizeScale { get; set; } = 1.0f;
    public bool MarkerColorOverrideEnabled { get; set; } = false;
    public System.Numerics.Vector3 MarkerOverrideColor { get; set; } = new(1f, 0f, 1f);
    public bool MarkerStrongPulse { get; set; } = false;

    public void Save()
    {
        VenueMapperPlugin.PluginInterface.SavePluginConfig(this);
    }
}
