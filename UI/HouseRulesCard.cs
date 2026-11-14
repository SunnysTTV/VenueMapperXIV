using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using VenueMapper.Models;
using VenueMapper.Services;

namespace VenueMapper.UI;

public static class HouseRuleIcons
{
    public static readonly string[] Keys = ["info", "heart", "camera", "no", "glass", "music", "shirt", "coins", "clock", "chat", "shield", "star", "warning"];

    public static FontAwesomeIcon Icon(string? key) => key?.ToLowerInvariant() switch
    {
        "heart" => FontAwesomeIcon.Heart,
        "camera" => FontAwesomeIcon.Camera,
        "no" => FontAwesomeIcon.Ban,
        "glass" => FontAwesomeIcon.GlassMartiniAlt,
        "music" => FontAwesomeIcon.Music,
        "shirt" => FontAwesomeIcon.Tshirt,
        "coins" => FontAwesomeIcon.Coins,
        "clock" => FontAwesomeIcon.Clock,
        "chat" => FontAwesomeIcon.Comments,
        "shield" => FontAwesomeIcon.ShieldAlt,
        "star" => FontAwesomeIcon.Star,
        "warning" => FontAwesomeIcon.ExclamationTriangle,
        _ => FontAwesomeIcon.InfoCircle,
    };
}

public sealed class HouseRulesCard
{
    private const float Width = 300f;
    private const float Cut = 8f;
    private const double Duration = 14.0;
    private const float FadeIn = 0.2f;
    private const float FadeOut = 0.35f;
    private const float Margin = 16f;
    private const float TopOffset = 60f;

    private readonly VenueMapperPlugin plugin;
    private Venue? venue;
    private List<VenueRule> rules = new();
    private DateTime shownAt;
    private double hoveredFor;
    private bool closing;
    private DateTime closingAt;

    public HouseRulesCard(VenueMapperPlugin plugin)
    {
        this.plugin = plugin;
    }

    public float ReservedHeight { get; private set; }
    public ScreenRect LastRect { get; private set; }

    public static IReadOnlyList<VenueRule> RulesFor(Venue venue, string language)
        => language == "DE" && venue.RulesDe.Count > 0 ? venue.RulesDe : venue.Rules;

    public static string RulesKey(Venue venue, IReadOnlyList<VenueRule> rules)
    {
        var hash = 2166136261u;
        foreach (var ch in string.Join("|", rules.Select(r => $"{r.Icon}:{r.Text}")))
            hash = (hash ^ ch) * 16777619u;
        return $"{venue.VenueId}:{hash:x8}";
    }

    public void OnVenueEntered(Venue entered)
    {
        var cfg = plugin.Configuration;
        if (cfg.HouseRules == HouseRulesMode.Off || cfg.RulesMuted.Contains(entered.VenueId)) return;
        var list = RulesFor(entered, cfg.Language);
        if (list.Count == 0) return;
        var key = RulesKey(entered, list);
        if (cfg.HouseRules == HouseRulesMode.FirstVisit && cfg.RulesSeen.Contains(key)) return;
        cfg.RulesSeen.Add(key);
        cfg.Save();
        Show(entered);
    }

    public void ShowSample(Venue target)
    {
        var sample = new List<VenueRule>
        {
            new() { Icon = "heart", Text = "Be kind to staff and guests" },
            new() { Icon = "camera", Text = "Ask before taking screenshots of people" },
            new() { Icon = "no", Text = "No NSFW in public areas" },
            new() { Icon = "coins", Text = "Tips are welcome, never required" },
        };
        Show(target, sample);
    }

    public void Show(Venue target) => Show(target, RulesFor(target, plugin.Configuration.Language));

    private void Show(Venue target, IReadOnlyList<VenueRule> list)
    {
        if (list.Count == 0) return;
        venue = target;
        rules = list.Take(6).ToList();
        shownAt = DateTime.Now;
        hoveredFor = 0;
        closing = false;
    }

    private void Close()
    {
        if (closing) return;
        closing = true;
        closingAt = DateTime.Now;
    }

    private static uint U32(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c);

