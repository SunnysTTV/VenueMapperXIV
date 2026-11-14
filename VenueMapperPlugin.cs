using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Textures;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using VenueMapper.Models;
using VenueMapper.Services;
using VenueMapper.UI;

namespace VenueMapper;

public sealed class VenueMapperPlugin : IDalamudPlugin
{
    public string Name => "VenueMapper";

    private const string CommandName = "/vmapper";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IReliableFileStorage FileStorage { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static INamePlateGui NamePlateGui { get; private set; } = null!;
    [PluginService] internal static IPartyFinderGui PartyFinderGui { get; private set; } = null!;

    public Configuration Configuration { get; }
    public ConfigManager ConfigManager { get; }
    public GitHubConfigPuller GitHubPuller { get; }
    public HousingData HousingData { get; }
    public PlayerPositionTracker PositionTracker { get; }
    public HousingMapLoader MapLoader { get; }
    public LifestreamService Lifestream { get; }
    public PictomancyMarkerManager PictomancyMarkers { get; }
    public GameMapMarkers GameMapMarkers { get; }
    public Dalamud.Interface.ManagedFontAtlas.IFontHandle MarkerTextFont { get; }
    public Dalamud.Interface.ManagedFontAtlas.IFontHandle MarkerIconFont { get; }
    public Dalamud.Interface.ManagedFontAtlas.IFontHandle BannerFont { get; }
    public PartakeApiService PartakeApi { get; }
    public XivVenuesService XivVenues { get; }
    public VenueLogoService VenueLogos { get; }
    public EasterEggManager EasterEggManager { get; }
    public PassportManager Passport { get; }
    public ChatVenueLinks ChatLinks { get; }
    public IndoorGuide Guide { get; }
    public PlacardNames Placards { get; }
    public PartyFinderAds PartyFinderAds { get; }

#if DEBUG
    public ScreenshotAssistant Shots { get; }
#endif

    public PassportBook? DemoPassport { get; set; }

    private bool screenshotMode;

    public bool ScreenshotMode
    {
        get => screenshotMode;
        set
        {
            screenshotMode = value;
            PluginInterface.UiBuilder.DisableUserUiHide = value;
            PluginInterface.UiBuilder.DisableGposeUiHide = value;
            Toasts.BannersMuted = value;
        }
    }
    public HouseRulesCard HouseRules { get; }
    public KonamiDetector KonamiDetector { get; }
    public ToastManager Toasts { get; }

    public VenueMapWindow VenueMapWindow { get; }
    public SettingsWindow SettingsWindow { get; }
    public OwnerSubmitWindow OwnerSubmitWindow { get; }
    public OwnerSubmitWindow OwnerUpdateWindow { get; }
    public OwnerVerifyWindow OwnerVerifyWindow { get; }
    public SetupWindow SetupWindow { get; }
    public DebugWindow DebugWindow { get; }
    public VenueDetailWindow VenueDetailWindow { get; }
    public VenueQuickPopupWindow VenueQuickPopupWindow { get; }
    public NotificationHistoryWindow NotificationHistoryWindow { get; }
    public UpdateNotesWindow UpdateNotesWindow { get; }
    public FestivalSubmitWindow FestivalSubmitWindow { get; }
    public Dalamud.Interface.Windowing.WindowSystem WindowSystem { get; } = new("VenueMapper");

    public VenueMapperPlugin()
    {
        var existingConfig = PluginInterface.GetPluginConfig() as Configuration;
        var isBrandNewInstall = existingConfig == null;
        Configuration = existingConfig ?? new Configuration();
        if (Configuration.MarkerSizeScale > 1.45f)
        {
            Configuration.MarkerSizeScale = Configuration.MarkerSizeScale >= 1.9f ? 1.4f : 1.2f;
            Configuration.Save();
        }
        if (Configuration.SettingsRevision < 1)
        {
            Configuration.SettingsRevision = 1;
            Configuration.WardMapZoom = 2.5f;
            Configuration.Save();
        }
        if (Configuration.SettingsRevision < 2)
        {
            Configuration.SettingsRevision = 2;
            Configuration.Save();
        }
        if (string.IsNullOrWhiteSpace(Configuration.GitHubConfigUrl))
        {
            Configuration.GitHubConfigUrl = "https://raw.githubusercontent.com/SunnysTTV/VenueMapperXIV/main/Resources/venues.json";
            Configuration.Save();
        }
        UI.Lang.Set(Configuration.Language);
        UI.ChangelogData.CurrentLanguage = Configuration.Language;

        ConfigManager = new ConfigManager(Log, PluginInterface.ConfigDirectory.FullName);
        GitHubPuller = new GitHubConfigPuller(Log, ConfigManager);
        HousingData = new HousingData(DataManager, Log);
        PositionTracker = new PlayerPositionTracker(ClientState, ObjectTable, PlayerState, HousingData, Log);
        MapLoader   = new HousingMapLoader(DataManager, TextureProvider, Log);
        Lifestream  = new LifestreamService(PluginInterface, Log);
        Lifestream.HouseEnterFailed += OnHouseEnterFailed;
        MarkerTextFont = PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e =>
            e.OnPreBuild(tk => tk.AddDalamudDefaultFont(MarkerTextureCache.FontPx, MarkerTextureCache.GlyphRanges)));
        MarkerIconFont = PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e =>
            e.OnPreBuild(tk => tk.AddFontAwesomeIconFont(new Dalamud.Interface.ManagedFontAtlas.SafeFontConfig { SizePx = MarkerTextureCache.FontPx })));
        PictomancyMarkers = new PictomancyMarkerManager(PluginInterface, TextureProvider, Log, MarkerTextFont, MarkerIconFont);
        PictomancyMarkers.Enabled = Configuration.Markers3DEnabled;
        PartakeApi   = new PartakeApiService(Log);
        XivVenues    = new XivVenuesService(Log, PluginInterface.ConfigDirectory.FullName);

