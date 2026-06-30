using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace VenueMapper.UI;

public class SettingsWindow : Window, IDisposable
{
    private readonly VenueMapperPlugin plugin;
    private bool isPulling;
    private float aboutHeaderAlpha;

    public SettingsWindow(VenueMapperPlugin plugin)
        : base("VenueMapper Settings##VenueMapperSettings", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize)
    {
        this.plugin = plugin;
        Size = new Vector2(420, 220);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw() => DrawSettingsTab();

    public void DrawSettingsTab()
    {
        var config = plugin.Configuration;

        ImGui.TextColored(UIConstants.Primary, Lang.Settings.ToUpperInvariant());
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.TextSecondary, Lang.Language);
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.FrameBg, UIConstants.CardBackground);
        ImGui.PushStyleColor(ImGuiCol.Border, UIConstants.GlowDim);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.SetNextItemWidth(120);
        var langLabel = config.Language == "DE" ? Lang.LangGerman : Lang.LangEnglish;
        if (ImGui.BeginCombo("##lang", langLabel))
        {
            if (ImGui.Selectable(Lang.LangEnglish, config.Language == "EN"))
            { config.Language = "EN"; Lang.Set("EN"); ChangelogData.CurrentLanguage = "EN"; config.Save(); plugin.VenueMapWindow.ShowSettings(); }
            if (ImGui.Selectable(Lang.LangGerman, config.Language == "DE"))
            { config.Language = "DE"; Lang.Set("DE"); ChangelogData.CurrentLanguage = "DE"; config.Save(); plugin.VenueMapWindow.ShowSettings(); }
            ImGui.EndCombo();
        }
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(2);

        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.CheckMark, UIConstants.Glow);
        var markers = plugin.PictomancyMarkers.Enabled;
        if (ImGui.Checkbox(Lang.Markers3D, ref markers))
            plugin.PictomancyMarkers.Enabled = markers;
        ImGui.PopStyleColor();

        ImGui.PushStyleColor(ImGuiCol.CheckMark, UIConstants.Primary);
        var autoPull = config.AutoPullOnStartup;
        if (ImGui.Checkbox(Lang.AutoPullCfg, ref autoPull))
        { config.AutoPullOnStartup = autoPull; config.Save(); }
        ImGui.PopStyleColor();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.TextSecondary, Lang.GithubConfig);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, UIConstants.WithAlpha(UIConstants.CardBackground, 0.5f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        var url = config.GitHubConfigUrl;
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##ghUrl", ref url, 256, ImGuiInputTextFlags.ReadOnly);
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();

        ImGui.Spacing();

        DrawAccentButton(isPulling ? "..." : Lang.PullNow, () =>
        { isPulling = true; _ = PullAsync(); },
        disabled: isPulling || string.IsNullOrWhiteSpace(config.GitHubConfigUrl));
        ImGui.SameLine();
        DrawAccentButton(Lang.ResetCache, () =>
        {
            try
            {
                if (System.IO.File.Exists(plugin.ConfigManager.CacheFilePath))
                    System.IO.File.Delete(plugin.ConfigManager.CacheFilePath);
                var etagPath = System.IO.Path.Combine(plugin.ConfigManager.ConfigDirectory, "venues.etag");
                if (System.IO.File.Exists(etagPath)) System.IO.File.Delete(etagPath);
                var bundled = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(VenueMapperPlugin.PluginInterface.AssemblyLocation.FullName) ?? "",
                    "Resources", "venues.json");
                plugin.ConfigManager.Load(bundled);
            }
            catch (Exception ex) { VenueMapperPlugin.Log.Error(ex, "Reset cache failed"); }
        });
    }

    public void DrawAboutTab()
    {
        ImGui.PushTextWrapPos(0);

        aboutHeaderAlpha = MathF.Min(aboutHeaderAlpha + ImGui.GetIO().DeltaTime * 3f, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, aboutHeaderAlpha);

        ImGui.TextColored(UIConstants.Primary, "VenueMapper");
        ImGui.SameLine(0, 6);
        ImGui.TextColored(UIConstants.Glow, ChangelogData.PluginVersion);
        ImGui.TextWrapped(Lang.PluginDesc);
        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f), "by SunnysOfficial");

        ImGui.PopStyleVar();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var btnW = (ImGui.GetContentRegionAvail().X - 4) / 2f;

        LinkBtn("Support Discord", "https://discord.com/invite/agKWEzK5nR",
            new Vector4(0.34f, 0.40f, 0.93f, 1f), btnW, "Join Discord");
        ImGui.SameLine(0, 4);
        LinkBtn("GitHub", "https://github.com/sunnysofficial/VenueMapper",
            new Vector4(0.6f, 0.6f, 0.6f, 1f), btnW, "Source code");
        LinkBtn("Support on Ko-Fi", "https://ko-fi.com/sunnysofficial",
            new Vector4(1f, 0.4f, 0.4f, 1f), -1, "Support development");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f),
            "Developer: SunnysOfficial");
        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.35f),
            "Dalamud  |  Lumina  |  Lifestream IPC  |  Pictomancy  |  Partake.gg API  |  FFXIVVenues API");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.45f),
            Lang.WantVenue);
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.WithAlpha(UIConstants.Primary, 0.2f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UIConstants.WithAlpha(UIConstants.Primary, 0.4f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, UIConstants.WithAlpha(UIConstants.Primary, 0.6f));
        ImGui.PushStyleColor(ImGuiCol.Text, UIConstants.Primary);
        if (ImGui.Button(Lang.SubmitVenue, new Vector2(-1, 26)))
            plugin.OwnerSubmitWindow.IsOpen = true;
        ImGui.PopStyleColor(4);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.Primary, $"{ChangelogData.PluginVersion} - {Lang.CurRelease}");
        if (ChangelogData.Versions.Length > 0)
        {
            ImGui.SameLine(0, 6);
            ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f), ChangelogData.Versions[0].Date);
        }
        ImGui.Spacing();

        ImGui.PopTextWrapPos();

        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 0f);
        if (ImGui.BeginChild("##changelogScroll", new Vector2(-1, -1)))
        {
            ImGui.PushTextWrapPos(0);
            if (ChangelogData.Changelogs.TryGetValue(ChangelogData.PluginVersion, out var sections))
                UIConstants.DrawChangelog(sections);
            ImGui.PopTextWrapPos();
        }
        ImGui.EndChild();
        ImGui.PopStyleVar();
    }


    private static void LinkBtn(string label, string url, Vector4 col, float w, string tooltip)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.WithAlpha(col, 0.15f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UIConstants.WithAlpha(col, 0.3f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, UIConstants.WithAlpha(col, 0.5f));
        ImGui.PushStyleColor(ImGuiCol.Text, col);
        if (ImGui.Button($"{label}##{url}", new Vector2(w, 26)))
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                { FileName = url, UseShellExecute = true }); } catch { }
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);
        ImGui.PopStyleColor(4);
    }

    private async System.Threading.Tasks.Task PullAsync()
    {
        try { await plugin.GitHubPuller.PullAsync(plugin.Configuration.GitHubConfigUrl, force: true); }
        finally { isPulling = false; }
    }

    private static void DrawAccentButton(string label, Action onClick, bool disabled = false)
    {
        if (disabled) ImGui.BeginDisabled();
        ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.Primary);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UIConstants.PrimaryHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, UIConstants.Primary);
        ImGui.PushStyleColor(ImGuiCol.Text, UIConstants.TextPrimary);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        if (ImGui.Button(label, new Vector2(120, 26))) onClick();
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(4);
        if (disabled) ImGui.EndDisabled();
    }

    public void Dispose() { }
}
