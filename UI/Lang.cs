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
    public static string HideVenue     => G("HideVenue");
    public static string UnhideVenue   => G("UnhideVenue");
    public static string ShowHidden    => G("ShowHidden");
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
    public static string LangEnglish   => G("LangEnglish");
    public static string LangGerman    => G("LangGerman");
    public static string About         => G("About");
    public static string GithubConfig  => G("GithubConfig");
    public static string AutoPull      => G("AutoPull");
    public static string LastUpdated   => G("LastUpdated");
    public static string PullNow       => G("PullNow");
    public static string ResetCache    => G("ResetCache");
    public static string Notifications      => G("Notifications");
    public static string TestNotification   => G("TestNotification");
    public static string TestNotificationText => G("TestNotificationText");
    public static string NotificationPosition => G("NotificationPosition");
    public static string PosTopRight        => G("PosTopRight");
    public static string PosTopLeft         => G("PosTopLeft");
    public static string PosBottomRight     => G("PosBottomRight");
    public static string PosBottomLeft      => G("PosBottomLeft");
    public static string EventReminders       => G("EventReminders");
    public static string EventRemindersFavOnly=> G("EventRemindersFavOnly");
    public static string EventReminderMinutesLabel => G("EventReminderMinutesLabel");
    public static string EnableNotifications  => G("EnableNotifications");
    public static string MiscSettings         => G("MiscSettings");
    public static string ResetWindowPosition    => G("ResetWindowPosition");
    public static string ResetWindowPositionTip => G("ResetWindowPositionTip");
    public static string UnhideAllVenues        => G("UnhideAllVenues");
    public static string UnhideAllVenuesTip(int count) => string.Format(G("UnhideAllVenuesTip"), count);
    public static string ToastWindowReset       => G("ToastWindowReset");
    public static string ToastAllVenuesUnhidden(int count) => string.Format(G("ToastAllVenuesUnhidden"), count);
    public static string MaxVisibleToasts     => G("MaxVisibleToasts");
    public static string ToastDurationLabel   => G("ToastDurationLabel");
    public static string SuppressInCombat     => G("SuppressInCombat");
    public static string DefaultTabLabel      => G("DefaultTabLabel");
    public static string DefaultTabRemember   => G("DefaultTabRemember");
    public static string BoostOpenVenues      => G("BoostOpenVenues");
    public static string BoostOpenVenuesTip   => G("BoostOpenVenuesTip");

    public static string ToastCacheCleared      => G("ToastCacheCleared");
    public static string ToastCacheClearFailed  => G("ToastCacheClearFailed");
    public static string ToastConfigUpdated     => G("ToastConfigUpdated");
    public static string ToastConfigUpToDate    => G("ToastConfigUpToDate");
    public static string ToastConfigPullFailed  => G("ToastConfigPullFailed");
    public static string ToastLifestreamMissing => G("ToastLifestreamMissing");
    public static string ToastTeleportingTo(string name) => string.Format(G("ToastTeleportingTo"), name);
    public static string ToastTeleportFailed    => G("ToastTeleportFailed");
    public static string ToastFavoriteAdded(string name)   => string.Format(G("ToastFavoriteAdded"), name);
    public static string ToastFavoriteRemoved(string name) => string.Format(G("ToastFavoriteRemoved"), name);
    public static string ToastAddressCopied     => G("ToastAddressCopied");
    public static string ToastVenueHidden(string name)   => string.Format(G("ToastVenueHidden"), name);
    public static string ToastVenueUnhidden(string name) => string.Format(G("ToastVenueUnhidden"), name);
    public static string ToastJsonCopied           => G("ToastJsonCopied");
    public static string ToastRequiredFieldsMissing=> G("ToastRequiredFieldsMissing");
    public static string ToastWelcomeFirstLoad(int count) => string.Format(G("ToastWelcomeFirstLoad"), count);
    public static string ToastUpdated(string version)     => string.Format(G("ToastUpdated"), version);
    public static string ToastSetupComplete     => G("ToastSetupComplete");
    public static string ToastAllEggsFound      => G("ToastAllEggsFound");
    public static string ToastWelcomeToVenue(string name) => string.Format(G("ToastWelcomeToVenue"), name);
    public static string ToastVenueClosed(string name)    => string.Format(G("ToastVenueClosed"), name);
    public static string ToastEventSoon(string title, string venueName, int minutes) => string.Format(G("ToastEventSoon"), title, venueName, minutes);

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
    public static string GotIdeas      => G("GotIdeas");
    public static string CurRelease    => G("CurRelease");
    public static string OlderVer      => G("OlderVer");
    public static string WantVenue     => G("WantVenue");
    public static string PluginDesc    => G("PluginDesc");

    public static string OwnerTitle    => G("OwnerTitle");
    public static string OwnerDesc     => G("OwnerDesc");
    public static string UpdateVenueTitle => G("UpdateVenueTitle");
    public static string UpdateVenueDesc  => G("UpdateVenueDesc");
    public static string VenueInfo     => G("VenueInfo");
    public static string VenueName     => G("VenueName");
    public static string YourDiscord   => G("YourDiscord");
    public static string YourDiscordOptional => G("YourDiscordOptional");
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
    public static string OptFormDescVerified => G("OptFormDescVerified");
    public static string OpenForm      => G("OpenForm");
    public static string OptDiscord    => G("OptDiscord");
    public static string OptDiscordDesc=> G("OptDiscordDesc");
    public static string SendUpdate     => G("SendUpdate");
    public static string SendUpdateDesc => G("SendUpdateDesc");
    public static string CopyJsonDm    => G("CopyJsonDm");
    public static string Preview       => G("Preview");
    public static string FillRequired  => G("FillRequired");
    public static string Floor         => G("Floor");
    public static string Coordinates   => G("Coordinates");
    public static string ServiceType   => G("ServiceType");
    public static string ServiceName   => G("ServiceName");
    public static string DetectPosition=> G("DetectPosition");
    public static string ToastVenueLoaded(string name) => string.Format(G("ToastVenueLoaded"), name);
    public static string UpdateVenue         => G("UpdateVenue");
    public static string UpdateVenueTip      => G("UpdateVenueTip");
    public static string OwnerVerifyTitle    => G("OwnerVerifyTitle");
    public static string OwnerVerifyScanning => G("OwnerVerifyScanning");
    public static string OwnerVerifyGranted  => G("OwnerVerifyGranted");
    public static string OwnerVerifyDenied   => G("OwnerVerifyDenied");
    public static string OwnerVerifyDeniedHint => G("OwnerVerifyDeniedHint");
    public static string VenueNameHint => G("VenueNameHint");
    public static string DiscordHint   => G("DiscordHint");
    public static string HouseSize     => G("HouseSize");
    public static string NsfwVenue     => G("NsfwVenue");
    public static string NsfwVenueTip  => G("NsfwVenueTip");
    public static string NsfwUncheckedHint => G("NsfwUncheckedHint");
    public static string RegisterOwnerId    => G("RegisterOwnerId");
    public static string RegisterOwnerIdTip => G("RegisterOwnerIdTip");
    public static string NsfwBadge     => G("NsfwBadge");
    public static string SfwBadge      => G("SfwBadge");
    public static string ColorPrimary  => G("ColorPrimary");
    public static string ColorAccent   => G("ColorAccent");
    public static string ColorSecondary=> G("ColorSecondary");
    public static string ServiceNameHint => G("ServiceNameHint");
    public static string CopyJson      => G("CopyJson");
    public static string FormOpened    => G("FormOpened");
    public static string CurrentTag    => G("CurrentTag");
    public static string NoChangelog   => G("NoChangelog");
    public static string AllServers    => G("AllServers");

    public static string FloorGround        => G("FloorGround");
    public static string FloorSecond        => G("FloorSecond");
    public static string FloorCellar        => G("FloorCellar");

    public static string SvcEntrance        => G("SvcEntrance");
    public static string SvcBar             => G("SvcBar");
    public static string SvcStage           => G("SvcStage");
    public static string SvcGambling        => G("SvcGambling");
    public static string SvcDjBooth         => G("SvcDjBooth");
    public static string SvcVip             => G("SvcVip");
    public static string SvcBath            => G("SvcBath");
    public static string SvcSpa             => G("SvcSpa");
    public static string SvcEvent           => G("SvcEvent");
    public static string SvcUpstairs        => G("SvcUpstairs");
    public static string SvcDownstairs      => G("SvcDownstairs");

    public static string StatusOpenNow      => G("StatusOpenNow");
    public static string StatusOpensInMin(int n) => string.Format(G("StatusOpensInMin"), n);
    public static string StatusOpensInHours(int n) => string.Format(G("StatusOpensInHours"), n);
    public static string StatusOpensInDays(int n)  => string.Format(G("StatusOpensInDays"), n);

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
    public static string SetupForcedBannerTitle => G("SetupForcedBannerTitle");
    public static string SetupForcedBannerDesc  => G("SetupForcedBannerDesc");
    public static string SetupOwnerIdNoteTitle => G("SetupOwnerIdNoteTitle");
    public static string SetupOwnerIdNoteDesc  => G("SetupOwnerIdNoteDesc");
    public static string SetupFeatMap      => G("SetupFeatMap");
    public static string SetupFeatMapDesc  => G("SetupFeatMapDesc");
    public static string SetupFeatDir      => G("SetupFeatDir");
    public static string SetupFeatDirDesc  => G("SetupFeatDirDesc");
    public static string SetupFeatEvents   => G("SetupFeatEvents");
    public static string SetupFeatEventsDesc => G("SetupFeatEventsDesc");
    public static string SetupFeat3D       => G("SetupFeat3D");
    public static string SetupFeat3DDesc   => G("SetupFeat3DDesc");
    public static string SetupFeatNotify    => G("SetupFeatNotify");
    public static string SetupFeatNotifyDesc=> G("SetupFeatNotifyDesc");
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

    public static string EasterEggs         => G("EasterEggs");
    public static string EggDiscoveredOn(string date) => string.Format(G("EggDiscoveredOn"), date);
    public static string EggUnlockedToast(string name) => string.Format(G("EggUnlockedToast"), name);

    public static string EggRgbName    => G("EggRgbName");
    public static string EggRgbHint    => G("EggRgbHint");
    public static string EggHackerName => G("EggHackerName");
    public static string EggHackerHint => G("EggHackerHint");
    public static string EggSunnyName  => G("EggSunnyName");
    public static string EggSunnyHint  => G("EggSunnyHint");
    public static string EggWobbleName => G("EggWobbleName");
    public static string EggWobbleHint => G("EggWobbleHint");
    public static string EggTitleName  => G("EggTitleName");
    public static string EggTitleHint  => G("EggTitleHint");

    public static string Location(int count)
        => string.Format(G(count != 1 ? "LocationMany" : "LocationOne"), count);
}