        XivVenues.RequestSchedule();
        VenueLogos   = new VenueLogoService(TextureProvider, Log);
        EasterEggManager = new EasterEggManager(Configuration, Log);
        Passport = new PassportManager(Configuration);
        ChatLinks = new ChatVenueLinks(this, ChatGui, DataManager, Log);
        Guide = new IndoorGuide(this);
        Placards = new PlacardNames(this, NamePlateGui, Framework, DataManager, Log);
        PartyFinderAds = new PartyFinderAds(this, PartyFinderGui, Log);
#if DEBUG
        Shots = new ScreenshotAssistant(this);
#endif
        HouseRules = new HouseRulesCard(this);
        KonamiDetector =new KonamiDetector(KeyState);
        KonamiDetector.OnCompleted += OnKonamiCompleted;
        Toasts = new ToastManager();
        EasterEggManager.OnUnlocked += OnEasterEggUnlocked;
        EasterEggManager.OnAllUnlocked += OnAllEasterEggsUnlocked;

        var bundledResourcePath = Path.Combine(
            Path.GetDirectoryName(PluginInterface.AssemblyLocation.FullName) ?? string.Empty,
            "Resources", "venues.json");

        ConfigManager.Load(bundledResourcePath);

        if (isBrandNewInstall)
        {
            pendingWelcomeToast = true;
            pendingWelcomeVenueCount = ConfigManager.Config?.Venues.Count ?? 0;
        }
        else if (Configuration.LastSeenVersion != ChangelogData.PluginVersion)
        {

            if (ChangelogData.CrossedAny(ChangelogData.ForcedSetupVersions, Configuration.LastSeenVersion, ChangelogData.PluginVersion))
            {
                Configuration.HasSeenSetup = false;
                Configuration.PendingForcedSetup = true;
                pendingUpdateToast = true;
            }

            else if (ChangelogData.CrossedAny(ChangelogData.HighlightVersions, Configuration.LastSeenVersion, ChangelogData.PluginVersion))
            {
                Configuration.PendingWhatsNew = true;
            }
            else
            {
                pendingUpdateToast = true;
            }
        }

        if (Configuration.LastSeenVersion != ChangelogData.PluginVersion)
        {
            Configuration.LastSeenVersion = ChangelogData.PluginVersion;
            Configuration.Save();
        }

        GameMapMarkers = new GameMapMarkers(this, Framework, GameGui, AddonLifecycle, Log);
        BannerFont = PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(
            new Dalamud.Interface.GameFonts.GameFontStyle(Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Jupiter23));
        VenueMapWindow = new VenueMapWindow(this);
        SettingsWindow = new SettingsWindow(this);
        OwnerSubmitWindow = new OwnerSubmitWindow(this);
        OwnerUpdateWindow = new OwnerSubmitWindow(this, isUpdateMode: true);
        OwnerVerifyWindow = new OwnerVerifyWindow(this);
        SetupWindow = new SetupWindow(this);
        DebugWindow = new DebugWindow(PositionTracker, this);
        VenueDetailWindow = new VenueDetailWindow(this);
        VenueQuickPopupWindow = new VenueQuickPopupWindow(this);
        NotificationHistoryWindow = new NotificationHistoryWindow(this);
        UpdateNotesWindow = new UpdateNotesWindow(this);
        FestivalSubmitWindow = new FestivalSubmitWindow(this);

