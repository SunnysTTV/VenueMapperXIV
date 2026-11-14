using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using VenueMapper.Services;

namespace VenueMapper.UI;

public partial class SettingsWindow
{
    private const float SidebarMinWidth = 540f;
    private const float SidebarWidth = 168f;
    private const float RowGap = 7f;
    private const float SubIndent = 18f;

    private static readonly Vector4 MarkersColor = UIConstants.HexToVec4("#B49CFF");
    private static readonly Vector4 DataColor = UIConstants.HexToVec4("#7AA7FF");
    private static readonly float[] SignRanges = [60f, 100f, 150f, Configuration.WholeWardRange];

    private int selectedGroup;
    private readonly HashSet<int> expandedGroups = new();
    private int lastSettingsFrame = -10;
    private bool firstRow;
    private float? rowWidth;

    private sealed record SettingsGroup(string Title, string Subtitle, FontAwesomeIcon Icon, Vector4 Color, Action<Configuration, bool> Draw);

    private SettingsGroup[] Groups() =>
    [
        new(Lang.SetGeneral, Lang.SetGeneralSub, FontAwesomeIcon.Cog, UIConstants.Accent, DrawGeneralGroup),
        new(Lang.Directory, Lang.SetDirectorySub, FontAwesomeIcon.List, UIConstants.Success, DrawDirectoryGroup),
        new(Lang.MarkersAndMap, Lang.SetMarkersSub, FontAwesomeIcon.MapMarkerAlt, MarkersColor, DrawMarkersGroup),
        new(Lang.Notifications, Lang.SetNotificationsSub, FontAwesomeIcon.Bell, UIConstants.Warning, DrawNotificationsGroup),
        new(Lang.AccessibilitySettings, Lang.SetAccessibilitySub, FontAwesomeIcon.UniversalAccess, UIConstants.Rose, DrawAccessibilityGroup),
        new(Lang.SetData, Lang.SetDataSub, FontAwesomeIcon.Sync, DataColor, DrawDataGroup),
    ];

    private static uint U32(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c);

    public void SelectGroup(int index) => selectedGroup = index;

    public void DrawSettingsTab()
    {
        var config = plugin.Configuration;
        var myHash = OwnerIdHelper.ComputeHash(VenueMapperPlugin.PlayerState.ContentId);
        var ownsAnyVenue = plugin.ConfigManager.Config?.Venues.Any(v => v.OwnerIdHashes.Contains(myHash)) ?? false;
        var groups = Groups();
        var frame = ImGui.GetFrameCount();
        if (frame - lastSettingsFrame > 1)
            expandedGroups.Clear();
        lastSettingsFrame = frame;

        using var scrollbar = UIConstants.ScrollbarStyle();
        using var childBg = ImRaii.PushColor(ImGuiCol.ChildBg, Vector4.Zero);
        if (ImGui.GetContentRegionAvail().X >= SidebarMinWidth * ImGuiHelpers.GlobalScale)
            DrawSidebarLayout(groups, config, ownsAnyVenue);
        else
            DrawCardLayout(groups, config, ownsAnyVenue);
    }

    private void DrawSidebarLayout(SettingsGroup[] groups, Configuration config, bool ownsAnyVenue)
    {
        selectedGroup = Math.Clamp(selectedGroup, 0, groups.Length - 1);
        var height = ImGui.GetContentRegionAvail().Y;

        using (var side = ImRaii.Child("##settingsSide", new Vector2(SidebarWidth * ImGuiHelpers.GlobalScale, height)))
        {
            if (side)
            {
                for (var i = 0; i < groups.Length; i++)
                {
                    if (NavItem(groups[i], i == selectedGroup))
                        selectedGroup = i;
                }
            }
        }

        var dl = ImGui.GetWindowDrawList();
        var lineX = ImGui.GetItemRectMax().X + 4f;
        dl.AddLine(new Vector2(lineX, ImGui.GetItemRectMin().Y), new Vector2(lineX, ImGui.GetItemRectMax().Y), U32(UIConstants.Line), 1f);

        ImGui.SameLine(0, 12f);
        using var content = ImRaii.Child("##settingsContent", new Vector2(-1, height));
        if (!content) return;

        var group = groups[selectedGroup];
        GroupHeader(group, null);
        ImGui.Dummy(new Vector2(0, 4f));
        firstRow = true;
        using (ImRaii.PushId(selectedGroup))
            group.Draw(config, ownsAnyVenue);
        ImGui.Dummy(new Vector2(0, 8f));
    }

