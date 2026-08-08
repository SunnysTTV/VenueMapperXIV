using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using VenueMapper.Models;
using VenueMapper.Services;

namespace VenueMapper.UI;

public class VenueDetailWindow : Window, IDisposable
{
    private readonly VenueMapperPlugin plugin;
    private Venue? venue;

    public VenueDetailWindow(VenueMapperPlugin plugin)
        : base("Venue Details##VenueDetail", ImGuiWindowFlags.NoCollapse)
    {
        this.plugin = plugin;
        Size = new Vector2(400, 520);
        SizeCondition = ImGuiCond.Always;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 480),
            MaximumSize = new Vector2(600, 900),
        };
    }

    public void Open(Venue v)
    {
        venue = v;
        IsOpen = true;
    }

    public override void PreDraw() => UIConstants.PushWindowChrome(UIConstants.Glow);
    public override void PostDraw() => UIConstants.PopWindowChrome();

    public override void Draw()
    {
        if (SizeCondition == ImGuiCond.Always)
            SizeCondition = ImGuiCond.FirstUseEver;

        var v = venue;
        if (v == null)
        {
            ImGui.TextColored(UIConstants.TextSecondary, Lang.NoVenues);
            return;
        }

        UIConstants.PushScrollbarStyle();
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0, 0, 0, 0));
        try
        {

            if (ImGui.BeginChild("##venueDetailScroll", new Vector2(0, 0)))
                DrawContent(v);
        }
        catch (Exception ex)
        {
            VenueMapperPlugin.Log.Error(ex, "[VenueMapper] VenueDetailWindow draw failed");
        }
        finally
        {
            ImGui.EndChild();
            ImGui.PopStyleColor();
            UIConstants.PopScrollbarStyle();
        }
    }

    private static float Luminance(Vector4 col) => 0.299f * col.X + 0.587f * col.Y + 0.114f * col.Z;

    private void DrawContent(Venue v)
    {
        ImGui.PushTextWrapPos(0);

        var xivId = VenueMapWindow.ExtractXivVenuesId(v.Links?.FfxivVenues);
        if (v.TeamId > 0)
        {
            _ = plugin.PartakeApi.FetchTeamAsync(v.TeamId);

            _ = plugin.PartakeApi.FetchTeamIconAsync(v.TeamId);
        }
        if (xivId != null) plugin.XivVenues.RequestSchedule(xivId);

        var logoUrl = v.TeamId > 0 ? plugin.PartakeApi.GetTeamIconUrl(v.TeamId) : null;
        logoUrl ??= xivId != null ? plugin.XivVenues.GetBannerUri(xivId) : null;
        var logoTex = plugin.VenueLogos.Get(logoUrl);

        var avatarCol = v.Colors?.PrimaryVec ?? UIConstants.Primary;
        const float avatarR = 24f;
        var dl = ImGui.GetWindowDrawList();
        var avatarOrigin = ImGui.GetCursorScreenPos();
        var avatarCenter = avatarOrigin + new Vector2(avatarR, avatarR);

        if (logoTex != null)
        {

            var texW = (float)logoTex.Width;
            var texH = (float)logoTex.Height;
            var uvMin = Vector2.Zero;
            var uvMax = Vector2.One;
            if (texW > texH && texW > 0)
            {
                var crop = (texW - texH) / texW / 2f;
                uvMin = new Vector2(crop, 0f);
                uvMax = new Vector2(1f - crop, 1f);
            }
            else if (texH > texW && texH > 0)
            {
                var crop = (texH - texW) / texH / 2f;
                uvMin = new Vector2(0f, crop);
                uvMax = new Vector2(1f, 1f - crop);
            }

            dl.AddImageRounded(logoTex.Handle, avatarOrigin, avatarOrigin + new Vector2(avatarR * 2, avatarR * 2),
                uvMin, uvMax, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 1)), avatarR);
        }
        else
        {
            dl.AddCircleFilled(avatarCenter, avatarR, ImGui.ColorConvertFloat4ToU32(avatarCol));
            var letter = v.Name.Length > 0 ? v.Name[..1].ToUpperInvariant() : "?";
            var letterCol = Luminance(avatarCol) > 0.5f ? new Vector4(0, 0, 0, 1) : new Vector4(1, 1, 1, 1);
            var font = ImGui.GetFont();
            var letterSize = avatarR * 1.1f;
            var letterScale = letterSize / font.FontSize;
            var letterSz = ImGui.CalcTextSize(letter) * letterScale;
            dl.AddText(font, letterSize, avatarCenter - letterSz * 0.5f, ImGui.ColorConvertFloat4ToU32(letterCol), letter);
        }
        dl.AddCircle(avatarCenter, avatarR, ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Glow, 0.5f)), 0, 1.5f);

        ImGui.SetCursorScreenPos(avatarOrigin + new Vector2(avatarR * 2 + 10, 0));
        ImGui.BeginGroup();

        ImGui.TextColored(UIConstants.Primary, v.Name.ToUpperInvariant());
        ImGui.SameLine(0, 6);
        var nsfwCol = v.Nsfw ? UIConstants.Danger : UIConstants.Success;
        ImGui.TextColored(nsfwCol, $"[{(v.Nsfw ? Lang.NsfwBadge : Lang.SfwBadge)}]");

        var sched = xivId != null ? plugin.XivVenues.GetSchedule(xivId) : null;
        var statusText = sched?.GetStatusText() ?? "";
        if (statusText.Length > 0)
            ImGui.TextColored(sched!.IsOpenNow ? UIConstants.Success : UIConstants.WithAlpha(UIConstants.TextSecondary, 0.6f), statusText);

        var myHash = OwnerIdHelper.ComputeHash(VenueMapperPlugin.PlayerState.ContentId);
        if (v.OwnerIdHashes.Contains(myHash))
        {
            ImGui.TextColored(UIConstants.Glow, Lang.YouAreOwner);
        }

        ImGui.EndGroup();
        ImGui.SetCursorScreenPos(new Vector2(avatarOrigin.X, avatarOrigin.Y + MathF.Max(avatarR * 2, ImGui.GetItemRectSize().Y) + 6));

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        void Row(string label, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            ImGui.TextColored(UIConstants.TextSecondary, label);
            ImGui.SameLine(120);
            ImGui.TextColored(UIConstants.TextPrimary, value);
        }

        Row(Lang.Datacenter, v.Datacenter);
        Row(Lang.Server, v.Server);

        var addrParts = v.Address.Split(" - ");
        var district = addrParts.Length >= 3 ? addrParts[2] : "";
        if (!string.IsNullOrEmpty(district))
            Row(Lang.HousingDist, district);
        if (v.Ward > 0) Row(Lang.Ward, v.Ward.ToString());
        if (v.Plot > 0) Row(Lang.Plot, v.Plot.ToString());

        if (!string.IsNullOrEmpty(v.Address))
        {
            ImGui.Spacing();
            var btnW = (ImGui.GetContentRegionAvail().X - 8) / 2f;
            if (UIConstants.AccentButton($"{Lang.Visit}##detailVisit", UIConstants.Glow, width: btnW))
            {
                if (!plugin.Lifestream.IsLoaded)
                {
                    plugin.Toasts.Show(Lang.ToastLifestreamMissing, ToastKind.Warning, 3.5);
                }
                else
                {
                    var ok = plugin.Lifestream.NavigateTo(v.Address);
                    plugin.Toasts.Show(ok ? Lang.ToastTeleportingTo(v.Name) : Lang.ToastTeleportFailed,
                        ok ? ToastKind.Success : ToastKind.Warning, 2.5);
                }
            }
            ImGui.SameLine(0, 8);
            if (UIConstants.AccentButton($"{Lang.CopyLifestreamAddress}##detailCopyLs", UIConstants.Primary, width: btnW))
            {

                ImGui.SetClipboardText($"/li {v.Server} {district} {v.Ward} {v.Plot}");
                plugin.Toasts.Show(Lang.ToastAddressCopied, ToastKind.Success, 2.0);
            }
        }

        if (v.Floors.Any(f => f.Services.Count > 0))
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.TextColored(UIConstants.TextSecondary, Lang.VenueServices);
            foreach (var floor in v.Floors)
            {
                if (floor.Services.Count == 0) continue;
                ImGui.TextColored(UIConstants.Glow, VenueMapWindow.TranslateFloorName(floor.Name));
                ImGui.SameLine(120);
                var types = string.Join(", ", floor.Services.Select(s => VenueMapWindow.ChipLabel(s.Type)).Distinct());
                ImGui.TextColored(UIConstants.TextPrimary, types);
            }
        }

        {
            var events = v.TeamId > 0 ? plugin.PartakeApi.GetEvents(v.TeamId) : new List<VenueEvent>();
            var loading = v.TeamId > 0 && plugin.PartakeApi.IsLoading(v.TeamId);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.TextColored(UIConstants.TextSecondary, Lang.NextEvent);
            ImGui.Spacing();

            if (loading && events.Count == 0)
            {
                ImGui.TextColored(UIConstants.WithAlpha(UIConstants.Glow, 0.5f), Lang.Loading);
            }
            else if (events.Count > 0)
            {
                var evt = events[0];
                var title = EventsView.StripEmoji(evt.Title);
                var isNow = evt.StartTime <= DateTime.UtcNow && evt.EndTime >= DateTime.UtcNow;
                var accent = v.Colors?.PrimaryVec ?? UIConstants.Primary;

                var cardDl = ImGui.GetWindowDrawList();
                var cardMin = ImGui.GetCursorScreenPos();
                var cardW = ImGui.GetContentRegionAvail().X;
                const float cardH = 46f;
                var cardMax = cardMin + new Vector2(cardW, cardH);
                var cardBody = UIConstants.Vector4Lerp(UIConstants.Background, UIConstants.CardBackground, 0.75f);
                UIConstants.DrawCardWithAccentBar(cardDl, cardMin, cardMax, cardBody, accent, UIConstants.ChipRounding);

                var textX = cardMin.X + 12;
                var lineH = ImGui.GetTextLineHeight();
                cardDl.AddText(new Vector2(textX, cardMin.Y + 6),
                    ImGui.ColorConvertFloat4ToU32(UIConstants.TextPrimary), title);

                string dateText;
                try { dateText = evt.StartTime.ToLocalTime().ToString("ddd, MMM d - h:mm tt"); }
                catch { dateText = evt.StartTime.ToString("ddd, MMM d - h:mm tt"); }
                cardDl.AddText(new Vector2(textX, cardMin.Y + 8 + lineH),
                    ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.6f)), dateText);

                if (isNow)
                {
                    var nowBadge = " NOW ";
                    var nowSz = ImGui.CalcTextSize(nowBadge);
                    var bx = cardMax.X - nowSz.X - 10;
                    cardDl.AddRectFilled(new Vector2(bx - 2, cardMin.Y + 5), new Vector2(bx + nowSz.X + 2, cardMin.Y + 5 + nowSz.Y + 2),
                        ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Warning, 0.25f)), UIConstants.ChipRounding);
                    cardDl.AddRect(new Vector2(bx - 2, cardMin.Y + 5), new Vector2(bx + nowSz.X + 2, cardMin.Y + 5 + nowSz.Y + 2),
                        ImGui.ColorConvertFloat4ToU32(UIConstants.WithAlpha(UIConstants.Warning, 0.6f)), UIConstants.ChipRounding, ImDrawFlags.None, 1f);
                    cardDl.AddText(new Vector2(bx, cardMin.Y + 6),
                        ImGui.ColorConvertFloat4ToU32(UIConstants.Warning), nowBadge);
                }

                ImGui.Dummy(new Vector2(cardW, cardH));
                ImGui.Spacing();

                if (UIConstants.AccentButton($"{Lang.ViewPartake}##nextEvent", new Vector4(0.95f, 0.55f, 0.15f, 1f), width: -1))
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        { FileName = $"https://partake.gg/events/{evt.EventId}", UseShellExecute = true }); } catch { }
                }
            }
            else
            {

                ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f), Lang.NoPartakeEvents);
                if (statusText.Length > 0)
                    ImGui.TextColored(sched!.IsOpenNow ? UIConstants.Success : UIConstants.WithAlpha(UIConstants.TextSecondary, 0.7f), statusText);
                else
                    ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.4f), Lang.NoScheduleInfo);
            }
        }

        if (!string.IsNullOrEmpty(v.Description))
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.TextColored(UIConstants.TextSecondary, Lang.Description);
            ImGui.TextWrapped(v.Description);
        }

        if (v.Links != null && v.Links.HasAny)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.TextColored(UIConstants.TextSecondary, Lang.Links);
            ImGui.Spacing();

            var pairW = (ImGui.GetContentRegionAvail().X - 8) / 2f;

            var linkEntries = new (string Label, string Url, Vector4 Col)[]
            {
                ("Discord", v.Links.Discord, new Vector4(0.34f, 0.40f, 0.93f, 1f)),
                ("Partake", v.Links.Partake, new Vector4(0.95f, 0.55f, 0.15f, 1f)),
                ("XIVVenues", v.Links.FfxivVenues, new Vector4(0.7f, 0.3f, 0.9f, 1f)),
                ("Website", v.Links.Website, UIConstants.Glow),
            }.Where(e => !string.IsNullOrEmpty(e.Url)).ToArray();

            for (var i = 0; i < linkEntries.Length; i++)
            {
                var (label, url, col) = linkEntries[i];
                if (UIConstants.AccentButton($"{label}##detail_{label}", col, width: pairW))
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        { FileName = url, UseShellExecute = true }); } catch { }
                }
                if (i % 2 == 0 && i + 1 < linkEntries.Length)
                    ImGui.SameLine(0, 8);
            }
        }

        ImGui.PopTextWrapPos();
    }

    public void Dispose() { }
}