        WindowSystem.AddWindow(VenueMapWindow);
        WindowSystem.AddWindow(SettingsWindow);
        WindowSystem.AddWindow(OwnerSubmitWindow);
        WindowSystem.AddWindow(OwnerUpdateWindow);
        WindowSystem.AddWindow(OwnerVerifyWindow);
        WindowSystem.AddWindow(SetupWindow);
        WindowSystem.AddWindow(DebugWindow);
        WindowSystem.AddWindow(VenueDetailWindow);
        WindowSystem.AddWindow(VenueQuickPopupWindow);
        WindowSystem.AddWindow(NotificationHistoryWindow);
        WindowSystem.AddWindow(UpdateNotesWindow);
        WindowSystem.AddWindow(FestivalSubmitWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the VenueMapper window. Use '/vmapper settings' for settings, '/vmapper pull now' to refresh config, '/vmapper debug' for the debug window, '/vmapper quick' for the quick popup, '/vmapper festivals' for festivals, '/vmapper festival' to submit one."
        });

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenMainUi += OnOpenMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;

        Framework.Update += OnFrameworkUpdate;

        if (Configuration.AutoPullOnStartup)
        {
            _ = AutoPullConfigAsync();
        }
    }

    private string? stampedVenueId;
    private DateTime lastPassportCheck = DateTime.MinValue;
    private static readonly TimeSpan PassportCheckInterval = TimeSpan.FromSeconds(5);
    private Venue? presentVenue;
    private Venue? venueCandidate;
    private DateTime venueCandidateSince = DateTime.MinValue;
    private Festival? presentFestival;
    private Festival? festivalCandidate;
    private DateTime festivalCandidateSince = DateTime.MinValue;
    private static readonly TimeSpan OutdoorEnterDelay = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan LeaveDelay = TimeSpan.FromSeconds(3);
    private bool lastShowQuickPopupOnEnter;
    private bool quickPopupSettingInitialized;
    private bool setupShownThisSession;
    private bool whatsNewShownThisSession;
    private DateTime lastAutoCheck = DateTime.Now;
    private DateTime lastEventReminderCheck = DateTime.MinValue;
    private readonly Dictionary<string, DateTime> remindedEventIds = new();
    private readonly Dictionary<string, bool> favoriteOpenState = new();
    private DateTime lastFavoriteCheck = DateTime.MinValue;
    private bool pendingWelcomeToast;
    private bool pendingUpdateToast;
    private int pendingWelcomeVenueCount;
    private string? pendingClosedCheckXivId;
    private string? pendingClosedCheckVenueName;
    private DateTime pendingClosedCheckAt = DateTime.MaxValue;

    private void OnOpenMainUi() => VenueMapWindow.IsOpen = true;
    private void OnOpenConfigUi() => SettingsWindow.IsOpen = true;

    private void OnHouseEnterFailed() => Toasts.Show(Lang.ToastHouseEnterFailed, ToastKind.Warning, 4.0);

    private void UpdateVenuePresence(VenueConfig? config)
    {
        var atVenue = config != null ? PositionTracker.GetVenueAtCurrentPlotIncludingGarden(config) : null;
        var now = DateTime.Now;
        if (atVenue != venueCandidate)
        {
            venueCandidate = atVenue;
            venueCandidateSince = now;
        }
        if (venueCandidate == presentVenue) return;

        var delay = venueCandidate == null ? LeaveDelay
                  : PositionTracker.IsInsideHouse ? TimeSpan.Zero
                  : OutdoorEnterDelay;
        if (now - venueCandidateSince < delay) return;

        presentVenue = venueCandidate;
        if (presentVenue != null)
            OnVenueEntered(presentVenue);
        else
            OnVenueLeft(config);
    }

    private void OnVenueEntered(Venue venue)
    {
        var isOwnVenue = venue.OwnerIdHashes.Contains(OwnerIdHelper.ComputeHash(PlayerState.ContentId));
        var shouldAutoOpen = Configuration.AutoOpenOnVenueEnter && (!isOwnVenue || Configuration.AutoOpenOwnVenue);
        if (shouldAutoOpen)
        {
            VenueMapWindow.IsOpen = true;
            VenueMapWindow.HideDirectory();
        }
        if (Configuration.ShowQuickPopupOnEnter)
            VenueQuickPopupWindow.Open(venue);
        ShowVenueEntryToasts(venue);
        HouseRules.OnVenueEntered(venue);
    }

    public string PassportCharacter()
    {
        if (!PlayerState.IsLoaded) return string.Empty;
        var world = PlayerState.HomeWorld.ValueNullable?.Name.ToString();
        return string.IsNullOrEmpty(world) ? PlayerState.CharacterName : $"{PlayerState.CharacterName} @ {world}";
    }

    private void UpdatePassport(VenueConfig? config)
    {
        var venue = presentVenue;
        if (venue == null)
        {
            stampedVenueId = null;
            return;
        }
        if (config == null || !PositionTracker.IsInsideHouse || stampedVenueId == venue.VenueId) return;
        if (DateTime.Now - lastPassportCheck < PassportCheckInterval) return;
        lastPassportCheck = DateTime.Now;
        if (!GetVenueStatus(venue).Open) return;
        stampedVenueId = venue.VenueId;

        if (Passport.Record(PlayerState.ContentId, PassportCharacter(), venue, config.Venues) is not { } result) return;
        if (result.NewStamp && Passport.Book(PlayerState.ContentId) is { } book)
            Toasts.Show(Lang.PassportStampNew(UIConstants.DisplayName(venue.Name)), ToastKind.Egg, 5.0,
                detail: Lang.PassportProgress(PassportManager.Visited(book, config.Venues), PassportManager.Total(config.Venues)),
                color: venue.Colors?.PrimaryVec, color2: venue.Colors?.SecondaryVec);
        foreach (var badge in result.Earned)
            Toasts.Announce(Lang.PassportBadgeEarned(PassportView.BadgeName(badge)), PassportView.BadgeDescription(badge),
                ToastKind.Egg, UIConstants.GoldAccent, UIConstants.Accent, 5.0);
    }

    private void OnVenueLeft(VenueConfig? config)
    {
        VenueQuickPopupWindow.IsOpen = false;
        if (config == null || PositionTracker.GetWardVenues(config).Count == 0)
            VenueMapWindow.ShowDirectory();
    }

    private void UpdateFestivalPresence(VenueConfig? config)
    {
        var here = config != null ? PositionTracker.GetFestivalHere(config) : null;
        var now = DateTime.Now;
        if (here != festivalCandidate)
        {
            festivalCandidate = here;
            festivalCandidateSince = now;
        }
        if (festivalCandidate == presentFestival) return;

        var delay = festivalCandidate == null ? LeaveDelay : OutdoorEnterDelay;
        if (now - festivalCandidateSince < delay) return;

        presentFestival = festivalCandidate;
        if (presentFestival == null) return;

        var festivalName = UIConstants.DisplayName(presentFestival.Name);
        var nextDay = presentFestival.NextDay(DateTime.UtcNow);
        Toasts.Announce(presentFestival.IsLive(DateTime.UtcNow) || nextDay == null
            ? Lang.ToastWelcomeToFestival(festivalName)
            : Lang.ToastFestivalStartsLater(festivalName, UIConstants.FormatServerTime(nextDay.Start)),
            string.IsNullOrWhiteSpace(presentFestival.Organizer) ? null : presentFestival.Organizer,
            ToastKind.Info, presentFestival.Colors?.PrimaryVec, presentFestival.Colors?.SecondaryVec, 4.5);
    }

    public bool ShowFestivalOnMap(Festival festival)
    {
        var (territoryId, mapId) = FestivalMap(festival);
        var location = festival.Location;
        var opened = territoryId != 0 && mapId != 0
                     && GameGui.OpenMapWithMapLink(territoryId, mapId, new System.Numerics.Vector3(location.X, location.Z, location.Y));
        if (!opened)
            Toasts.Show(Lang.ToastShowOnMapFailed, ToastKind.Warning, 3.0);
        return opened;
    }

    public (uint TerritoryId, uint MapId) FestivalMap(Festival festival)
    {
        var location = festival.Location;
        var territoryId = location.TerritoryId;
        if (festival.IsWard && territoryId == 0)
            territoryId = HousingData.WardTerritory(HousingData.ParseDistrict(location.District));
        var mapId = location.MapId != 0 ? location.MapId : HousingData.DefaultMapId(territoryId);
        return (territoryId, mapId);
    }

    public bool TeleportToFestival(Festival festival)
    {
        if (!Lifestream.IsLoaded)
        {
            Toasts.Show(Lang.ToastLifestreamMissing, ToastKind.Warning, 3.5);
            return false;
        }

        var location = festival.Location;
        bool ok;
        if (festival.IsWard)
        {
            var district = HousingData.ParseDistrict(location.District);
            var plot = HousingData.NearestPlot(district, location.X, location.Y);
            ok = plot > 0 && Lifestream.NavigateTo(
                $"{festival.Datacenter} - {festival.Server} - {HousingData.EnglishName(district)} - Ward {location.Ward} - Plot {plot}");
        }
        else
        {
            var aetheryte = location.AetheryteId != 0
                ? HousingData.AetheryteName(location.AetheryteId)
                : HousingData.NearestAetheryte(location.TerritoryId, location.X, location.Y).Name;
            var sameWorld = string.IsNullOrEmpty(festival.Server)
                            || string.Equals(festival.Server, PositionTracker.CurrentServerName, StringComparison.OrdinalIgnoreCase);
            var command = sameWorld
                ? (aetheryte.Length > 0 ? $"tp {aetheryte}" : "")
                : (aetheryte.Length > 0 ? $"{festival.Server}, tp {aetheryte}" : festival.Server);
            ok = command.Length > 0 && Lifestream.ExecuteCommand(command);
        }

        var flagged = ok && SetFestivalFlag(festival);
        Toasts.Show(ok ? Lang.ToastTeleportingTo(UIConstants.DisplayName(festival.Name)) : Lang.ToastTeleportFailed,
            ok ? ToastKind.Success : ToastKind.Warning, 3.0, detail: flagged ? Lang.FestivalFlagSet : null);
        return ok;
    }

    public unsafe bool SetVenueFlag(Venue venue)
    {
        var district = PositionTracker.GetVenueDistrict(venue);
        if (!HousingData.TryGetPlotMapLink(district, venue.Plot, out var territoryId, out var mapId, out var position)) return false;
        var agent = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMap.Instance();
        if (agent == null) return false;
        agent->SetFlagMapMarker(territoryId, mapId, position);
        return true;
    }
    public unsafe bool SetFestivalFlag(Festival festival)
    {
        var (territoryId, mapId) = FestivalMap(festival);
        if (territoryId == 0 || mapId == 0) return false;
        var agent = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMap.Instance();
        if (agent == null) return false;
        var location = festival.Location;
        agent->SetFlagMapMarker(territoryId, mapId, new System.Numerics.Vector3(location.X, location.Z, location.Y));
        return true;
    }

    public bool ShowVenueOnMap(Venue venue)
    {
        var district = PositionTracker.GetVenueDistrict(venue);
        var opened = HousingData.TryGetPlotMapLink(district, venue.Plot, out var territoryId, out var mapId, out var position)
                     && GameGui.OpenMapWithMapLink(territoryId, mapId, position);
        if (!opened)
            Toasts.Show(Lang.ToastShowOnMapFailed, ToastKind.Warning, 3.0);
        return opened;
    }

    public void CancelTeleport()
    {
        if (Lifestream.Abort())
            Toasts.Show(Lang.ToastTeleportCancelled, ToastKind.Info, 2.5);
    }

    private void OnKonamiCompleted()
    {
        EasterEggManager.Unlock(EasterEggIds.RgbOverload);
        EasterEggManager.Unlock(EasterEggIds.HackerMode, autoEnable: false);
    }

    private void OnEasterEggUnlocked(string id)
        => Toasts.Show(Lang.EggUnlockedToast(EasterEggUI.GetName(id)), ToastKind.Egg, 3.5);

    private void OnAllEasterEggsUnlocked()
        => Toasts.Show(Lang.ToastAllEggsFound, ToastKind.Egg, 5.0);

    private static bool IsInCombatOrDuty()
        => Condition[ConditionFlag.InCombat]
        || Condition[ConditionFlag.BoundByDuty]
        || Condition[ConditionFlag.BoundByDuty56]
        || Condition[ConditionFlag.BoundByDuty95];

    private void CheckEventReminders()
    {
        var cfg = ConfigManager.Config;
        if (cfg == null) return;

        var now = DateTime.UtcNow;
        foreach (var id in remindedEventIds.Where(kv => kv.Value < now).Select(kv => kv.Key).ToList())
            remindedEventIds.Remove(id);

        var venues = Configuration.EventRemindersFavoritesOnly
            ? cfg.Venues.Where(v => Configuration.FavoriteVenueIds.Contains(v.VenueId))
            : cfg.Venues.AsEnumerable();

        foreach (var v in venues)
        {
            if (v.TeamId <= 0) continue;
            PartakeApi.RequestTeam(v.TeamId);

            foreach (var evt in PartakeApi.GetEvents(v.TeamId))
            {
                if (remindedEventIds.ContainsKey(evt.EventId)) continue;

                var minutesUntil = (evt.StartTime - now).TotalMinutes;
                if (minutesUntil > 0 && minutesUntil <= Configuration.EventReminderMinutes)
                {
                    remindedEventIds[evt.EventId] = evt.StartTime;
                    Toasts.Show(Lang.ToastEventSoon(UIConstants.DisplayName(evt.Title), UIConstants.DisplayName(v.Name), (int)Math.Ceiling(minutesUntil)), ToastKind.Info, 5.0);
                }
            }
        }

        foreach (var festival in cfg.Festivals)
        {
            if (festival.NextDay(now) is not { } day) continue;
            var key = $"festival:{festival.FestivalId}:{day.Start:yyyyMMddHHmm}";
            if (remindedEventIds.ContainsKey(key)) continue;

            var minutesUntil = (day.Start - now).TotalMinutes;
            if (minutesUntil > 0 && minutesUntil <= Configuration.EventReminderMinutes)
            {
                remindedEventIds[key] = day.Start;
                Toasts.Show(Lang.ToastFestivalSoon(UIConstants.DisplayName(festival.Name), (int)Math.Ceiling(minutesUntil)), ToastKind.Info, 5.0);
            }
        }
    }

    public readonly record struct VenueOpening(bool Open, DateTime? Until, DateTime? Next, bool Known);

    public VenueOpening GetVenueOpening(Venue v, bool request = true)
    {
        var now = DateTime.UtcNow;
        var known = false;
        var open = false;
        DateTime? until = null;
        DateTime? next = null;

        var xivId = UI.VenueMapWindow.ExtractXivVenuesId(v.Links?.FfxivVenues);
        if (xivId != null)
        {
            if (request) XivVenues.RequestSchedule(xivId);
            if (XivVenues.GetSchedule(xivId) is { } schedule)
            {
                known = true;
                if (schedule.IsOpenNow)
                {
                    open = true;
                    until = schedule.NextEnd;
                }
                else if (schedule.NextStart is { } start && start > now)
                {
                    next = start;
                }
            }
        }

        if (v.TeamId > 0)
        {
            if (request) PartakeApi.RequestTeam(v.TeamId);
            known |= PartakeApi.HasLoaded(v.TeamId);
            foreach (var evt in PartakeApi.GetEvents(v.TeamId))
            {
                if (evt.StartTime <= now && evt.EndTime >= now)
                {
                    if (!open || until is not { } end || evt.EndTime > end) until = evt.EndTime;
                    open = true;
                }
                else if (evt.StartTime > now && (next is not { } first || evt.StartTime < first))
                {
                    next = evt.StartTime;
                }
            }
        }

        return open ? new VenueOpening(true, until, null, known) : new VenueOpening(false, null, next, known);
    }

    public static DateTime TodayEndUtc(DateTime nowUtc)
        => nowUtc.ToLocalTime().Date.AddDays(1).AddHours(6).ToUniversalTime();

    public bool IsOpenToday(Venue v, DateTime nowUtc)
    {
        var opening = GetVenueOpening(v);
        return opening.Open || (opening.Next is { } start && start <= TodayEndUtc(nowUtc));
    }

    public (string Text, bool Open, bool Known) GetVenueStatus(Venue v)
    {
        var opening = GetVenueOpening(v);
        if (opening.Open) return (Lang.StatusOpenNow, true, true);
        if (opening.Next is not { } start) return ("", false, opening.Known);

        var diff = start - DateTime.UtcNow;
        if (diff.TotalMinutes < 60) return (Lang.StatusOpensInMin((int)diff.TotalMinutes), false, true);
        if (diff.TotalHours < 24) return (Lang.StatusOpensInHours((int)diff.TotalHours), false, true);
        return (Lang.StatusOpensInDays((int)diff.TotalDays), false, true);
    }

    private void CheckFavoriteOpenings()
    {
        var cfg = ConfigManager.Config;
        if (cfg == null) return;

        foreach (var id in favoriteOpenState.Keys.Where(id => !Configuration.FavoriteVenueIds.Contains(id)).ToList())
            favoriteOpenState.Remove(id);

        foreach (var v in cfg.Venues)
        {
            if (!Configuration.FavoriteVenueIds.Contains(v.VenueId)) continue;
            var (_, open, known) = GetVenueStatus(v);
            if (!known) continue;

            if (open && favoriteOpenState.TryGetValue(v.VenueId, out var wasOpen) && !wasOpen
                && Configuration.FavoriteOpenNotify && presentVenue?.VenueId != v.VenueId)
            {
                var (_, until, _) = GetVenueTimes(v);
                Toasts.Announce(Lang.ToastFavoriteOpened(UIConstants.DisplayName(v.Name)),
                    until is { } end ? Lang.SignOpenUntil(UIConstants.FormatServerTime(end)) : null,
                    ToastKind.Success, v.Colors?.PrimaryVec, v.Colors?.SecondaryVec, 5.0);
            }

            favoriteOpenState[v.VenueId] = open;
        }
    }

    private void ShowVenueEntryToasts(Venue venue)
    {
        var status = GetVenueStatus(venue);
        Toasts.Announce(Lang.ToastWelcomeToVenue(UIConstants.DisplayName(venue.Name)),
            status.Open && status.Text.Length > 0 ? status.Text : null,
            ToastKind.Info, venue.Colors?.PrimaryVec, venue.Colors?.SecondaryVec, 4.0);

        var xivId = UI.VenueMapWindow.ExtractXivVenuesId(venue.Links?.FfxivVenues);
        if (xivId == null) return;

        XivVenues.RequestSchedule(xivId);
        var sched = XivVenues.GetSchedule(xivId);
        if (sched != null)
        {
            if (!sched.IsOpenNow)
                Toasts.Show(Lang.ToastVenueClosed(venue.Name), ToastKind.Info, 3.5);
        }
        else
        {

            pendingClosedCheckXivId = xivId;
            pendingClosedCheckVenueName = venue.Name;
            pendingClosedCheckAt = DateTime.Now.AddSeconds(2.5);
        }
    }

    private void CheckPendingClosedToast()
    {
        if (pendingClosedCheckXivId == null || DateTime.Now < pendingClosedCheckAt) return;

        if (presentVenue != null)
        {
            var sched = XivVenues.GetSchedule(pendingClosedCheckXivId);
            if (sched != null && !sched.IsOpenNow && pendingClosedCheckVenueName != null)
                Toasts.Show(Lang.ToastVenueClosed(pendingClosedCheckVenueName), ToastKind.Info, 3.5);
        }

        pendingClosedCheckXivId = null;
        pendingClosedCheckVenueName = null;
        pendingClosedCheckAt = DateTime.MaxValue;
    }

    private DateTime lastFrameworkError = DateTime.MinValue;

    private void OnFrameworkUpdate(IFramework framework)
    {
        try
        {
            if (!ClientState.IsLoggedIn)
                return;

            if (pendingWelcomeToast)
            {
                pendingWelcomeToast = false;
                Toasts.Show(Lang.ToastWelcomeFirstLoad(pendingWelcomeVenueCount), ToastKind.Success, 4.0);
            }
            else if (pendingUpdateToast)
            {
                pendingUpdateToast = false;
                Toasts.Show(Lang.ToastUpdated(ChangelogData.PluginVersion), ToastKind.Info, 4.0);
            }
            CheckPendingClosedToast();

            if (!Configuration.HasSeenSetup && !setupShownThisSession)
            {
                setupShownThisSession = true;
                SetupWindow.Forced = Configuration.PendingForcedSetup;
                SetupWindow.RefreshFromConfig();
                SetupWindow.IsOpen = true;
            }

            if (Configuration.PendingWhatsNew && !whatsNewShownThisSession)
            {
                whatsNewShownThisSession = true;
                UpdateNotesWindow.Open();
            }

            if (Configuration.AutoPullOnStartup && (DateTime.Now - lastAutoCheck).TotalHours >= 1 && !string.IsNullOrWhiteSpace(Configuration.GitHubConfigUrl))
            {
                lastAutoCheck = DateTime.Now;
                _ = AutoPullConfigAsync();
            }

            if (Configuration.EventReminders && (DateTime.Now - lastEventReminderCheck).TotalSeconds >= 60)
            {
                lastEventReminderCheck = DateTime.Now;
                CheckEventReminders();
            }

            if ((DateTime.Now - lastFavoriteCheck).TotalSeconds >= 60)
            {
                lastFavoriteCheck = DateTime.Now;
                CheckFavoriteOpenings();
            }

            PositionTracker.Update(ConfigManager.Config);

            var config = ConfigManager.Config;
            UpdateVenuePresence(config);
            UpdatePassport(config);
            Guide.Tick(config);
#if DEBUG
            Shots.Tick();
#endif
            UpdateFestivalPresence(config);
            var currentVenue = presentVenue;
            var isInVenue = currentVenue != null;

            if (!quickPopupSettingInitialized)
            {
                lastShowQuickPopupOnEnter = Configuration.ShowQuickPopupOnEnter;
                quickPopupSettingInitialized = true;
            }
            else if (Configuration.ShowQuickPopupOnEnter != lastShowQuickPopupOnEnter)
            {
                if (isInVenue)
                {
                    if (Configuration.ShowQuickPopupOnEnter)
                        VenueQuickPopupWindow.Open(currentVenue!);
                    else
                        VenueQuickPopupWindow.IsOpen = false;
                }
                lastShowQuickPopupOnEnter = Configuration.ShowQuickPopupOnEnter;
            }

        }
        catch (Exception ex)
        {
            if ((DateTime.Now - lastFrameworkError).TotalSeconds > 10)
            {
                Log.Error(ex, "[VenueMapper] Framework update error");
                lastFrameworkError = DateTime.Now;
            }
        }
    }

    private void OnCommand(string command, string args)
    {
        args = args.Trim();

        if (args.Equals("settings", StringComparison.OrdinalIgnoreCase))
        {
            VenueMapWindow.IsOpen = true;
            VenueMapWindow.ShowSettings();
            return;
        }

        if (args.Equals("debug", StringComparison.OrdinalIgnoreCase))
        {
            DebugWindow.IsOpen = !DebugWindow.IsOpen;
            return;
        }

        if (args.Equals("quick", StringComparison.OrdinalIgnoreCase))
        {
            if (!Configuration.ShowQuickPopupOnEnter)
            {
                Toasts.Show(Lang.ToastQuickPopupDisabled, ToastKind.Warning, 3.5);
                return;
            }

            if (VenueQuickPopupWindow.IsOpen)
            {
                VenueQuickPopupWindow.IsOpen = false;
            }
            else
            {
                var vmConfig = ConfigManager.Config;
                var currentVenue = vmConfig != null ? PositionTracker.GetVenueAtCurrentPlotIncludingGarden(vmConfig) : null;
                if (currentVenue != null)
                    VenueQuickPopupWindow.Open(currentVenue);
                else
                    Toasts.Show(Lang.ToastMustBeInVenue, ToastKind.Warning, 2.5);
            }
            return;
        }

        if (args.Equals("pull now", StringComparison.OrdinalIgnoreCase) || args.Equals("pull", StringComparison.OrdinalIgnoreCase))
        {
            _ = ManualPullConfigAsync();
            return;
        }

        if (args.Equals("venues", StringComparison.OrdinalIgnoreCase))
        {
            VenueMapWindow.IsOpen = true;
            VenueMapWindow.ShowDirectory();
            return;
        }

        if (args.Equals("map", StringComparison.OrdinalIgnoreCase))
        {
            VenueMapWindow.IsOpen = true;
            VenueMapWindow.HideDirectory();
            return;
        }

        if (args.Equals("events", StringComparison.OrdinalIgnoreCase))
        {
            VenueMapWindow.IsOpen = true;
            VenueMapWindow.ShowEvents();
            return;
        }

        if (args.Equals("festivals", StringComparison.OrdinalIgnoreCase))
        {
            VenueMapWindow.IsOpen = true;
            VenueMapWindow.ShowFestivals();
            return;
        }

        if (args.Equals("festival", StringComparison.OrdinalIgnoreCase))
        {
            FestivalSubmitWindow.IsOpen = true;
            return;
        }

        if (args.Equals("owner", StringComparison.OrdinalIgnoreCase))
        {
            OwnerSubmitWindow.IsOpen = true;
            return;
        }

        if (args.StartsWith("markers", StringComparison.OrdinalIgnoreCase))
        {
            var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var sub = parts.Length > 1 ? parts[1].ToLowerInvariant() : "";
            PictomancyMarkers.Enabled = sub switch
            {
                "on"  => true,
                "off" => false,
                _     => !PictomancyMarkers.Enabled,
            };
            Configuration.Markers3DEnabled = PictomancyMarkers.Enabled;
            Configuration.Save();
            Log.Information($"[VenueMapper] 3D markers: {(PictomancyMarkers.Enabled ? "ON" : "OFF")}");
            return;
        }

        if (VenueMapWindow.IsOpen)
        {
            VenueMapWindow.IsOpen = false;
        }
        else
        {
            VenueMapWindow.IsOpen = true;
            var config = ConfigManager.Config;
            var mapAvailable = config != null
                && (PositionTracker.GetVenueAtCurrentPlotIncludingGarden(config) != null
                    || PositionTracker.GetFestivalHere(config) != null
                    || PositionTracker.GetWardVenues(config).Count > 0);
            if (mapAvailable)
                VenueMapWindow.HideDirectory();
            else
                VenueMapWindow.ShowDirectory();
        }
    }

    private async System.Threading.Tasks.Task AutoPullConfigAsync()
    {
        try
        {
            var result = await GitHubPuller.PullAsync(Configuration.GitHubConfigUrl);
            if (result == PullResult.Updated)
            {
                Log.Information("[VenueMapper] Auto-pull: config updated from GitHub");
                Toasts.Show(Lang.ToastConfigUpdated, ToastKind.Success, 3.0);
            }
            else if (result == PullResult.Failed)
            {
                Toasts.Show(Lang.ToastConfigPullFailed, ToastKind.Warning, 3.5);
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"[VenueMapper] Auto-pull failed: {ex.Message}");
        }
    }

    public async System.Threading.Tasks.Task ManualPullConfigAsync()
    {
        try
        {
            var result = await GitHubPuller.PullAsync(Configuration.GitHubConfigUrl, force: true);
            switch (result)
            {
                case PullResult.Updated:
                    Toasts.Show(Lang.ToastConfigUpdated, ToastKind.Success, 3.0);
                    break;
                case PullResult.Unchanged:
                    Toasts.Show(Lang.ToastConfigUpToDate, ToastKind.Info, 2.5);
                    break;
                case PullResult.Failed:
                    Toasts.Show(Lang.ToastConfigPullFailed, ToastKind.Warning, 3.5);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"[VenueMapper] Manual pull failed: {ex.Message}");
            Toasts.Show(Lang.ToastConfigPullFailed, ToastKind.Info, 3.5);
        }
    }

    private void DrawUI()
    {
        UIConstants.OverrideMode = EasterEggManager.IsHackerModeActive ? ColorOverrideMode.Hacker
            : EasterEggManager.IsRgbOverloadActive ? ColorOverrideMode.Rgb
            : ColorOverrideMode.None;
        if (UIConstants.OverrideMode == ColorOverrideMode.Rgb)
            UIConstants.RgbHue = (float)(ImGui.GetTime() / 3.0) % 1f;
        UIConstants.IsHackerBooting = EasterEggManager.IsHackerModeBooting;

        using (UIConstants.PushTheme())
        {
            WindowSystem.Draw();

            Toasts.MaxVisible = Configuration.MaxVisibleToasts;
            Toasts.DurationMultiplier = Configuration.ToastDurationMultiplier;
            Toasts.BannersEnabled = Configuration.NotifyBanners;
            var suppressedByCombat = Configuration.SuppressInCombat && IsInCombatOrDuty();
            Toasts.SetPaused(suppressedByCombat);
            if (!suppressedByCombat)
            {
                HouseRules.Draw(Configuration.ToastPosition);
                UI.ToastOverlay.Draw(Toasts, Configuration.ToastPosition, Configuration.NotificationsEnabled, BannerFont, HouseRules.ReservedHeight);
            }
        }

        if (!ClientState.IsLoggedIn)
        {
            PictomancyMarkers.Flush();
            return;
        }

        GameMapMarkers.DrawOverlay();

        var config = ConfigManager.Config;
        var route = Guide.Current;
        if (config == null || !PictomancyMarkers.Available || (!PictomancyMarkers.Enabled && route == null))
        {
            PictomancyMarkers.Flush();
            return;
        }

        PictomancyMarkers.BehindGameUi = Configuration.MarkersBehindUi && !ScreenshotMode;
        var access = new Services.MarkerAccessibilityOptions
        {
            SizeScale = Configuration.MarkerSizeScale,
            ColorOverrideEnabled = Configuration.MarkerColorOverrideEnabled,
            OverrideColor = Configuration.MarkerOverrideColor,
            StrongPulse = Configuration.MarkerStrongPulse,
            Names = Configuration.MarkerNames,
            ThroughWalls = Configuration.MarkerThroughWalls,
            VenueColors = Configuration.MarkerVenueColors,
            SignRange = Configuration.VenueSignRange,
            GuideAnimation = Configuration.GuideAnimation,
            GuideColor = Configuration.GuideColor,
            GuideSingleColor = new System.Numerics.Vector4(Configuration.GuideSingleColor, 1f),
        };
        if (ScreenshotMode)
        {
            access.SizeScale = 1f;
            access.VenueColors = true;
            access.StrongPulse = false;
            access.ColorOverrideEnabled = false;
            access.SignRange = Configuration.WholeWardRange;
        }

        var tracker = PositionTracker;
        var viewer = new System.Numerics.Vector3(tracker.PlayerX, tracker.PlayerY, tracker.PlayerZ);
        markerItems.Clear();
        if (tracker.IsInsideHouse)
        {
            var venue = tracker.GetVenueAtCurrentPlotIncludingGarden(config);
            var floor = venue != null && !HidesOwnMarkers(venue) ? tracker.GetCurrentFloor(venue) : null;
            if (floor != null)
            {
                var color = venue!.Colors?.PrimaryVec;
                foreach (var service in floor.Services)
                    markerItems.Add(new Services.MarkerItem(service, color));
            }
            PictomancyMarkers.Draw(markerItems, Configuration.ServiceFilters, access, viewer, route: route);
            return;
        }

        var playerPos = new System.Numerics.Vector2(tracker.PlayerX, tracker.PlayerZ);
        if (tracker.GetVenueAtCurrentPlotIncludingGarden(config) is { } plotVenue && !HidesOwnMarkers(plotVenue)
            && plotVenue.Floors.FirstOrDefault(f => HousingData.IsGarden(f.Name)) is { } garden)
        {
            var color = plotVenue.Colors?.PrimaryVec;
            foreach (var service in garden.Services)
                markerItems.Add(new Services.MarkerItem(service, color));
        }

        var festival = tracker.GetFestivalHere(config);
        if (festival != null)
        {
            var color = festival.Colors?.PrimaryVec;
            foreach (var service in festival.Services)
                markerItems.Add(new Services.MarkerItem(service, color));
        }

        var signs = Configuration.VenueSigns != VenueSignsMode.Off ? GetVenueSigns(config) : null;
        PictomancyMarkers.Draw(markerItems, Configuration.ServiceFilters, access, viewer, playerPos, signs, route);
    }

    private readonly List<Services.MarkerItem> markerItems = new();
    private readonly List<Services.VenueSign> venueSigns = new();
    private DateTime venueSignsBuiltAt = DateTime.MinValue;
    private static readonly TimeSpan VenueSignsRefresh = TimeSpan.FromSeconds(1);

    public (bool Open, DateTime? Until, DateTime? Next) GetVenueTimes(Venue v)
    {
        var opening = GetVenueOpening(v, false);
        return (opening.Open, opening.Until, opening.Next);
    }

    private IReadOnlyList<Services.VenueSign> GetVenueSigns(VenueConfig config)
    {
        if (DateTime.Now - venueSignsBuiltAt < VenueSignsRefresh) return venueSigns;
        venueSignsBuiltAt = DateTime.Now;
        venueSigns.Clear();

        var now = DateTime.UtcNow;
        var mode = ScreenshotMode ? VenueSignsMode.All : Configuration.VenueSigns;
        var district = PositionTracker.CurrentDistrict;
        var wholeWard = Configuration.VenueSignRange >= Configuration.WholeWardRange;
        foreach (var venue in PositionTracker.GetWardVenues(config))
        {
            if (Configuration.HiddenVenueIds.Contains(venue.VenueId)) continue;
            var favorite = Configuration.FavoriteVenueIds.Contains(venue.VenueId);
            if (mode == VenueSignsMode.Favorites && !favorite) continue;
            if (wholeWard && HousingData.TryGetPlotMapLink(district, venue.Plot, out _, out var plotMap, out _) && plotMap != PositionTracker.CurrentMapId) continue;
            if (!VenueSignPosition(district, venue.Plot, out var position)) continue;

            var open = GetVenueStatus(venue).Open;
            if (mode == VenueSignsMode.Open && !open) continue;
            var (_, until, next) = GetVenueTimes(venue);
            string? line2 = open
                ? until is { } end ? Lang.SignOpenUntil(UIConstants.FormatServerTime(end)) : Lang.StatOpenNow
                : next is { } start && start - now < TimeSpan.FromHours(24) ? Lang.SignOpensAt(UIConstants.FormatServerTime(start)) : null;
            var accent = venue.Colors?.PrimaryVec ?? UIConstants.TextPrimary;
            var secondary = venue.Colors?.SecondaryVec ?? accent;
            venueSigns.Add(new Services.VenueSign(venue.VenueId, position, venue.Name, line2, open, favorite, accent, secondary));
        }
        return venueSigns;
    }

    public bool VenueSignPosition(HousingDistrict district, int plot, out System.Numerics.Vector3 position)
        => VenueSignPosition(district, plot, out position, out _);
    public bool VenueSignPosition(HousingDistrict district, int plot, out System.Numerics.Vector3 position, out float groundY)
    {
        position = default;
        groundY = 0f;
        if (!HousingData.TryGetPlotMapLink(district, plot, out _, out _, out var plotPos)) return false;
        var lift = HousingData.PlotSize(district, plot) switch
        {
            0 => 9f,
            2 => 15f,
            _ => 12f,
        };
        groundY = plotPos.Y;
        position = plotPos with { Y = plotPos.Y + lift };
        return true;
    }

    private bool HidesOwnMarkers(Venue venue)
        => !ScreenshotMode && Configuration.HideMarkersInOwnVenue
           && venue.OwnerIdHashes.Contains(OwnerIdHelper.ComputeHash(PlayerState.ContentId));

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenMainUi -= OnOpenMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
        KonamiDetector.OnCompleted -= OnKonamiCompleted;
        EasterEggManager.OnUnlocked -= OnEasterEggUnlocked;
        EasterEggManager.OnAllUnlocked -= OnAllEasterEggsUnlocked;

        WindowSystem.RemoveAllWindows();
        VenueMapWindow.Dispose();
        SettingsWindow.Dispose();
        OwnerSubmitWindow.Dispose();
        OwnerUpdateWindow.Dispose();
        OwnerVerifyWindow.Dispose();
        SetupWindow.Dispose();
        DebugWindow.Dispose();
        VenueDetailWindow.Dispose();
        VenueQuickPopupWindow.Dispose();
        NotificationHistoryWindow.Dispose();
        UpdateNotesWindow.Dispose();
        FestivalSubmitWindow.Dispose();
        MapLoader.Dispose();
        GitHubPuller.Dispose();
        Lifestream.HouseEnterFailed -= OnHouseEnterFailed;
        Lifestream.Dispose();
        PositionTracker.Dispose();
        GameMapMarkers.Dispose();
        Placards.Dispose();
        PartyFinderAds.Dispose();
        ChatLinks.Dispose();
        BannerFont.Dispose();
        PictomancyMarkers.Dispose();
        MarkerTextFont.Dispose();
        MarkerIconFont.Dispose();
        PartakeApi.Dispose();
        XivVenues.Dispose();
        VenueLogos.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }
}