    private static bool NavItem(SettingsGroup group, bool active)
    {
        var pos = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var lineH = ImGui.GetTextLineHeight();
        var height = lineH + 14f;
        var clicked = ImGui.InvisibleButton($"##nav_{group.Title}", new Vector2(width, height));
        var hovered = ImGui.IsItemHovered();
        var dl = ImGui.GetWindowDrawList();
        var max = pos + new Vector2(width, height);

        if (active || hovered)
            dl.AddRectFilled(pos, max, U32(active ? UIConstants.Hover : UIConstants.WithAlpha(UIConstants.Hover, 0.6f)), 3f);
        if (active)
            dl.AddRectFilled(pos + new Vector2(0f, 4f), new Vector2(pos.X + 2f, max.Y - 4f), U32(UIConstants.Accent));

        var boxSize = height - 10f;
        var boxMin = new Vector2(pos.X + 8f, pos.Y + 5f);
        IconBox(dl, boxMin, boxSize, group.Icon, group.Color);
        dl.AddText(new Vector2(boxMin.X + boxSize + 8f, pos.Y + (height - lineH) / 2f),
            U32(active || hovered ? UIConstants.TextPrimary : UIConstants.TextSecondary), group.Title);
        return clicked;
    }

    private static void IconBox(ImDrawListPtr dl, Vector2 min, float size, FontAwesomeIcon icon, Vector4 color)
    {
        dl.AddRectFilled(min, min + new Vector2(size), U32(UIConstants.WithAlpha(color, 0.16f)), 4f);
        UIConstants.DrawIconCentered(dl, size * 0.58f, min + new Vector2(size) / 2f, icon, U32(color));
    }

    private bool GroupHeader(SettingsGroup group, bool? expanded)
    {
        var pos = ImGui.GetCursorScreenPos();
        var width = rowWidth ?? ImGui.GetContentRegionAvail().X;
        var lineH = ImGui.GetTextLineHeight();
        var height = lineH * 2f + 6f;
        var clicked = ImGui.InvisibleButton($"##grp_{group.Title}", new Vector2(width, height));
        var hovered = expanded.HasValue && ImGui.IsItemHovered();
        var dl = ImGui.GetWindowDrawList();

        IconBox(dl, pos, height, group.Icon, group.Color);
        var textX = pos.X + height + 10f;
        dl.AddText(new Vector2(textX, pos.Y + 1f), U32(UIConstants.Heading), group.Title.ToUpperInvariant());
        var sub = UIConstants.FitText(group.Subtitle, width - (textX - pos.X) - 24f);
        dl.AddText(new Vector2(textX, pos.Y + lineH + 4f), U32(UIConstants.TextMuted), sub);

        if (expanded is { } open)
        {
            var chevron = (open ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronRight).ToIconString();
            var iconFont = UiBuilder.IconFont;
            var size = iconFont.FontSize * 0.7f;
            dl.AddText(iconFont, size, new Vector2(pos.X + width - size - 6f, pos.Y + (height - size) / 2f),
                U32(hovered ? UIConstants.Accent : UIConstants.TextMuted), chevron);
        }
        return clicked;
    }

