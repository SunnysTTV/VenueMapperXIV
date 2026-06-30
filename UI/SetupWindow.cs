using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace VenueMapper.UI;

public class SetupWindow : Window
{
    private readonly VenueMapperPlugin plugin;
    private int step;
    private int langIdx;
    private bool markers3d = true;

    public SetupWindow(VenueMapperPlugin plugin)
        : base("VenueMapper##Setup",
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoResize)
    {
        this.plugin = plugin;
        Size = new Vector2(480, 420);
        SizeCondition = ImGuiCond.Always;
        RespectCloseHotkey = false;
    }

    public override void PreDraw()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg, UIConstants.Background);
        ImGui.PushStyleColor(ImGuiCol.TitleBg, UIConstants.WithAlpha(UIConstants.Primary, 0.25f));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, UIConstants.WithAlpha(UIConstants.Primary, 0.35f));
        ImGui.PushStyleColor(ImGuiCol.Border, UIConstants.WithAlpha(UIConstants.Glow, 0.6f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 2f);

        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(vp.GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(4);
    }

    public override void Draw()
    {
        ImGui.PushTextWrapPos(0);

        if (ImGui.BeginChild("##setupContent", new Vector2(-1, -40)))
        {
            switch (step)
            {
                case 0: DrawWelcome(); break;
                case 1: DrawLanguage(); break;
                case 2: DrawFeatures(); break;
                case 3: DrawSettings(); break;
            }
            ImGui.EndChild();
        }

        ImGui.Separator();
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.WithAlpha(UIConstants.TextSecondary, 0.1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UIConstants.WithAlpha(UIConstants.TextSecondary, 0.2f));
        ImGui.PushStyleColor(ImGuiCol.Text, UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f));
        if (ImGui.Button(Lang.SetupSkip, new Vector2(50, 26)))
            IsOpen = false;
        ImGui.PopStyleColor(3);
        ImGui.SameLine();

        if (step > 0)
        {
            if (ImGui.Button(Lang.SetupBack, new Vector2(80, 26)))
                step--;
            ImGui.SameLine();
        }

        var rightX = ImGui.GetContentRegionAvail().X - 100;
        ImGui.Dummy(new Vector2(rightX, 0));
        ImGui.SameLine();

        if (step < 3)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.WithAlpha(UIConstants.Glow, 0.2f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UIConstants.WithAlpha(UIConstants.Glow, 0.4f));
            ImGui.PushStyleColor(ImGuiCol.Text, UIConstants.Glow);
            if (ImGui.Button(Lang.SetupNext, new Vector2(100, 26)))
                step++;
            ImGui.PopStyleColor(3);
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.WithAlpha(UIConstants.Primary, 0.3f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UIConstants.WithAlpha(UIConstants.Primary, 0.5f));
            ImGui.PushStyleColor(ImGuiCol.Text, UIConstants.Primary);
            if (ImGui.Button(Lang.SetupDone, new Vector2(100, 26)))
                Finish();
            ImGui.PopStyleColor(3);
        }

        ImGui.PopTextWrapPos();
    }

    private void DrawWelcome()
    {
        ImGui.Spacing();
        ImGui.TextColored(UIConstants.Primary, "VenueMapper");
        ImGui.SameLine(0, 6);
        ImGui.TextColored(UIConstants.Glow, ChangelogData.PluginVersion);
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.TextPrimary, Lang.SetupWelcomeTitle);
        ImGui.Spacing();
        ImGui.TextWrapped(Lang.SetupWelcomeDesc);

        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.Glow, 0.8f), Lang.SetupWhatYouGet);
        ImGui.Spacing();
        ImGui.BulletText(Lang.SetupFeature1);
        ImGui.BulletText(Lang.SetupFeature2);
        ImGui.BulletText(Lang.SetupFeature3);
        ImGui.BulletText(Lang.SetupFeature4);
        ImGui.BulletText(Lang.SetupFeature5);
        ImGui.BulletText(Lang.SetupFeature6);
    }

    private void DrawLanguage()
    {
        ImGui.Spacing();
        ImGui.TextColored(UIConstants.Glow, Lang.SetupChooseLang);
        ImGui.Spacing();
        ImGui.Spacing();

        var languages = new[] { "English", "Deutsch" };
        var codes = new[] { "EN", "DE" };

        for (var i = 0; i < languages.Length; i++)
        {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, UIConstants.WithAlpha(UIConstants.CardBackground, 0.8f));
            if (ImGui.RadioButton(languages[i], langIdx == i))
                langIdx = i;
            ImGui.PopStyleColor();
            ImGui.Spacing();
        }

        ImGui.Spacing();
        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f),
            langIdx == 1 ? Lang.SetupLangHintDe : Lang.SetupLangHintEn);
    }

    private void DrawFeatures()
    {
        ImGui.Spacing();
        ImGui.TextColored(UIConstants.Glow, Lang.SetupKeyFeatures);
        ImGui.Spacing();

        Feature(Lang.SetupFeatMap, Lang.SetupFeatMapDesc, UIConstants.Primary);
        Feature(Lang.SetupFeatDir, Lang.SetupFeatDirDesc, UIConstants.Glow);
        Feature(Lang.SetupFeatEvents, Lang.SetupFeatEventsDesc, UIConstants.Secondary);
        Feature(Lang.SetupFeat3D, Lang.SetupFeat3DDesc, new Vector4(0.2f, 1f, 0.5f, 1f));
        Feature(Lang.SetupFeatOwner, Lang.SetupFeatOwnerDesc, new Vector4(1f, 0.84f, 0f, 1f));
        Feature(Lang.SetupFeatUpdate, Lang.SetupFeatUpdateDesc, UIConstants.TextSecondary);
    }

    private static void Feature(string title, string desc, Vector4 col)
    {
        ImGui.TextColored(col, title);
        ImGui.SameLine(120);
        ImGui.TextWrapped(desc);
        ImGui.Spacing();
    }

    private void DrawSettings()
    {
        ImGui.Spacing();
        ImGui.TextColored(UIConstants.Glow, Lang.SetupQuickSettings);
        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.CheckMark, UIConstants.Glow);
        ImGui.Checkbox(Lang.SetupEnable3D, ref markers3d);
        ImGui.PopStyleColor();

        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f),
            Lang.SetupEnable3DDesc);

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f),
            Lang.SetupAllSet);
        ImGui.Spacing();
        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.4f),
            Lang.SetupCommand);
    }

    private void Finish()
    {
        var codes = new[] { "EN", "DE" };
        var lang = codes[langIdx];

        plugin.Configuration.HasSeenSetup = true;
        plugin.Configuration.Language = lang;
        plugin.Configuration.Save();

        Lang.Set(lang);
        ChangelogData.CurrentLanguage = lang;
        plugin.PictomancyMarkers.Enabled = markers3d;

        IsOpen = false;
        plugin.VenueMapWindow.IsOpen = true;
        plugin.VenueMapWindow.ShowDirectory();
    }

    public void Dispose() { }
}
