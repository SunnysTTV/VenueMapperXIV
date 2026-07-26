using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
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

    private const ulong SunnyContentId = 18014598551440541;

    public Configuration Configuration { get; }
    public ConfigManager ConfigManager { get; }
    public GitHubConfigPuller GitHubPuller { get; }
    public PlayerPositionTracker PositionTracker { get; }
    public HousingMapLoader MapLoader { get; }
    public LifestreamService Lifestream { get; }
    public PictomancyMarkerManager PictomancyMarkers { get; }
    public PartakeApiService PartakeApi { get; }
    public XivVenuesService XivVenues { get; }
    public EasterEggManager EasterEggManager { get; }
    public KonamiDetector KonamiDetector { get; }
    public ToastManager Toasts { get; }

    public VenueMapWindow VenueMapWindow { get; }
    public SettingsWindow SettingsWindow { get; }
    public ChangelogWindow ChangelogWindow { get; }
    public OwnerSubmitWindow OwnerSubmitWindow { get; }
    public OwnerSubmitWindow OwnerUpdateWindow { get; }
    public OwnerVerifyWindow OwnerVerifyWindow { get; }
    public SetupWindow SetupWindow { get; }
    public DebugWindow DebugWindow { get; }
    public Dalamud.Interface.Windowing.WindowSystem WindowSystem { get; } = new("VenueMapper");

    public VenueMapperPlugin()
    {
        var existingConfig = PluginInterface.GetPluginConfig() as Configuration;
        var isBrandNewInstall = existingConfig == null;
        Configuration = existingConfig ?? new Configuration();
        if (string.IsNullOrWhiteSpace(Configuration.GitHubConfigUrl))
        {
            Configuration.GitHubConfigUrl = "https://raw.githubusercontent.com/SunnysTTV/VenueMapperXIV/main/Resources/venues.json";
            Configuration.Save();
        }
        UI.Lang.Set(Configuration.Language);
        UI.ChangelogData.CurrentLanguage = Configuration.Language;

        ConfigManager = new ConfigManager(Log, PluginInterface.ConfigDirectory.FullName);
        GitHubPuller = new GitHubConfigPuller(Log, ConfigManager);
        PositionTracker = new PlayerPositionTracker(ClientState, ObjectTable, DataManager, Log);
        MapLoader   = new HousingMapLoader(DataManager, TextureProvider, Log);
        Lifestream  = new LifestreamService(PluginInterface, Log);
        PictomancyMarkers = new PictomancyMarkerManager(PluginInterface, Log);
        PartakeApi   = new PartakeApiService(Log);
        XivVenues    = new XivVenuesService(Log);
        EasterEggManager = new EasterEggManager(Configuration, Log);
        KonamiDetector = new KonamiDetector(KeyState);
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
            pendingUpdateToast = true;
            // Only versions listed in ChangelogData.ForcedSetupVersions force the wizard - small
            // hotfixes (e.g. a v0.5.8.1 patch) still show the normal "updated to vX" toast, but
            // don't drag existing users through the whole wizard again for no reason.
            // PendingForcedSetup is persisted (not just an in-memory flag) so the forced,
            // unskippable wizard still triggers correctly even if the plugin reloads or the
            // game restarts before the user finishes it - it only clears once Finish() runs.
            if (ChangelogData.ForcedSetupVersions.Contains(ChangelogData.PluginVersion))
            {
                Configuration.HasSeenSetup = false;
                Configuration.PendingForcedSetup = true;
            }
        }

        if (Configuration.LastSeenVersion != ChangelogData.PluginVersion)
        {
            Configuration.LastSeenVersion = ChangelogData.PluginVersion;
            Configuration.Save();
        }

        VenueMapWindow = new VenueMapWindow(this);
        SettingsWindow = new SettingsWindow(this);
        ChangelogWindow = new ChangelogWindow();
        OwnerSubmitWindow = new OwnerSubmitWindow(this);
        OwnerUpdateWindow = new OwnerSubmitWindow(this, isUpdateMode: true);
        OwnerVerifyWindow = new OwnerVerifyWindow(this);
        SetupWindow = new SetupWindow(this);
        DebugWindow = new DebugWindow(PositionTracker, this);

        WindowSystem.AddWindow(VenueMapWindow);
        WindowSystem.AddWindow(SettingsWindow);
        WindowSystem.AddWindow(ChangelogWindow);
        WindowSystem.AddWindow(OwnerSubmitWindow);
        WindowSystem.AddWindow(OwnerUpdateWindow);
        WindowSystem.AddWindow(OwnerVerifyWindow);
        WindowSystem.AddWindow(SetupWindow);
        WindowSystem.AddWindow(DebugWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the Venue Map window. Use '/vmapper settings' for settings, '/vmapper pull now' to refresh config, '/vmapper debug' for the debug window."
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

    private bool wasInVenue;
    private bool wasInsideHouse;
    private bool setupShownThisSession;
    private DateTime lastAutoCheck = DateTime.MinValue;
    private DateTime lastEventReminderCheck = DateTime.MinValue;
    private readonly Dictionary<string, DateTime> remindedEventIds = new();
    private bool pendingWelcomeToast;
    private bool pendingUpdateToast;
    private int pendingWelcomeVenueCount;
    private string? pendingClosedCheckXivId;
    private string? pendingClosedCheckVenueName;
    private DateTime pendingClosedCheckAt = DateTime.MaxValue;

    private void OnOpenMainUi() => VenueMapWindow.IsOpen = true;
    private void OnOpenConfigUi() => SettingsWindow.IsOpen = true;

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
            _ = PartakeApi.FetchTeamAsync(v.TeamId);

            foreach (var evt in PartakeApi.GetEvents(v.TeamId))
            {
                if (remindedEventIds.ContainsKey(evt.EventId)) continue;

                var minutesUntil = (evt.StartTime - now).TotalMinutes;
                if (minutesUntil > 0 && minutesUntil <= Configuration.EventReminderMinutes)
                {
                    remindedEventIds[evt.EventId] = evt.StartTime;
                    Toasts.Show(Lang.ToastEventSoon(evt.Title, v.Name, (int)Math.Ceiling(minutesUntil)), ToastKind.Info, 5.0);
                }
            }
        }
    }

    private void ShowVenueEntryToasts(Venue venue)
    {
        Toasts.Show(Lang.ToastWelcomeToVenue(venue.Name), ToastKind.Info, 3.0);

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
            // Schedule not cached yet - the fetch was just queued, give it a moment and check again.
            pendingClosedCheckXivId = xivId;
            pendingClosedCheckVenueName = venue.Name;
            pendingClosedCheckAt = DateTime.Now.AddSeconds(2.5);
        }
    }

    private void CheckPendingClosedToast()
    {
        if (pendingClosedCheckXivId == null || DateTime.Now < pendingClosedCheckAt) return;

        if (wasInVenue)
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

            if ((DateTime.Now - lastAutoCheck).TotalHours >= 1 && !string.IsNullOrWhiteSpace(Configuration.GitHubConfigUrl))
            {
                lastAutoCheck = DateTime.Now;
                _ = AutoPullConfigAsync();
            }

            if (Configuration.EventReminders && (DateTime.Now - lastEventReminderCheck).TotalSeconds >= 60)
            {
                lastEventReminderCheck = DateTime.Now;
                CheckEventReminders();
            }

            PositionTracker.Update(ConfigManager.Config);

            var config = ConfigManager.Config;
            var currentVenue = config != null ? PositionTracker.GetCurrentVenue(config) : null;
            var isInVenue = currentVenue != null;

            if (isInVenue && !wasInVenue)
            {
                VenueMapWindow.IsOpen = true;
                VenueMapWindow.HideDirectory();
                ShowVenueEntryToasts(currentVenue!);
            }
            else if (!isInVenue && wasInVenue)
            {
                VenueMapWindow.ShowDirectory();
            }

            wasInVenue = isInVenue;

            var isInsideHouse = PositionTracker.IsInsideHouse;
            if (isInsideHouse && !wasInsideHouse && SunnyContentId != 0 && PlayerState.ContentId == SunnyContentId)
            {
                EasterEggManager.Unlock(EasterEggIds.SunnyDetection);
                if (EasterEggManager.IsEnabled(EasterEggIds.SunnyDetection))
                    Toasts.Show(VenueMapWindow.PickSunnyLine(), ToastKind.Egg, 3.0);
            }
            wasInsideHouse = isInsideHouse;
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
            var inVenue = ConfigManager.Config != null &&
                          PositionTracker.GetCurrentVenue(ConfigManager.Config) != null;
            if (inVenue)
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
                Toasts.Show(Lang.ToastConfigPullFailed, ToastKind.Info, 3.5);
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
                    Toasts.Show(Lang.ToastConfigPullFailed, ToastKind.Info, 3.5);
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

        WindowSystem.Draw();

        Toasts.MaxVisible = Configuration.MaxVisibleToasts;
        Toasts.DurationMultiplier = Configuration.ToastDurationMultiplier;
        var suppressedByCombat = Configuration.SuppressInCombat && IsInCombatOrDuty();
        Toasts.SetPaused(suppressedByCombat);
        if (!suppressedByCombat)
            UI.ToastOverlay.Draw(Toasts, Configuration.ToastPosition, Configuration.NotificationsEnabled);

        if (!ClientState.IsLoggedIn) return;

        var config = ConfigManager.Config;
        if (config != null)
        {
            var venue = PositionTracker.GetCurrentVenue(config);
            if (venue != null)
            {
                var floor = PositionTracker.GetCurrentFloor(venue);

                if (PictomancyMarkers.Available && PictomancyMarkers.Enabled)
                    PictomancyMarkers.DrawMarkers(floor, venue.Colors, Configuration.ServiceFilters);
            }
        }
    }

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
        ChangelogWindow.Dispose();
        OwnerSubmitWindow.Dispose();
        OwnerUpdateWindow.Dispose();
        OwnerVerifyWindow.Dispose();
        SetupWindow.Dispose();
        DebugWindow.Dispose();
        MapLoader.Dispose();
        GitHubPuller.Dispose();
        Lifestream.Dispose();
        PictomancyMarkers.Dispose();
        PartakeApi.Dispose();
        XivVenues.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }
}