    private void DrawCardLayout(SettingsGroup[] groups, Configuration config, bool ownsAnyVenue)
    {
        using var child = ImRaii.Child("##settingsCards", new Vector2(-1, -1));
        if (!child) return;

        for (var i = 0; i < groups.Length; i++)
        {
            var group = groups[i];
            var expanded = expandedGroups.Contains(i);
            var dl = ImGui.GetWindowDrawList();
            var start = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X - 6f;

            dl.ChannelsSplit(2);
            dl.ChannelsSetCurrent(1);
            ImGui.SetCursorScreenPos(start + new Vector2(12f, 10f));
            rowWidth = width - 24f;
            using (ImRaii.PushId(i))
            {
                using (ImRaii.Group())
                {
                    ImGui.Dummy(new Vector2(width - 24f, 0f));
                    if (GroupHeader(group, expanded))
                    {
                        if (!expandedGroups.Remove(i))
                            expandedGroups.Add(i);
                    }

                    if (expanded)
                    {
                        ImGui.Dummy(new Vector2(0, 2f));
                        firstRow = true;
                        group.Draw(config, ownsAnyVenue);
                    }
                }
            }
            rowWidth = null;
            var bottom = ImGui.GetItemRectMax().Y + 10f;
            var end = new Vector2(start.X + width, bottom);

            dl.ChannelsSetCurrent(0);
            dl.AddRectFilled(start, end, U32(UIConstants.PanelRaised), 3f);
            dl.AddRect(start, end, U32(UIConstants.LineStrong), 3f, ImDrawFlags.None, 1f);
            dl.AddRectFilled(new Vector2(start.X, start.Y + 3f), new Vector2(start.X + 3f, end.Y - 3f), U32(group.Color));
            dl.ChannelsMerge();

            ImGui.SetCursorScreenPos(new Vector2(start.X, end.Y + 8f));
            ImGui.Dummy(Vector2.Zero);
        }
    }

    private void Row(string label, string? description, float controlWidth, Action control, bool sub = false)
    {
        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var avail = rowWidth ?? ImGui.GetContentRegionAvail().X;
        var indent = sub ? SubIndent : 0f;
        var controlW = controlWidth * ImGuiHelpers.GlobalScale;

        if (!firstRow)
            dl.AddLine(new Vector2(start.X + indent, start.Y), new Vector2(start.X + avail, start.Y), U32(UIConstants.Line), 1f);
        firstRow = false;

        var textTop = start.Y + RowGap;
        var leftW = Math.Max(avail - indent - controlW - 14f, 60f);
        ImGui.SetCursorScreenPos(new Vector2(start.X + indent, textTop));
        using (ImRaii.Group())
        using (ImRaii.TextWrapPos(ImGui.GetCursorPosX() + leftW))
        {
            ImGui.TextColored(UIConstants.TextPrimary, label);
            if (!string.IsNullOrEmpty(description))
                ImGui.TextColored(UIConstants.TextMuted, description);
        }
        var textBottom = ImGui.GetItemRectMax().Y;

        var frameH = ImGui.GetFrameHeight();
        var controlTop = textTop + MathF.Max(0f, (textBottom - textTop - frameH) / 2f);
        ImGui.SetCursorScreenPos(new Vector2(start.X + avail - controlW, controlTop));
        control();
        var controlBottom = ImGui.GetItemRectMax().Y;

        ImGui.SetCursorScreenPos(new Vector2(start.X, MathF.Max(textBottom, controlBottom) + RowGap));
        ImGui.Dummy(Vector2.Zero);
    }

    private static float ToggleControlWidth => UIConstants.ToggleWidth() / ImGuiHelpers.GlobalScale;

    private void ToggleSetting(string id, string label, string? description, bool value, Action<bool> set, bool sub = false, bool disabled = false)
    {
        Row(label, description, ToggleControlWidth, () =>
        {
            using (ImRaii.Disabled(disabled))
            {
                var current = value;
                if (UIConstants.Toggle(id, ref current))
                    set(current);
            }
        }, sub);
    }

