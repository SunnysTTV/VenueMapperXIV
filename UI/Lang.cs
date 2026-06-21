using System.Globalization;
using System.Resources;

namespace VenueMapper.UI;

public static class Lang
{
    private static readonly ResourceManager Rm =
        new("VenueMapper.Resources.Localization.UIStrings", typeof(Lang).Assembly);

    private static CultureInfo culture = CultureInfo.InvariantCulture;

    public static void Set(string l)
    {
        culture = l?.ToUpperInvariant() == "DE"
            ? new CultureInfo("de")
            : CultureInfo.InvariantCulture;
    }

    private static string G(string key) => Rm.GetString(key, culture) ?? key;

    public static string Map           => G("Map");
    public static string Directory     => G("Directory");
    public static string Events        => G("Events");

    public static string VenueDirectory=> G("VenueDirectory");
    public static string UpcomingEvents=> G("UpcomingEvents");
    public static string Search        => G("Search");
    public static string AllDc         => G("AllDc");
    public static string Visit         => G("Visit");
    public static string Teleporting   => G("Teleporting");
    public static string Copied        => G("Copied");
    public static string AddFavorite   => G("AddFavorite");
    public static string RemoveFavorite=> G("RemoveFavorite");
    public static string CopyAddress   => G("CopyAddress");
    public static string RightClickHint=> G("RightClickHint");

    public static string Active        => G("Active");
    public static string NotInstalled  => G("NotInstalled");
    public static string Here          => G("Here");
    public static string Here2         => G("Here2");
    public static string NoVenues      => G("NoVenues");
    public static string NoEvents      => G("NoEvents");
    public static string EnterVenue    => G("EnterVenue");

    public static string Settings      => G("Settings");
    public static string Language      => G("Language");
    public static string About         => G("About");
    public static string GithubConfig  => G("GithubConfig");
    public static string AutoPull      => G("AutoPull");
    public static string LastUpdated   => G("LastUpdated");
    public static string PullNow       => G("PullNow");
    public static string ResetCache    => G("ResetCache");

    public static string DebugInfo     => G("DebugInfo");
    public static string TerritoryId   => G("TerritoryId");
    public static string MapId         => G("MapId");
    public static string PlayerPos     => G("PlayerPos");
    public static string CurrentFloor  => G("CurrentFloor");

    public static string MarkersOn     => G("MarkersOn");
    public static string MarkersOff    => G("MarkersOff");
    public static string Markers3D     => G("Markers3D");

    public static string MapUnavailable=> G("MapUnavailable");
    public static string MapNotInVenue => G("MapNotInVenue");
    public static string ScrollZoom    => G("ScrollZoom");
    public static string TeleportVia   => G("TeleportVia");

    public static string Links         => G("Links");
    public static string NoLinks       => G("NoLinks");
    public static string SubmitVenue   => G("SubmitVenue");

    public static string Retry         => G("Retry");
    public static string ViewPartake   => G("ViewPartake");
    public static string Loading       => G("Loading");

    public static string AutoPullCfg   => G("AutoPullCfg");
    public static string JoinSupport   => G("JoinSupport");
    public static string CurRelease    => G("CurRelease");
    public static string OlderVer      => G("OlderVer");
    public static string WantVenue     => G("WantVenue");
    public static string PluginDesc    => G("PluginDesc");

    public static string OwnerTitle    => G("OwnerTitle");
    public static string OwnerDesc     => G("OwnerDesc");
    public static string VenueInfo     => G("VenueInfo");
    public static string VenueName     => G("VenueName");
    public static string YourDiscord   => G("YourDiscord");
    public static string Datacenter    => G("Datacenter");
    public static string Server        => G("Server");
    public static string HousingDist   => G("HousingDist");
    public static string Ward          => G("Ward");
    public static string Plot          => G("Plot");
    public static string Description   => G("Description");
    public static string VenueColors   => G("VenueColors");
    public static string SelectHint    => G("SelectHint");
    public static string AddService    => G("AddService");
    public static string UseMyPos      => G("UseMyPos");
    public static string Delete        => G("Delete");
    public static string CoordsTip     => G("CoordsTip");
    public static string Export        => G("Export");
    public static string OptForm       => G("OptForm");
    public static string OptFormDesc   => G("OptFormDesc");
    public static string OpenForm      => G("OpenForm");
    public static string OptDiscord    => G("OptDiscord");
    public static string OptDiscordDesc=> G("OptDiscordDesc");
    public static string CopyJsonDm    => G("CopyJsonDm");
    public static string Preview       => G("Preview");
    public static string FillRequired  => G("FillRequired");
    public static string Floor         => G("Floor");
    public static string Coordinates   => G("Coordinates");
    public static string ServiceType   => G("ServiceType");
    public static string ServiceName   => G("ServiceName");

    public static string SetupWelcomeTitle => G("SetupWelcomeTitle");
    public static string SetupWelcomeDesc  => G("SetupWelcomeDesc");
    public static string SetupWhatYouGet   => G("SetupWhatYouGet");
    public static string SetupFeature1     => G("SetupFeature1");
    public static string SetupFeature2     => G("SetupFeature2");
    public static string SetupFeature3     => G("SetupFeature3");
    public static string SetupFeature4     => G("SetupFeature4");
    public static string SetupFeature5     => G("SetupFeature5");
    public static string SetupFeature6     => G("SetupFeature6");
    public static string SetupChooseLang   => G("SetupChooseLang");
    public static string SetupLangHintEn   => G("SetupLangHintEn");
    public static string SetupLangHintDe   => G("SetupLangHintDe");
    public static string SetupKeyFeatures  => G("SetupKeyFeatures");
    public static string SetupFeatMap      => G("SetupFeatMap");
    public static string SetupFeatMapDesc  => G("SetupFeatMapDesc");
    public static string SetupFeatDir      => G("SetupFeatDir");
    public static string SetupFeatDirDesc  => G("SetupFeatDirDesc");
    public static string SetupFeatEvents   => G("SetupFeatEvents");
    public static string SetupFeatEventsDesc => G("SetupFeatEventsDesc");
    public static string SetupFeat3D       => G("SetupFeat3D");
    public static string SetupFeat3DDesc   => G("SetupFeat3DDesc");
    public static string SetupFeatOwner    => G("SetupFeatOwner");
    public static string SetupFeatOwnerDesc=> G("SetupFeatOwnerDesc");
    public static string SetupFeatUpdate   => G("SetupFeatUpdate");
    public static string SetupFeatUpdateDesc => G("SetupFeatUpdateDesc");
    public static string SetupQuickSettings=> G("SetupQuickSettings");
    public static string SetupEnable3D     => G("SetupEnable3D");
    public static string SetupEnable3DDesc => G("SetupEnable3DDesc");
    public static string SetupAllSet       => G("SetupAllSet");
    public static string SetupCommand      => G("SetupCommand");
    public static string SetupSkip         => G("SetupSkip");
    public static string SetupBack         => G("SetupBack");
    public static string SetupNext         => G("SetupNext");
    public static string SetupDone         => G("SetupDone");

    public static string Location(int count)
        => string.Format(G(count != 1 ? "LocationMany" : "LocationOne"), count);
}