    public void Draw(ToastCorner corner)
    {
        ReservedHeight = 0f;
        if (venue == null) return;

        var elapsed = (DateTime.Now - shownAt).TotalSeconds - hoveredFor;
        if (!closing && elapsed > Duration) Close();
        var alpha = closing
            ? 1f - Math.Clamp((float)(DateTime.Now - closingAt).TotalSeconds / FadeOut, 0f, 1f)
            : Math.Clamp((float)(DateTime.Now - shownAt).TotalSeconds / FadeIn, 0f, 1f);
        if (closing && alpha <= 0.01f)
        {
            venue = null;
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var width = Width * scale;
        var lineH = ImGui.GetTextLineHeight();
        var pad = new Vector2(12f, 10f) * scale;
        var iconW = 22f * scale;
        var textW = width - pad.X * 2f - iconW - 4f;
        var wrapped = rules.Select(r => UIConstants.WrapText(r.Text, textW)).ToList();
        var title = Lang.HouseRulesTitle(UIConstants.DisplayName(venue.Name));
        var titleLines = UIConstants.WrapText(title, width - pad.X * 2f - 30f * scale);
        var height = pad.Y * 2f + titleLines.Count * lineH + lineH + 8f + wrapped.Sum(w => w.Count * lineH + 4f) + 8f + lineH + 8f;

        var vp = ImGui.GetMainViewport();
        var isTop = corner is ToastCorner.TopLeft or ToastCorner.TopRight;
        var isRight = corner is ToastCorner.TopRight or ToastCorner.BottomRight;
        var slide = (1f - alpha) * 24f;
        var x = isRight ? vp.WorkPos.X + vp.WorkSize.X - Margin - width + slide : vp.WorkPos.X + Margin - slide;
        var y = isTop ? vp.WorkPos.Y + TopOffset : vp.WorkPos.Y + vp.WorkSize.Y - Margin - height;
        ReservedHeight = height + 8f;

        LastRect = new ScreenRect(x, y, width, height + 3f);
        ImGui.SetNextWindowPos(new Vector2(x, y));
        ImGui.SetNextWindowSize(new Vector2(width, height));
        using var bg = ImRaii.PushColor(ImGuiCol.WindowBg, Vector4.Zero).Push(ImGuiCol.Border, Vector4.Zero);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero).Push(ImGuiStyleVar.WindowBorderSize, 0f)
            .Push(ImGuiStyleVar.Alpha, alpha);
        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings
                                       | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoDocking;
        var open = ImGui.Begin("##VenueMapperHouseRules", flags);
        try
        {
            ImGuiP.BringWindowToDisplayFront(ImGuiP.GetCurrentWindow());
            if (open)
                DrawCard(x, y, width, height, lineH, pad, iconW, titleLines, wrapped, elapsed, alpha, scale);
        }
        finally
        {
            ImGui.End();
        }
    }

