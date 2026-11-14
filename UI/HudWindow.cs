using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace VenueMapper.UI;

public abstract class HudWindow : Window
{
    protected const float FrameCut = 12f;
    protected const float TitleBarH = 30f;
    private const float ToolSize = 24f;

    protected readonly VenueMapperPlugin Plugin;
    private readonly string windowId;
    private readonly bool resizable;
    private double hackerModeStart = -1;
    private double hackerTitleLoopStart = -1;

    protected HudWindow(VenueMapperPlugin plugin, string windowId, Vector2 size, bool resizable = false)
        : base($"###{windowId}", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking
                                 | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse
                                 | (resizable ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoResize))
    {
        Plugin = plugin;
        this.windowId = windowId;
        this.resizable = resizable;
        Size = size;
        SizeCondition = resizable ? ImGuiCond.FirstUseEver : ImGuiCond.Always;
        if (resizable)
            SizeConstraints = new WindowSizeConstraints { MinimumSize = size };
    }

    protected abstract string Title { get; }
    protected abstract void DrawBody();

    private static uint U32(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c);

    public override void PreDraw()
    {
        WindowName = $"{Title}###{windowId}";
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.FirstUseEver, new Vector2(0.5f, 0.5f));
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ResizeGrip, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ResizeGripHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ResizeGripActive, Vector4.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 8));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(4);
    }

    public override void Draw()
    {
        try
        {
            var min = ImGui.GetWindowPos();
            UIConstants.DrawHudFrame(ImGui.GetWindowDrawList(), min, min + ImGui.GetWindowSize(), FrameCut,
                ImGui.GetStyle().Alpha, Plugin.EasterEggManager.IsRgbOverloadActive, resizable);
            DrawTitleBar();
            using (ImRaii.Disabled(UIConstants.IsHackerBooting))
                DrawBody();
            HackerModeOverlay.Draw(ref hackerModeStart, ref hackerTitleLoopStart, Title, TitleBarH, FrameCut, 34f, 8f);
        }
        catch (Exception ex)
        {
            VenueMapperPlugin.Log.Error(ex, $"[VenueMapper] {GetType().Name} draw failed");
        }
    }

    private void DrawTitleBar()
    {
        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var avail = ImGui.GetContentRegionAvail().X;
        var lineH = ImGui.GetTextLineHeight();
        var a = ImGui.GetStyle().Alpha;
        var dragW = Math.Max(avail - ToolSize - 8f, 40f);

        ImGui.Dummy(new Vector2(dragW, TitleBarH));
        HudIcons.Crystal(dl, new Vector2(start.X + 6f, start.Y + TitleBarH / 2f), lineH * 1.1f, U32(UIConstants.WithAlpha(UIConstants.Glow, a)));
        var title = UIConstants.FitText(Title, dragW - 24f);
        if (UIConstants.OverrideMode != ColorOverrideMode.Hacker)
            dl.AddText(new Vector2(start.X + 20f, start.Y + (TitleBarH - lineH) / 2f), U32(UIConstants.WithAlpha(UIConstants.Heading, a)), title);

        ImGui.SetCursorScreenPos(new Vector2(start.X + avail - ToolSize, start.Y + (TitleBarH - ToolSize) / 2f));
        if (TitleTool($"##{windowId}_close", FontAwesomeIcon.Times, Lang.Close))
            IsOpen = false;

        var winPos = ImGui.GetWindowPos();
        UIConstants.DrawTrimRule(dl, winPos.X + 14f, winPos.X + ImGui.GetWindowSize().X - 14f, start.Y + TitleBarH + 2f);
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + TitleBarH));
        ImGui.Dummy(new Vector2(0, 8f));
    }

    protected static bool TitleTool(string id, FontAwesomeIcon icon, string? tooltip)
    {
        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(ToolSize, ToolSize);
        var clicked = ImGui.InvisibleButton(id, size);
        var hovered = ImGui.IsItemHovered();
        var dl = ImGui.GetWindowDrawList();
        if (hovered)
            dl.AddRectFilled(pos, pos + size, U32(UIConstants.CardBackground), 3f);
        UIConstants.DrawIconCentered(dl, UiBuilder.IconFont.FontSize, pos + size / 2f, icon,
            U32(hovered ? UIConstants.TextPrimary : UIConstants.TextSecondary));
        if (hovered && tooltip != null)
            ImGui.SetTooltip(tooltip);
        return clicked;
    }
}
