using System;
using System.IO;
using Dalamud.Game.Command;
using Dalamud.Interface.Textures;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
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

    public Configuration Configuration { get; }
    public ConfigManager ConfigManager { get; }
    public GitHubConfigPuller GitHubPuller { get; }
    public PlayerPositionTracker PositionTracker { get; }
    public HousingMapLoader MapLoader { get; }
    public LifestreamService Lifestream { get; }
    public PictomancyMarkerManager PictomancyMarkers { get; }
    public PartakeApiService PartakeApi { get; }
    public XivVenuesService XivVenues { get; }

    public VenueMapWindow VenueMapWindow { get; }
    public SettingsWindow SettingsWindow { get; }
    public ChangelogWindow ChangelogWindow { get; }
    public OwnerSubmitWindow OwnerSubmitWindow { get; }
    public SetupWindow SetupWindow { get; }
    public DebugWindow DebugWindow { get; }
    public Dalamud.Interface.Windowing.WindowSystem WindowSystem { get; } = new("VenueMapper");

    public VenueMapperPlugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (string.IsNullOrWhiteSpace(Configuration.GitHubConfigUrl))
        {
            Configuration.GitHubConfigUrl = "https://raw.githubusercontent.com/SunnysTTV/VenueMapperXIV/main/Resources/venues.json";
            Configuration.Save();
        }
        UI.Lang.Set(Configuration.Language);
        UI.ChangelogData.CurrentLanguage = Configuration.Language;

        ConfigManager = new ConfigManager(Log, PluginInterface.ConfigDirectory.FullName);
        GitHubPuller = new GitHubConfigPuller(Log, ConfigManager);
        PositionTracker = new PlayerPositionTracker(ClientState, ObjectTable, Log);
        MapLoader   = new HousingMapLoader(DataManager, TextureProvider, Log);
        Lifestream  = new LifestreamService(PluginInterface, Log);
        PictomancyMarkers = new PictomancyMarkerManager(PluginInterface, Log);
        PartakeApi   = new PartakeApiService(Log);
        XivVenues    = new XivVenuesService(Log);

        var bundledResourcePath = Path.Combine(
            Path.GetDirectoryName(PluginInterface.AssemblyLocation.FullName) ?? string.Empty,
            "Resources", "venues.json");

        ConfigManager.Load(bundledResourcePath);

        VenueMapWindow = new VenueMapWindow(this);
        SettingsWindow = new SettingsWindow(this);
        ChangelogWindow = new ChangelogWindow();
        OwnerSubmitWindow = new OwnerSubmitWindow(this);
        SetupWindow = new SetupWindow(this);
        DebugWindow = new DebugWindow(PositionTracker, this);

        WindowSystem.AddWindow(VenueMapWindow);
        WindowSystem.AddWindow(SettingsWindow);
        WindowSystem.AddWindow(ChangelogWindow);
        WindowSystem.AddWindow(OwnerSubmitWindow);
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
            _ = GitHubPuller.PullAsync(Configuration.GitHubConfigUrl);
        }
    }

    private bool wasInVenue;
    private bool setupShownThisSession;
    private DateTime lastAutoCheck = DateTime.MinValue;

    private void OnOpenMainUi() => VenueMapWindow.IsOpen = true;
    private void OnOpenConfigUi() => SettingsWindow.IsOpen = true;

    private DateTime lastFrameworkError = DateTime.MinValue;

    private void OnFrameworkUpdate(IFramework framework)
    {
        try
        {
            if (!ClientState.IsLoggedIn)
                return;

            if (!Configuration.HasSeenSetup && !setupShownThisSession)
            {
                setupShownThisSession = true;
                SetupWindow.IsOpen = true;
            }

            if ((DateTime.Now - lastAutoCheck).TotalHours >= 1 && !string.IsNullOrWhiteSpace(Configuration.GitHubConfigUrl))
            {
                lastAutoCheck = DateTime.Now;
                _ = AutoPullConfigAsync();
            }

            PositionTracker.Update(ConfigManager.Config);

            var config = ConfigManager.Config;
            var isInVenue = config != null && PositionTracker.GetCurrentVenue(config) != null;

            if (isInVenue && !wasInVenue)
            {
                VenueMapWindow.IsOpen = true;
                VenueMapWindow.HideDirectory();
            }
            else if (!isInVenue && wasInVenue)
            {
                VenueMapWindow.ShowDirectory();
            }

            wasInVenue = isInVenue;
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
            _ = GitHubPuller.PullAsync(Configuration.GitHubConfigUrl, force: true);
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
            var updated = await GitHubPuller.PullAsync(Configuration.GitHubConfigUrl);
            if (updated)
                Log.Information("[VenueMapper] Auto-pull: config updated from GitHub");
        }
        catch (Exception ex)
        {
            Log.Debug($"[VenueMapper] Auto-pull failed: {ex.Message}");
        }
    }

    private void DrawUI()
    {
        WindowSystem.Draw();

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

        WindowSystem.RemoveAllWindows();
        VenueMapWindow.Dispose();
        SettingsWindow.Dispose();
        ChangelogWindow.Dispose();
        OwnerSubmitWindow.Dispose();
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