    private void ComboSetting(string id, string label, string? description, string preview, float width, Action items, bool sub = false)
        => Row(label, description, width, () => UIConstants.StyledCombo(id, preview, width * ImGuiHelpers.GlobalScale, items), sub);

    private static float SegmentWidth(string[] options)
        => options.Sum(o => ImGui.CalcTextSize(o).X + 18f) / ImGuiHelpers.GlobalScale;

    private void SegmentSetting(string id, string label, string? description, string[] options, int index, Action<int> set, bool sub = false)
    {
        Row(label, description, SegmentWidth(options), () =>
        {
            var changed = Segmented(id, options, index);
            if (changed >= 0) set(changed);
        }, sub);
    }

    private static int Segmented(string id, string[] options, int index)
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var height = ImGui.GetFrameHeight();
        var result = -1;
        var accent = UIConstants.Accent;
        var x = pos.X;
        for (var i = 0; i < options.Length; i++)
        {
            var w = ImGui.CalcTextSize(options[i]).X + 18f;
            ImGui.SetCursorScreenPos(new Vector2(x, pos.Y));
            if (ImGui.InvisibleButton($"{id}_{i}", new Vector2(w, height)))
                result = i;
            var hovered = ImGui.IsItemHovered();
            var active = i == index;
            var min = new Vector2(x, pos.Y);
            var max = new Vector2(x + w, pos.Y + height);
            if (active)
                dl.AddRectFilled(min, max, U32(UIConstants.WithAlpha(accent, 0.18f)));
            else if (hovered)
                dl.AddRectFilled(min, max, U32(UIConstants.Hover));
            var textCol = active ? accent : hovered ? UIConstants.TextPrimary : UIConstants.TextSecondary;
            dl.AddText(new Vector2(x + 9f, pos.Y + (height - ImGui.GetTextLineHeight()) / 2f), U32(textCol), options[i]);
            x += w;
        }
        var outer = new Vector2(x, pos.Y + height);
        dl.AddRect(pos, outer, U32(UIConstants.LineStrong), 3f, ImDrawFlags.None, 1f);
        return result;
    }

    private void ButtonSetting(string label, string? description, string buttonLabel, Action onClick, bool disabled = false, Vector4? color = null, bool sub = false)
    {
        var width = (ImGui.CalcTextSize(buttonLabel).X + 32f) / ImGuiHelpers.GlobalScale;
        Row(label, description, width, () =>
        {
            if (UIConstants.AccentButton(buttonLabel, color, width * ImGuiHelpers.GlobalScale, disabled))
                onClick();
        }, sub);
    }

    private void Save(Action change)
    {
        change();
        plugin.Configuration.Save();
    }

    private void DrawGeneralGroup(Configuration config, bool ownsAnyVenue)
    {
        var langLabel = config.Language == "DE" ? Lang.LangGerman : Lang.LangEnglish;
        ComboSetting("##lang", Lang.Language, null, langLabel, 130f, () =>
        {
            if (ImGui.Selectable(Lang.LangEnglish, config.Language == "EN"))
                SetLanguage(config, "EN");
            if (ImGui.Selectable(Lang.LangGerman, config.Language == "DE"))
                SetLanguage(config, "DE");
        });

        ComboSetting("##defaultTab", Lang.DefaultTabLabel, null, DefaultTabLabel(config.DefaultTab), 150f, () =>
        {
            foreach (var tab in new[] { "remember", "map", "dir", "evt", "fes" })
                if (ImGui.Selectable(DefaultTabLabel(tab), config.DefaultTab == tab))
                    Save(() => config.DefaultTab = tab);
        });

        ButtonSetting(Lang.ResetWindowPosition, Lang.ResetWindowPositionTip, $"{Lang.Reset}##resetWindow", () =>
        {
            plugin.VenueMapWindow.ResetWindowPosition();
            plugin.Toasts.Show(Lang.ToastWindowReset, ToastKind.Success, 2.5);
        });
    }

    private void DrawDirectoryGroup(Configuration config, bool ownsAnyVenue)
    {
        ToggleSetting("##boostOpen", Lang.BoostOpenVenues, Lang.BoostOpenVenuesTip, config.BoostOpenVenues,
            v => Save(() => config.BoostOpenVenues = v));
        ToggleSetting("##autoOpenVenue", Lang.AutoOpenOnVenueEnter, Lang.AutoOpenOnVenueEnterTip, config.AutoOpenOnVenueEnter,
            v => Save(() => config.AutoOpenOnVenueEnter = v));
        ToggleSetting("##autoOpenOwnVenue", Lang.AutoOpenOwnVenue,
            ownsAnyVenue ? Lang.AutoOpenOwnVenueTip : Lang.HideMarkersInOwnVenueNeedsOwnerTip, config.AutoOpenOwnVenue,
            v => Save(() => config.AutoOpenOwnVenue = v), sub: true, disabled: !ownsAnyVenue);
        ToggleSetting("##showQuickPopup", Lang.ShowQuickPopupOnEnter, Lang.ShowQuickPopupOnEnterTip, config.ShowQuickPopupOnEnter,
            v => Save(() => config.ShowQuickPopupOnEnter = v));

        ToggleSetting("##partyFinderAds", Lang.PartyFinderAds, Lang.PartyFinderAdsTip, config.PartyFinderAds,
            v => Save(() => config.PartyFinderAds = v));

        ToggleSetting("##chatLinks", Lang.ChatLinks, Lang.ChatLinksTip, config.ChatLinks,
            v => Save(() => config.ChatLinks = v));
        if (config.ChatLinks)
            ComboSetting("##chatChannels", Lang.ChatLinkChannels, null, ChannelPreview(config.ChatLinkChannels), 170f, () =>
            {
                foreach (var (flag, label) in ChatChannelOptions())
                {
                    var on = (config.ChatLinkChannels & flag) != 0;
                    if (ImGui.Checkbox($"{label}##ch_{flag}", ref on))
                        Save(() => config.ChatLinkChannels = on ? config.ChatLinkChannels | flag : config.ChatLinkChannels & ~flag);
                }
            }, sub: true);

        var hidden = config.HiddenVenueIds.Count;
        ButtonSetting(Lang.HiddenVenues, hidden > 0 ? Lang.UnhideAllVenuesTip(hidden) : Lang.NoHiddenVenues,
            $"{Lang.UnhideAllVenues}##unhideAll", () =>
            {
                config.HiddenVenueIds.Clear();
                config.Save();
                plugin.Toasts.Show(Lang.ToastAllVenuesUnhidden(hidden), ToastKind.Success, 2.5);
            }, disabled: hidden == 0);
    }

    private static (ChatLinkChannels Flag, string Label)[] ChatChannelOptions() =>
    [
        (ChatLinkChannels.Say, Lang.ChannelSay),
        (ChatLinkChannels.ShoutYell, Lang.ChannelShout),
        (ChatLinkChannels.Tell, Lang.ChannelTell),
        (ChatLinkChannels.Party, Lang.ChannelParty),
        (ChatLinkChannels.FreeCompany, Lang.ChannelFc),
        (ChatLinkChannels.Linkshell, Lang.ChannelLs),
        (ChatLinkChannels.CrossLinkshell, Lang.ChannelCwls),
        (ChatLinkChannels.Novice, Lang.ChannelNovice),
    ];

    private static string ChannelPreview(ChatLinkChannels channels)
    {
        if ((channels & ChatLinkChannels.All) == ChatLinkChannels.All) return Lang.ChatChannelsAll;
        var count = ChatChannelOptions().Count(o => (channels & o.Flag) != 0);
        return count == 0 ? Lang.ChatChannelsNone : count == 1 ? ChatChannelOptions().First(o => (channels & o.Flag) != 0).Label : Lang.ChatChannelsCount(count);
    }

    private void DrawMarkersGroup(Configuration config, bool ownsAnyVenue)
    {
        ToggleSetting("##markers3d", Lang.Markers3D, Lang.Markers3DDesc, plugin.PictomancyMarkers.Enabled, v => Save(() =>
        {
            plugin.PictomancyMarkers.Enabled = v;
            config.Markers3DEnabled = v;
        }));

        var markersOff = !plugin.PictomancyMarkers.Enabled;
        using (ImRaii.Disabled(markersOff))
        {
            SegmentSetting("##markerNames", Lang.MarkerNames, null,
                [Lang.MarkerNamesAlways, Lang.MarkerNamesNear, Lang.MarkerNamesOff], (int)config.MarkerNames,
                i => Save(() => config.MarkerNames = (MarkerNamesMode)i), sub: true);
            ToggleSetting("##markerWalls", Lang.MarkerThroughWalls, Lang.MarkerThroughWallsTip, config.MarkerThroughWalls,
                v => Save(() => config.MarkerThroughWalls = v), sub: true);
            ToggleSetting("##markerBehindUi", Lang.MarkersBehindUi, Lang.MarkersBehindUiTip, config.MarkersBehindUi,
                v => Save(() => config.MarkersBehindUi = v), sub: true);
            ToggleSetting("##markerVenueColors", Lang.MarkerVenueColors, Lang.MarkerVenueColorsTip, config.MarkerVenueColors,
                v => Save(() => config.MarkerVenueColors = v), sub: true);
            ToggleSetting("##hideOwn", Lang.HideMarkersInOwnVenue,
                ownsAnyVenue ? Lang.HideMarkersInOwnVenueTip : Lang.HideMarkersInOwnVenueNeedsOwnerTip, config.HideMarkersInOwnVenue,
                v => Save(() => config.HideMarkersInOwnVenue = v), sub: true, disabled: !ownsAnyVenue);

            ComboSetting("##venueSigns", Lang.VenueSigns, Lang.VenueSignsTip, SignsLabel(config.VenueSigns), 150f, () =>
            {
                foreach (var mode in new[] { VenueSignsMode.All, VenueSignsMode.Open, VenueSignsMode.Favorites, VenueSignsMode.Off })
                    if (ImGui.Selectable(SignsLabel(mode), config.VenueSigns == mode))
                        Save(() => config.VenueSigns = mode);
            });

            using (ImRaii.Disabled(config.VenueSigns == VenueSignsMode.Off))
            {
                var rangeIndex = Array.FindIndex(SignRanges, r => MathF.Abs(r - config.VenueSignRange) < 1f);
                SegmentSetting("##signRange", Lang.VenueSignRange, Lang.VenueSignRangeTip,
                    SignRanges.Select(r => r >= Configuration.WholeWardRange ? Lang.WholeWard : $"{r:0} y").ToArray(),
                    rangeIndex < 0 ? 1 : rangeIndex, i => Save(() => config.VenueSignRange = SignRanges[i]), sub: true);
            }
        }

        ToggleSetting("##placardNames", Lang.PlacardNames, Lang.PlacardNamesTip, config.PlacardNames,
            v => Save(() => config.PlacardNames = v));

        ComboSetting("##gameMapNames", Lang.GameMapNames, Lang.GameMapDesc, GameMapLabel(config.GameMapNames), 150f, () =>
        {
            foreach (var mode in new[] { GameMapMode.Off, GameMapMode.Native, GameMapMode.Overlay, GameMapMode.Both })
            {
                if (ImGui.Selectable(GameMapLabel(mode), config.GameMapNames == mode))
                {
                    Save(() => config.GameMapNames = mode);
                    plugin.GameMapMarkers.Refresh();
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(Lang.GameMapTip);
            }
        });

        SegmentSetting("##guideAnimation", Lang.GuideAnimLabel, null,
            [Lang.GuideAnimArrow, Lang.GuideAnimSparkles, Lang.GuideAnimPulse, Lang.GuideAnimDots], (int)config.GuideAnimation,
            i => Save(() => config.GuideAnimation = (GuideAnimation)i));
        SegmentSetting("##guideColor", Lang.GuideColorLabel, Lang.GuideColorTip, [Lang.GuideColorSingle, Lang.GuideColorVenue],
            (int)config.GuideColor, i => Save(() => config.GuideColor = (GuideColorMode)i), sub: true);
        if (config.GuideColor == GuideColorMode.Single)
            Row(Lang.GuideColorPick, null, 30f, () =>
            {
                var color = config.GuideSingleColor;
                if (ImGui.ColorEdit3("##guideSingleColor", ref color, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel))
                    Save(() => config.GuideSingleColor = color);
            }, sub: true);
    }

    private void DrawNotificationsGroup(Configuration config, bool ownsAnyVenue)
    {
        ToggleSetting("##notifEnabled", Lang.EnableNotifications, Lang.NotificationsDesc, config.NotificationsEnabled,
            v => Save(() => config.NotificationsEnabled = v));

        using (ImRaii.Disabled(!config.NotificationsEnabled))
        {
            ToggleSetting("##notifyBanners", Lang.NotifyBanners, Lang.NotifyBannersDesc, config.NotifyBanners,
                v => Save(() => config.NotifyBanners = v), sub: true);
            ButtonSetting(Lang.TestNotification, Lang.TestNotificationDesc, $"{Lang.Test}##testToast",
                () => _ = TestAllToastKinds(plugin), sub: true);
            ComboSetting("##toastPos", Lang.NotificationPosition, null, CornerLabel(config.ToastPosition), 140f, () =>
            {
                foreach (var corner in new[] { ToastCorner.TopRight, ToastCorner.TopLeft, ToastCorner.BottomRight, ToastCorner.BottomLeft })
                {
                    if (!ImGui.Selectable(CornerLabel(corner), config.ToastPosition == corner)) continue;
                    Save(() => config.ToastPosition = corner);
                    plugin.Toasts.Show(Lang.ToastPositionChanged, ToastKind.Info, 3.5, tag: "toastPositionChanged");
                }
            }, sub: true);
            Row(Lang.MaxVisibleToasts, null, 80f, () =>
            {
                ImGui.SetNextItemWidth(80f * ImGuiHelpers.GlobalScale);
                var maxVisible = config.MaxVisibleToasts;
                if (ImGui.DragInt("##maxVisibleToasts", ref maxVisible, 0.1f, 1, 10))
                    Save(() => config.MaxVisibleToasts = Math.Clamp(maxVisible, 1, 10));
            }, sub: true);
            Row(Lang.ToastDurationLabel, null, 130f, () =>
            {
                ImGui.SetNextItemWidth(130f * ImGuiHelpers.GlobalScale);
                var seconds = config.ToastDurationMultiplier * ToastBaselineSeconds;
                if (ImGui.SliderFloat("##toastDuration", ref seconds, 1.5f, 9.0f, "%.1fs"))
                    Save(() => config.ToastDurationMultiplier = seconds / ToastBaselineSeconds);
            }, sub: true);
            ToggleSetting("##suppressCombat", Lang.SuppressInCombat, null, config.SuppressInCombat,
                v => Save(() => config.SuppressInCombat = v), sub: true);
            ToggleSetting("##favoriteOpenNotify", Lang.FavoriteOpenNotify, Lang.FavoriteOpenNotifyTip, config.FavoriteOpenNotify,
                v => Save(() => config.FavoriteOpenNotify = v), sub: true);
            ToggleSetting("##eventReminders", Lang.EventReminders, null, config.EventReminders,
                v => Save(() => config.EventReminders = v), sub: true);

            using (ImRaii.Disabled(!config.EventReminders))
            {
                ToggleSetting("##favOnly", Lang.EventRemindersFavOnly, null, config.EventRemindersFavoritesOnly,
                    v => Save(() => config.EventRemindersFavoritesOnly = v), sub: true);
                Row(Lang.EventReminderMinutesLabel, null, 80f, () =>
                {
                    ImGui.SetNextItemWidth(80f * ImGuiHelpers.GlobalScale);
                    var minutes = config.EventReminderMinutes;
                    if (ImGui.DragInt("##eventReminderMin", ref minutes, 0.2f, 5, 60))
                        Save(() => config.EventReminderMinutes = Math.Clamp(minutes, 5, 60));
                }, sub: true);
            }
        }

        SegmentSetting("##houseRules", Lang.HouseRulesSetting, Lang.HouseRulesSettingTip,
            [Lang.HouseRulesEvery, Lang.HouseRulesFirst, Lang.HouseRulesOff], (int)config.HouseRules,
            i => Save(() => config.HouseRules = (HouseRulesMode)i));
        if (config.RulesMuted.Count > 0)
            ButtonSetting(Lang.HouseRulesMutedTitle, Lang.HouseRulesMutedDesc(config.RulesMuted.Count), $"{Lang.UnhideAllVenues}##rulesUnmute",
                () => Save(() => config.RulesMuted.Clear()), sub: true);
    }

    private void DrawAccessibilityGroup(Configuration config, bool ownsAnyVenue)
    {
        var sizeIndex = config.MarkerSizeScale switch
        {
            >= 1.35f => 2,
            >= 1.15f => 1,
            _ => 0,
        };
        var sizeLabels = new[] { Lang.MarkerSizeNormal, Lang.MarkerSizeLarge, Lang.MarkerSizeExtraLarge };
        ComboSetting("##markerSize", Lang.MarkerSizeLabel, null, sizeLabels[sizeIndex], 150f, () =>
        {
            var scales = new[] { 1.0f, 1.2f, 1.4f };
            for (var i = 0; i < scales.Length; i++)
                if (ImGui.Selectable(sizeLabels[i], i == sizeIndex))
                {
                    var scale = scales[i];
                    Save(() => config.MarkerSizeScale = scale);
                }
        });
        ToggleSetting("##strongPulse", Lang.MarkerStrongPulse, null, config.MarkerStrongPulse,
            v => Save(() => config.MarkerStrongPulse = v));
        ToggleSetting("##colorOverride", Lang.MarkerColorOverride, Lang.MarkerColorOverrideTip, config.MarkerColorOverrideEnabled,
            v => Save(() => config.MarkerColorOverrideEnabled = v));

        if (!config.MarkerColorOverrideEnabled) return;
        Row(Lang.MarkerColor, Lang.MarkerColorPickerTip, 30f, () =>
        {
            var color = config.MarkerOverrideColor;
            if (ImGui.ColorEdit3("##markerOverrideColor", ref color, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel))
                Save(() => config.MarkerOverrideColor = color);
        }, sub: true);
    }

    private void DrawDataGroup(Configuration config, bool ownsAnyVenue)
    {
        ToggleSetting("##autoPull", Lang.AutoPullCfg, Lang.AutoPullDesc, config.AutoPullOnStartup,
            v => Save(() => config.AutoPullOnStartup = v));
        ButtonSetting(Lang.PullNowLabel, Lang.PullNowDesc, isPulling ? "...##pullNow" : $"{Lang.PullNow}##pullNow", () =>
        {
            isPulling = true;
            _ = PullAsync();
        }, disabled: isPulling || string.IsNullOrWhiteSpace(config.GitHubConfigUrl));
        ButtonSetting(Lang.ResetCacheLabel, Lang.ResetCacheDesc, $"{Lang.ResetCache}##resetCache", ResetCache, color: UIConstants.Warning);
    }
}