using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using VenueMapper.Models;
using VenueMapper.Services;

namespace VenueMapper.UI;

public static class VenueShare
{
    private static string District(Venue v)
    {
        var parts = v.Address?.Split(" - ") ?? Array.Empty<string>();
        return parts.Length >= 3 ? parts[2] : "";
    }

    private static string? Status(VenueMapperPlugin plugin, Venue v)
    {
        var (open, until, next) = plugin.GetVenueTimes(v);
        if (open)
            return until is { } end ? Lang.SignOpenUntil(UIConstants.FormatServerTime(end)) : Lang.StatOpenNow;
        return next is { } start && start - DateTime.UtcNow < TimeSpan.FromDays(2) ? Lang.SignOpensAt(UIConstants.FormatServerTime(start)) : null;
    }

    public static string LifestreamCommand(Venue v) => $"/li {v.Server} {District(v)} {v.Ward} {v.Plot}";

    public static string ChatText(VenueMapperPlugin plugin, Venue v)
    {
        var parts = new List<string> { UIConstants.DisplayName(v.Name), $"{v.Server}, {District(v)} W{v.Ward} P{v.Plot}" };
        if (Status(plugin, v) is { } status) parts.Add(status);
        return string.Join(" · ", parts);
    }

    public static string DiscordText(VenueMapperPlugin plugin, Venue v)
    {
        var lines = new List<string>
        {
            $"**{UIConstants.DisplayName(v.Name)}**",
            $"{v.Datacenter} · {v.Server} · {District(v)} · Ward {v.Ward}, Plot {v.Plot}",
        };
        if (Status(plugin, v) is { } status) lines.Add(status);
        lines.Add($"`{LifestreamCommand(v)}`");
        var links = v.Links;
        if (links != null)
        {
            if (!string.IsNullOrEmpty(links.Discord)) lines.Add($"Discord: <{links.Discord}>");
            if (!string.IsNullOrEmpty(links.Website)) lines.Add($"Website: <{links.Website}>");
            if (!string.IsNullOrEmpty(links.Partake)) lines.Add($"Partake: <{links.Partake}>");
        }
        if (SiteLinks.VenuePage(v) is { } page) lines.Add($"VenueMapper: <{page}>");
        return string.Join("\n", lines);
    }

    public static void DrawMenu(VenueMapperPlugin plugin, Venue v)
    {
        if (ImGui.MenuItem($"{Lang.ShareChat}##share_chat"))
        {
            ImGui.SetClipboardText(ChatText(plugin, v));
            plugin.Toasts.Show(Lang.ToastShareCopied, ToastKind.Success, 2.5);
        }
        if (ImGui.MenuItem($"{Lang.ShareDiscord}##share_discord"))
        {
            ImGui.SetClipboardText(DiscordText(plugin, v));
            plugin.Toasts.Show(Lang.ToastShareCopied, ToastKind.Success, 2.5);
        }
        if (SiteLinks.VenuePage(v) is { } page && ImGui.MenuItem($"{Lang.ShareWebLink}##share_web"))
        {
            ImGui.SetClipboardText(page);
            plugin.Toasts.Show(Lang.ToastShareCopied, ToastKind.Success, 2.5);
        }
        if (ImGui.MenuItem($"{Lang.CopyLifestreamAddress}##share_ls"))
        {
            ImGui.SetClipboardText(LifestreamCommand(v));
            plugin.Toasts.Show(Lang.ToastAddressCopied, ToastKind.Success, 2.0);
        }
        if (ImGui.MenuItem($"{Lang.ShareFlag}##share_flag"))
        {
            if (plugin.SetVenueFlag(v))
                plugin.Toasts.Show(Lang.ToastFlagSet, ToastKind.Success, 3.5);
            else
                plugin.Toasts.Show(Lang.ToastShowOnMapFailed, ToastKind.Warning, 3.0);
        }
    }

    public static void Popup(VenueMapperPlugin plugin, Venue v, string id)
    {
        using var style = UIConstants.MenuStyle();
        using var popup = ImRaii.Popup(id);
        if (popup)
            DrawMenu(plugin, v);
    }

    public static bool IconButton(string id, FontAwesomeIcon icon, string tooltip, Vector2 pos, float size)
    {
        ImGui.SetCursorScreenPos(pos);
        var clicked = ImGui.InvisibleButton(id, new Vector2(size));
        var hovered = ImGui.IsItemHovered();
        var dl = ImGui.GetWindowDrawList();
        if (hovered)
            dl.AddRectFilled(pos, pos + new Vector2(size), ImGui.ColorConvertFloat4ToU32(UIConstants.Hover), 3f);
        var glyph = icon.ToIconString();
        Vector2 glyphSize;
        using (ImRaii.PushFont(UiBuilder.IconFont))
            glyphSize = ImGui.CalcTextSize(glyph);
        var scale = MathF.Min(1f, (size - 8f) / MathF.Max(glyphSize.X, glyphSize.Y));
        UIConstants.DrawIconCentered(dl, UiBuilder.IconFont, UiBuilder.IconFont.FontSize * scale, pos + new Vector2(size / 2f), glyph,
            ImGui.ColorConvertFloat4ToU32(hovered ? UIConstants.Accent : UIConstants.TextSecondary));
        if (hovered)
            ImGui.SetTooltip(tooltip);
        return clicked;
    }
}