    private void DrawCard(float x, float y, float width, float height, float lineH, Vector2 pad, float iconW,
        List<string> titleLines, List<List<string>> wrapped, double elapsed, float alpha, float scale)
    {
        if (venue == null) return;
        var dl = ImGui.GetWindowDrawList();
        var min = new Vector2(x, y);
        var max = min + new Vector2(width, height);
        var primary = venue.Colors != null ? UIConstants.Readable(venue.Colors.PrimaryVec) : UIConstants.Accent;
        var secondary = venue.Colors != null ? UIConstants.Readable(venue.Colors.SecondaryVec) : UIConstants.Accent;
        if (UIConstants.OverrideMode != ColorOverrideMode.None) primary = secondary = UIConstants.Accent;

        UIConstants.Chamfer(dl, min + new Vector2(0f, 3f), max + new Vector2(0f, 3f), Cut);
        dl.PathFillConvex(U32(new Vector4(0f, 0f, 0f, 0.3f * alpha)));
        UIConstants.Chamfer(dl, min, max, Cut);
        dl.PathFillConvex(U32(UIConstants.WithAlpha(UIConstants.Background, 0.95f * alpha)));
        UIConstants.Chamfer(dl, min, max, Cut);
        dl.PathStroke(U32(UIConstants.WithAlpha(UIConstants.TextPrimary, 0.12f * alpha)), ImDrawFlags.Closed, 1f);
        dl.AddRectFilledMultiColor(new Vector2(min.X, min.Y + Cut), new Vector2(min.X + 3f, max.Y),
            U32(UIConstants.WithAlpha(primary, alpha)), U32(UIConstants.WithAlpha(primary, alpha)),
            U32(UIConstants.WithAlpha(secondary, alpha)), U32(UIConstants.WithAlpha(secondary, alpha)));

        var hovered = ImGui.IsWindowHovered();
        if (hovered) hoveredFor += ImGui.GetIO().DeltaTime;

        var cursor = min + pad;
        var circleR = 11f * scale;
        var circleCenter = new Vector2(cursor.X + circleR, cursor.Y + circleR - 2f);
        dl.AddCircleFilled(circleCenter, circleR, U32(UIConstants.WithAlpha(primary, 0.2f * alpha)), 24);
        DrawIcon(dl, FontAwesomeIcon.BookOpen, circleCenter, 0.75f, UIConstants.WithAlpha(primary, alpha));
        var titleX = cursor.X + circleR * 2f + 8f;
        foreach (var line in titleLines)
        {
            dl.AddText(new Vector2(titleX, cursor.Y), U32(UIConstants.WithAlpha(UIConstants.TextPrimary, alpha)), line);
            cursor.Y += lineH;
        }
        dl.AddText(new Vector2(titleX, cursor.Y), U32(UIConstants.WithAlpha(UIConstants.TextSecondary, alpha)), Lang.HouseRulesSub);
        cursor.Y += lineH + 8f;

        for (var i = 0; i < rules.Count; i++)
        {
            DrawIcon(dl, HouseRuleIcons.Icon(rules[i].Icon), new Vector2(cursor.X + iconW / 2f, cursor.Y + lineH / 2f), 0.8f,
                UIConstants.WithAlpha(secondary, alpha));
            foreach (var line in wrapped[i])
            {
                dl.AddText(new Vector2(cursor.X + iconW + 4f, cursor.Y), U32(UIConstants.WithAlpha(UIConstants.TextPrimary, alpha)), line);
                cursor.Y += lineH;
            }
            cursor.Y += 4f;
        }
        cursor.Y += 4f;

        var footerY = cursor.Y;
        var muteLabel = Lang.HouseRulesMute;
        var muteSize = ImGui.CalcTextSize(muteLabel);
        ImGui.SetCursorScreenPos(new Vector2(cursor.X, footerY));
        if (ImGui.InvisibleButton("##rulesMute", muteSize + new Vector2(0f, 4f)))
        {
            plugin.Configuration.RulesMuted.Add(venue.VenueId);
            plugin.Configuration.Save();
            Close();
        }
        var muteHot = ImGui.IsItemHovered();
        dl.AddText(new Vector2(cursor.X, footerY + 2f), U32(UIConstants.WithAlpha(muteHot ? UIConstants.TextSecondary : UIConstants.TextMuted, alpha)), muteLabel);

        var gotLabel = Lang.HouseRulesGotIt;
        var gotSize = ImGui.CalcTextSize(gotLabel) + new Vector2(22f, 6f);
        var gotMin = new Vector2(max.X - pad.X - gotSize.X, footerY - 1f);
        ImGui.SetCursorScreenPos(gotMin);
        if (ImGui.InvisibleButton("##rulesGotIt", gotSize))
            Close();
        var gotHot = ImGui.IsItemHovered();
        dl.AddRectFilled(gotMin, gotMin + gotSize, U32(UIConstants.WithAlpha(UIConstants.Accent, (gotHot ? 0.25f : 0.1f) * alpha)), 3f);
        dl.AddRect(gotMin, gotMin + gotSize, U32(UIConstants.WithAlpha(UIConstants.Accent, 0.55f * alpha)), 3f, ImDrawFlags.None, 1f);
        dl.AddText(gotMin + new Vector2(11f, 3f), U32(UIConstants.WithAlpha(UIConstants.Accent, alpha)), gotLabel);

        var remaining = (float)Math.Clamp(1.0 - elapsed / Duration, 0.0, 1.0);
        dl.AddLine(new Vector2(min.X + 3f, max.Y - 1f), new Vector2(min.X + 3f + (width - 3f - Cut) * remaining, max.Y - 1f),
            U32(UIConstants.WithAlpha(primary, 0.7f * alpha)), 2f);
    }

    private static void DrawIcon(ImDrawListPtr dl, FontAwesomeIcon icon, Vector2 center, float scale, Vector4 color)
    {
        UIConstants.DrawIconCentered(dl, UiBuilder.IconFont.FontSize * scale, center, icon, U32(color));
    }
}
