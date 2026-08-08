using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using VenueMapper.Models;
using VenueMapper.Services;

namespace VenueMapper.UI;

public class OwnerSubmitWindow : Window, IDisposable
{
    private readonly VenueMapperPlugin plugin;
    private double hackerModeStart = -1;
    private double hackerTitleLoopStart = -1;

    private string loadedVenueId = "";

    private string clubName = "";
    private string discordName = "";
    private string description = "";
    private string selectedDc = "";
    private string selectedServer = "";
    private string ward = "";
    private string plot = "";
    private int districtIndex;
    private int houseSizeIndex;
    private bool isNsfw;
    private bool registerOwnerId = true;

    private string discordLink = "";
    private string partakeLink = "";
    private string xivVenuesLink = "";
    private string websiteLink = "";

    private string colorPrimary = "#ff006e";
    private string colorAccent = "#00f0ff";
    private string colorSecondary = "#9d4edd";
    private Vector3 colPriVec = new(1f, 0f, 0.43f);
    private Vector3 colAccVec = new(0f, 0.94f, 1f);
    private Vector3 colSecVec = new(0.62f, 0.31f, 0.87f);

    private readonly List<ServiceEntry> services = new();

    private bool copied;
    private double copiedTime;
    private string copiedWhat = "";
    private double copyJsonStart;

    private static readonly string[] Districts =
        ["Mist", "Lavender Beds", "The Goblet", "Shirogane", "Empyreum"];
    private static readonly string[] HouseSizeLabels = ["L - Large", "M - Medium", "S - Small"];
    private static readonly string[] HouseSizeKeys = ["L", "M", "S"];

    private static readonly Dictionary<string, (HashSet<int> L, HashSet<int> M)> PlotSizes = new()
    {
        ["Mist"]          = (new HashSet<int> { 2, 5, 15, 32, 35, 45 },
                             new HashSet<int> { 1, 4, 6, 7, 14, 29, 30, 31, 34, 36, 37, 44, 59, 60 }),
        ["Lavender Beds"] = (new HashSet<int> { 3, 6, 28, 33, 36, 58 },
                             new HashSet<int> { 1, 5, 11, 16, 21, 27, 30, 31, 35, 41, 46, 51, 57, 60 }),
        ["The Goblet"]    = (new HashSet<int> { 5, 13, 30, 35, 43, 60 },
                             new HashSet<int> { 4, 6, 8, 11, 12, 19, 25, 34, 36, 38, 41, 42, 49, 55 }),
        ["Empyreum"]      = (new HashSet<int> { 12, 22, 30, 42, 52, 60 },
                             new HashSet<int> { 2, 7, 8, 17, 18, 21, 26, 32, 37, 47, 48, 51 }),
        ["Shirogane"]     = (new HashSet<int> { 7, 16, 30, 37, 46, 60 },
                             new HashSet<int> { 1, 8, 13, 15, 19, 24, 28, 31, 38, 43, 45, 49, 54, 58 }),
    };

    private void AutoDetectHouseSizeFromPlot()
    {
        if (!int.TryParse(plot, out var p)) return;
        if (!PlotSizes.TryGetValue(Districts[districtIndex], out var sizes)) return;
        houseSizeIndex = sizes.L.Contains(p) ? 0 : sizes.M.Contains(p) ? 1 : 2;
    }

    private void AutoFillFromPosition()
    {
        var tracker = plugin.PositionTracker;

        var worldName = tracker.CurrentServerName;
        if (!string.IsNullOrEmpty(worldName))
        {
            foreach (var (dc, servers) in ServerData.DatacenterServers)
            {
                if (servers.Contains(worldName, StringComparer.OrdinalIgnoreCase))
                {
                    selectedDc = dc;
                    selectedServer = worldName;
                    break;
                }
            }
        }

        var district = tracker.CurrentHousingDistrict;
        if (!string.IsNullOrEmpty(district))
        {

            var idx = Array.FindIndex(Districts, d => district.Contains(d, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
                districtIndex = idx;
            else
                VenueMapperPlugin.Log.Warning($"[VenueMapper] Auto-detect: resolved district '{district}' did not match any known district name");
        }
        else
        {
            VenueMapperPlugin.Log.Warning("[VenueMapper] Auto-detect: housing district could not be resolved (empty) - territory lookup may have failed for this house/subdivision");
        }

        if (tracker.CurrentWard >= 0 && tracker.CurrentPlot >= 0)
        {
            ward = (tracker.CurrentWard + 1).ToString();
            plot = (tracker.CurrentPlot + 1).ToString();
            AutoDetectHouseSizeFromPlot();
        }

    }

    public bool CanLoadCurrentVenue()
    {
        var config = plugin.ConfigManager.Config;
        return config != null && plugin.PositionTracker.GetVenueAtCurrentPlotIncludingGarden(config) != null;
    }

    public void LoadCurrentVenue()
    {
        var config = plugin.ConfigManager.Config;
        var venue = config != null ? plugin.PositionTracker.GetVenueAtCurrentPlotIncludingGarden(config) : null;
        if (venue == null) return;

        loadedVenueId = venue.VenueId;
        clubName = venue.Name;
        selectedDc = venue.Datacenter;
        selectedServer = !string.IsNullOrEmpty(venue.Server) ? venue.Server : plugin.PositionTracker.CurrentServerName;
        ward = venue.Ward.ToString();
        plot = venue.Plot.ToString();

        var distIdx = Array.FindIndex(Districts, d => venue.Address.Contains(d, StringComparison.OrdinalIgnoreCase));
        if (distIdx >= 0) districtIndex = distIdx;

        var sizeIdx = Array.IndexOf(HouseSizeKeys, venue.HouseSize);
        houseSizeIndex = sizeIdx >= 0 ? sizeIdx : 0;

        isNsfw = venue.Nsfw;
        description = venue.Description;
        registerOwnerId = venue.OwnerIdHashes.Count > 0;

        if (venue.Colors != null)
        {
            colorPrimary = venue.Colors.Primary;
            colorAccent = venue.Colors.Accent;
            colorSecondary = venue.Colors.Secondary;
            colPriVec = HexToVec3(venue.Colors.Primary);
            colAccVec = HexToVec3(venue.Colors.Accent);
            colSecVec = HexToVec3(venue.Colors.Secondary);
        }

        if (venue.Links != null)
        {
            discordLink = venue.Links.Discord;
            partakeLink = venue.Links.Partake;
            xivVenuesLink = venue.Links.FfxivVenues;
            websiteLink = venue.Links.Website;
        }

        services.Clear();
        foreach (var floor in venue.Floors)
        {
            var floorIdx = Array.IndexOf(FloorNames, floor.Name);
            if (floorIdx < 0) continue;
            foreach (var svc in floor.Services)
            {
                var typeIdx = Array.IndexOf(ServiceTypes, svc.Type);
                services.Add(new ServiceEntry
                {
                    TypeIndex = typeIdx >= 0 ? typeIdx : 0,
                    Name = svc.Label,
                    FloorIndex = floorIdx,
                    Coords = new Vector3(svc.X, svc.Y, svc.Z),
                });
            }
        }

        plugin.Toasts.Show(Lang.ToastVenueLoaded(venue.Name), ToastKind.Success, 2.5);
    }

    private static Vector3 HexToVec3(string hex)
    {
        var h = (hex ?? "").TrimStart('#');
        if (h.Length != 6 || !int.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out var c))
            return new Vector3(1f, 1f, 1f);
        return new Vector3((c >> 16 & 0xFF) / 255f, (c >> 8 & 0xFF) / 255f, (c & 0xFF) / 255f);
    }

    private static readonly string[] ServiceTypes =
        ["bar", "dj_booth", "gambling", "entrance", "upstairs", "downstairs", "vip", "bath", "spa", "event", "stage"];
    private static readonly string[] ServiceTypeLabels =
        Array.ConvertAll(ServiceTypes, VenueMapWindow.ChipLabel);
    private static readonly string[] FloorNames =
        ["ground", "second", "cellar"];
    private static readonly string[] FloorNameLabels =
        Array.ConvertAll(FloorNames, VenueMapWindow.TranslateFloorName);

    private readonly bool isUpdateMode;

    public OwnerSubmitWindow(VenueMapperPlugin plugin, bool isUpdateMode = false)
        : base(isUpdateMode ? "Update Venue##OwnerUpdate" : "Venue Owner Setup##OwnerSubmit",
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoResize)
    {
        this.plugin = plugin;
        this.isUpdateMode = isUpdateMode;
        Size = new Vector2(540, 666);
        SizeCondition = ImGuiCond.Always;
    }

    private bool IdentityKnown() => isUpdateMode;

    public override void PreDraw()
    {
        UIConstants.PushWindowChrome(isUpdateMode ? UIConstants.Glow : UIConstants.Primary);
    }

    public override void PostDraw()
    {
        UIConstants.PopWindowChrome();
    }

    public override void Draw()
    {

        var hackerBooting = UIConstants.IsHackerBooting;
        try
        {
            if (hackerBooting) ImGui.BeginDisabled();
            try
            {
                ImGui.TextColored(isUpdateMode ? UIConstants.Glow : UIConstants.Primary,
                    isUpdateMode ? Lang.UpdateVenueTitle : Lang.OwnerTitle);
                ImGui.SameLine(0, 8);
                ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.45f),
                    isUpdateMode ? Lang.UpdateVenueDesc : Lang.OwnerDesc);
                ImGui.Spacing();

                var showCopied = copied && ImGui.GetTime() - copiedTime < 3.0;
                var bottomReserve = showCopied ? ImGui.GetTextLineHeightWithSpacing() + 4f : 0f;

                if (ImGui.BeginTabBar("##ownerTabs"))
                {
                    void ScrollTab(string label, Action draw)
                    {
                        if (!ImGui.BeginTabItem(label)) return;
                        UIConstants.PushScrollbarStyle();
                        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0, 0, 0, 0));
                        try
                        {
                            if (ImGui.BeginChild($"##scroll_{label}", new Vector2(0, -bottomReserve)))
                                draw();
                        }
                        finally
                        {
                            ImGui.EndChild();
                            ImGui.PopStyleColor();
                            UIConstants.PopScrollbarStyle();
                            ImGui.EndTabItem();
                        }
                    }

                    try
                    {
                        ScrollTab(Lang.VenueInfo, DrawVenueInfo);
                        ScrollTab($"{Lang.Links}##tab_links", DrawLinks);
                        ScrollTab("Services##tab_svc", DrawServices);
                        ScrollTab(Lang.Export, DrawExport);
                    }
                    finally
                    {
                        ImGui.EndTabBar();
                    }
                }

                if (copied && ImGui.GetTime() - copiedTime < 3.0)
                {
                    ImGui.Spacing();
                    ImGui.TextColored(UIConstants.Glow, $"{Lang.Copied} {copiedWhat}");
                }
            }
            finally
            {
                if (hackerBooting) ImGui.EndDisabled();
            }

            HackerModeOverlay.Draw(ref hackerModeStart, ref hackerTitleLoopStart, WindowName);
        }
        catch (Exception ex)
        {
            VenueMapperPlugin.Log.Error(ex, "[VenueMapper] OwnerSubmitWindow draw failed");
        }
    }

    private void DrawVenueInfo()
    {
        ImGui.Spacing();

        if (UIConstants.AccentButton($"{Lang.DetectPosition}##autopos", UIConstants.Glow, width: -1))
            AutoFillFromPosition();
        ImGui.Spacing();

        PushFieldStyle();

        Field(Lang.VenueName, ref clubName, Lang.VenueNameHint);
        Field(IdentityKnown() ? Lang.YourDiscordOptional : Lang.YourDiscord, ref discordName, Lang.DiscordHint);
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.TextSecondary, Lang.Datacenter);
        var dcWidth = ImGui.GetContentRegionAvail().X;
        UIConstants.StyledCombo("##dc", selectedDc.Length > 0 ? selectedDc : Lang.SelectHint, dcWidth, () =>
        {
            foreach (var dc in ServerData.AllDatacenters)
            {
                if (ImGui.Selectable(dc, dc == selectedDc))
                {
                    selectedDc = dc;
                    var servers = ServerData.GetServers(dc);
                    selectedServer = servers.Length > 0 ? servers[0] : "";
                }
            }
        });

        ImGui.TextColored(UIConstants.TextSecondary, Lang.Server);
        var srvWidth = ImGui.GetContentRegionAvail().X;
        var dcServers = ServerData.GetServers(selectedDc);
        UIConstants.StyledCombo("##server", selectedServer.Length > 0 ? selectedServer : Lang.SelectHint, srvWidth, () =>
        {
            foreach (var srv in dcServers)
            {
                if (ImGui.Selectable(srv, srv == selectedServer))
                    selectedServer = srv;
            }
        });

        ImGui.TextColored(UIConstants.TextSecondary, Lang.HousingDist);
        if (UIConstants.StyledCombo("##district", Districts, ref districtIndex, ImGui.GetContentRegionAvail().X))
            AutoDetectHouseSizeFromPlot();

        var halfW = (ImGui.GetContentRegionAvail().X - 8) / 2f;
        ImGui.TextColored(UIConstants.TextSecondary, Lang.Ward);
        ImGui.SameLine(halfW + 8);
        ImGui.TextColored(UIConstants.TextSecondary, Lang.Plot);
        ImGui.SetNextItemWidth(halfW);
        ImGui.InputTextWithHint("##ward", "1-30", ref ward, 8);
        ImGui.SameLine(0, 8);
        ImGui.SetNextItemWidth(halfW);
        if (ImGui.InputTextWithHint("##plot", "1-60", ref plot, 8))
            AutoDetectHouseSizeFromPlot();

        ImGui.TextColored(UIConstants.TextSecondary, Lang.HouseSize);
        UIConstants.StyledCombo("##housesize", HouseSizeLabels, ref houseSizeIndex, ImGui.GetContentRegionAvail().X);

        ImGui.Spacing();
        UIConstants.Toggle("##nsfwVenue", ref isNsfw);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(Lang.NsfwVenueTip);
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.TextPrimary, Lang.NsfwVenue);
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.4f), $"({Lang.NsfwUncheckedHint})");

        UIConstants.Toggle("##registerOwnerId", ref registerOwnerId);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(Lang.RegisterOwnerIdTip);
        ImGui.SameLine();
        ImGui.TextColored(UIConstants.TextPrimary, Lang.RegisterOwnerId);

        ImGui.Spacing();
        Field(Lang.Description, ref description, "");

        ImGui.Spacing();
        ImGui.TextColored(UIConstants.TextSecondary, Lang.VenueColors);

        void ColorField(string label, ref Vector3 vec, ref string hex)
        {
            var flags = ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoAlpha | ImGuiColorEditFlags.NoLabel;
            if (ImGui.ColorEdit3($"##{label}Pick", ref vec, flags))
                hex = $"#{(int)(vec.X * 255):x2}{(int)(vec.Y * 255):x2}{(int)(vec.Z * 255):x2}";
            ImGui.SameLine(0, 2);
            ImGui.SetNextItemWidth(60);
            if (ImGui.InputText($"##{label}Hex", ref hex, 8))
            {
                var h = hex.TrimStart('#');
                if (h.Length == 6 && int.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out var c))
                    vec = new Vector3((c >> 16 & 0xFF) / 255f, (c >> 8 & 0xFF) / 255f, (c & 0xFF) / 255f);
            }
            ImGui.SameLine(0, 2);
            ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f), label);
        }

        ColorField(Lang.ColorPrimary, ref colPriVec, ref colorPrimary);
        ImGui.SameLine(0, 8);
        ColorField(Lang.ColorAccent, ref colAccVec, ref colorAccent);
        ImGui.SameLine(0, 8);
        ColorField(Lang.ColorSecondary, ref colSecVec, ref colorSecondary);

        PopFieldStyle();
    }

    private void DrawLinks()
    {
        ImGui.Spacing();
        PushFieldStyle();

        Field("Discord Invite", ref discordLink, "https://discord.gg/...");
        Field("Partake Team", ref partakeLink, "https://partake.gg/t/...");
        Field("FFXIV Venues", ref xivVenuesLink, "https://ffxivvenues.com/...");
        Field("Website", ref websiteLink, "https://...");

        PopFieldStyle();
    }

    private void DrawServices()
    {
        ImGui.Spacing();

        if (UIConstants.AccentButton(Lang.AddService, UIConstants.Glow, width: -1))
            services.Add(new ServiceEntry());

        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.4f),
            Lang.CoordsTip);

        ImGui.Spacing();

        PushFieldStyle();
        ImGui.PushStyleColor(ImGuiCol.Header,        UIConstants.WithAlpha(UIConstants.Primary, 0.18f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered,  UIConstants.WithAlpha(UIConstants.Primary, 0.28f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive,   UIConstants.WithAlpha(UIConstants.Primary, 0.38f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, UIConstants.ChipRounding);
        for (var i = 0; i < services.Count; i++)
        {
            var svc = services[i];
            ImGui.PushID(i);

            var label = svc.Name.Length > 0 ? svc.Name : $"Service #{i + 1}";
            if (ImGui.CollapsingHeader(label, ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.FramePadding))
            {
                ImGui.Indent(4f);
                ImGui.TextColored(UIConstants.TextSecondary, Lang.ServiceType);
                UIConstants.StyledCombo("##type", ServiceTypeLabels, ref svc.TypeIndex, ImGui.GetContentRegionAvail().X);

                Field(Lang.ServiceName, ref svc.Name, Lang.ServiceNameHint);

                ImGui.TextColored(UIConstants.TextSecondary, Lang.Floor);
                UIConstants.StyledCombo("##floor", FloorNameLabels, ref svc.FloorIndex, ImGui.GetContentRegionAvail().X);

                ImGui.TextColored(UIConstants.TextSecondary, Lang.Coordinates);
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.6f);
                ImGui.DragFloat3("##coords", ref svc.Coords, 0.1f);
                ImGui.SameLine();

                if (UIConstants.AccentButton(Lang.UseMyPos, UIConstants.Glow))
                {
                    var pos = plugin.PositionTracker.LastPosition;
                    svc.Coords = new Vector3(pos.X, pos.Z, pos.Y);
                    svc.FloorIndex = pos.Y switch
                    {
                        < -3.5f => 2,
                        > 6.5f  => 1,
                        _       => 0,
                    };
                }

                if (UIConstants.AccentButton($"{Lang.Delete}##del", UIConstants.Danger, width: -1))
                    services.RemoveAt(i);
                ImGui.Unindent(4f);
            }

            ImGui.PopID();
            ImGui.Spacing();
        }
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(3);
        PopFieldStyle();
    }

    private const string GoogleFormId = "1FAIpQLSeXKwEDbHQzjoOFH4o5WLTfd2K7m_KwiKp9kiWAHCxTKcpELg";
    private const string EntryClubName = "67833796";
    private const string EntryDiscordInvite = "555999458";
    private const string EntryServer = "597719214";
    private const string EntryWard = "450797006";
    private const string EntryPlot = "639140243";
    private const string EntryDiscordName = "630718754";
    private const string EntryDistrict = "1416277688";
    private const string EntryDescription = "2104768768";
    private const string EntryColorPrimary = "588634657";
    private const string EntryColorAccent = "2109226808";
    private const string EntryColorSecondary = "2052616594";
    private const string EntryPartakeLink = "511286024";
    private const string EntryXivVenuesLink = "1619807229";
    private const string EntryWebsiteLink = "442188574";
    private const string EntryServicesJson = "1317194279";

    private void DrawExport()
    {
        ImGui.Spacing();
        ImGui.PushTextWrapPos(0);

        var identityKnown = IdentityKnown();
        var valid = clubName.Length > 0 && (identityKnown || discordName.Length > 0) && selectedDc.Length > 0 && selectedServer.Length > 0 && ward.Length > 0 && plot.Length > 0;
        if (!valid)
        {
            ImGui.TextColored(UIConstants.WithAlpha(UIConstants.Danger, 0.8f), Lang.FillRequired);
            ImGui.Spacing();
        }

        ImGui.TextColored(UIConstants.Primary, Lang.OptForm);
        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f),
            identityKnown ? Lang.OptFormDescVerified : Lang.OptFormDesc);
        ImGui.Spacing();

        var formBlue = new Vector4(0.2f, 0.5f, 1f, 1f);
        if (UIConstants.AccentButton(Lang.OpenForm, formBlue, width: -1))
        {
            if (!valid)
            {
                plugin.Toasts.Show(Lang.ToastRequiredFieldsMissing, ToastKind.Warning, 3.0);
            }
            else
            {
                var formUrl = GenerateGoogleFormUrl();
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = formUrl, UseShellExecute = true }); } catch { }
                copied = true; copiedTime = ImGui.GetTime(); copiedWhat = Lang.FormOpened;
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.Glow, identityKnown ? Lang.SendUpdate : Lang.OptDiscord);
        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f),
            identityKnown ? Lang.SendUpdateDesc : Lang.OptDiscordDesc);
        ImGui.Spacing();

        var jsonCopied = copyJsonStart > 0 && (ImGui.GetTime() - copyJsonStart) < 3.0;
        if (jsonCopied)
        {
            UIConstants.AccentButton($"{Lang.Copied}##copyJson", UIConstants.Success, width: -1, disabled: true);
        }
        else if (UIConstants.AccentButton($"{Lang.CopyJson}##copyJson", UIConstants.Primary, width: -1))
        {
            if (!valid)
            {
                plugin.Toasts.Show(Lang.ToastRequiredFieldsMissing, ToastKind.Warning, 3.0);
            }
            else
            {
                ImGui.SetClipboardText(FormatJson());
                copyJsonStart = ImGui.GetTime();
                plugin.Toasts.Show(Lang.ToastJsonCopied, ToastKind.Success, 2.0);
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f), Lang.Preview);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, UIConstants.WithAlpha(UIConstants.CardBackground, 0.3f));
        UIConstants.PushScrollbarStyle();
        try
        {
            if (ImGui.BeginChild("##preview", new Vector2(-1, 140), true))
            {
                ImGui.PushTextWrapPos(0);
                try
                {
                    ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.6f), FormatJson());
                }
                finally
                {
                    ImGui.PopTextWrapPos();
                }
            }
        }
        finally
        {
            ImGui.EndChild();
            UIConstants.PopScrollbarStyle();
            ImGui.PopStyleColor();
        }

        ImGui.PopTextWrapPos();
    }

    private string GenerateGoogleFormUrl()
    {
        var baseUrl = $"https://docs.google.com/forms/d/e/{GoogleFormId}/viewform";
        var p = new List<string>
        {
            $"entry.{EntryClubName}={Uri.EscapeDataString(clubName)}",
            $"entry.{EntryDiscordInvite}={Uri.EscapeDataString(discordLink)}",
            $"entry.{EntryServer}={Uri.EscapeDataString($"{selectedDc} - {selectedServer}")}",
            $"entry.{EntryWard}={Uri.EscapeDataString(ward)}",
            $"entry.{EntryPlot}={Uri.EscapeDataString(plot)}",
            $"entry.{EntryDiscordName}={Uri.EscapeDataString(discordName)}",
            $"entry.{EntryDistrict}={Uri.EscapeDataString(Districts[districtIndex])}",
            $"entry.{EntryDescription}={Uri.EscapeDataString(description)}",
            $"entry.{EntryColorPrimary}={Uri.EscapeDataString(colorPrimary)}",
            $"entry.{EntryColorAccent}={Uri.EscapeDataString(colorAccent)}",
            $"entry.{EntryColorSecondary}={Uri.EscapeDataString(colorSecondary)}",
            $"entry.{EntryPartakeLink}={Uri.EscapeDataString(partakeLink)}",
            $"entry.{EntryXivVenuesLink}={Uri.EscapeDataString(xivVenuesLink)}",
            $"entry.{EntryWebsiteLink}={Uri.EscapeDataString(websiteLink)}",
            $"entry.{EntryServicesJson}={Uri.EscapeDataString(FormatServicesJson())}",
        };
        return $"{baseUrl}?{string.Join("&", p)}";
    }

    private string FormatServicesJson()
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder();
        sb.Append("{");
        sb.Append($"\"nsfw\":{(isNsfw ? "true" : "false")},");
        if (registerOwnerId)
        {
            var ownerIdHash = OwnerIdHelper.ComputeHash(VenueMapperPlugin.PlayerState.ContentId);
            sb.Append($"\"ownerIdHash\":{Newtonsoft.Json.JsonConvert.ToString(ownerIdHash)},");
        }
        sb.Append("\"services\":[");
        for (var i = 0; i < services.Count; i++)
        {
            var s = services[i];
            if (i > 0) sb.Append(",");
            sb.Append($"{{\"type\":\"{ServiceTypes[s.TypeIndex]}\",\"name\":{Newtonsoft.Json.JsonConvert.ToString(s.Name)},\"floor\":\"{FloorNames[s.FloorIndex]}\",\"x\":{s.Coords.X.ToString("F1", ci)},\"y\":{s.Coords.Y.ToString("F1", ci)},\"z\":{s.Coords.Z.ToString("F1", ci)}}}");
        }
        sb.Append("]}");
        return sb.ToString();
    }

    private static void PushFieldStyle()
    {
        ImGui.PushStyleColor(ImGuiCol.FrameBg, UIConstants.WithAlpha(UIConstants.CardBackground, 0.9f));
        ImGui.PushStyleColor(ImGuiCol.Border, UIConstants.GlowDim);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
    }

    private static void PopFieldStyle()
    {
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(2);
    }

    private static void Field(string label, ref string value, string hint)
    {
        ImGui.TextColored(UIConstants.TextSecondary, label);
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint($"##{label}", hint, ref value, 256);
    }

    private static string J(string s) => Newtonsoft.Json.JsonConvert.ToString(s);

    private string FormatJson()
    {
        var isUpdate = loadedVenueId.Length > 0;
        var venueId = isUpdate ? loadedVenueId : clubName.ToLowerInvariant().Replace(" ", "");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"type\": {J(isUpdate ? "update" : "new")},");
        sb.AppendLine($"  \"venueId\": {J(venueId)},");
        if (registerOwnerId)
        {
            var ownerIdHash = OwnerIdHelper.ComputeHash(VenueMapperPlugin.PlayerState.ContentId);
            sb.AppendLine($"  \"ownerIdHash\": {J(ownerIdHash)},");
        }
        sb.AppendLine($"  \"name\": {J(clubName)},");
        sb.AppendLine($"  \"address\": {J($"{selectedDc} - {selectedServer} - {Districts[districtIndex]} - Ward {ward} - Plot {plot}")},");
        sb.AppendLine($"  \"datacenter\": {J(selectedDc)},");
        sb.AppendLine($"  \"server\": {J(selectedServer)},");
        sb.AppendLine($"  \"houseSize\": {J(HouseSizeKeys[houseSizeIndex])},");
        sb.AppendLine($"  \"ward\": {(int.TryParse(ward, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var wardNum) ? wardNum : 0)},");
        sb.AppendLine($"  \"plot\": {(int.TryParse(plot, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var plotNum) ? plotNum : 0)},");
        sb.AppendLine($"  \"nsfw\": {(isNsfw ? "true" : "false")},");
        sb.AppendLine($"  \"description\": {J(description)},");
        sb.AppendLine("  \"availableFloors\": [" + string.Join(", ", services.Select(s => FloorNames[s.FloorIndex]).Distinct().Select(J)) + "],");
        sb.AppendLine("  \"colors\": {");
        sb.AppendLine($"    \"primary\": {J(colorPrimary)},");
        sb.AppendLine($"    \"accent\": {J(colorAccent)},");
        sb.AppendLine($"    \"secondary\": {J(colorSecondary)}");
        sb.AppendLine("  },");
        sb.AppendLine("  \"links\": {");
        sb.AppendLine($"    \"discord\": {J(discordLink)},");
        sb.AppendLine($"    \"partake\": {J(partakeLink)},");
        sb.AppendLine($"    \"ffxivvenues\": {J(xivVenuesLink)},");
        sb.AppendLine($"    \"website\": {J(websiteLink)}");
        sb.AppendLine("  },");
        sb.AppendLine("  \"floors\": [");
        var floors = services.GroupBy(s => FloorNames[s.FloorIndex]);
        var floorList = floors.ToList();
        for (var fi = 0; fi < floorList.Count; fi++)
        {
            var f = floorList[fi];
            sb.AppendLine($"    {{ \"floor\": \"{f.Key}\", \"services\": [");
            var svcs = f.ToList();
            for (var si = 0; si < svcs.Count; si++)
            {
                var s = svcs[si];
                var comma = si < svcs.Count - 1 ? "," : "";
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                sb.AppendLine($"      {{ \"type\": \"{ServiceTypes[s.TypeIndex]}\", \"label\": {Newtonsoft.Json.JsonConvert.ToString(s.Name)}, \"x\": {s.Coords.X.ToString("F2", ci)}, \"y\": {s.Coords.Y.ToString("F2", ci)}, \"z\": {s.Coords.Z.ToString("F2", ci)} }}{comma}");
            }
            var fcomma = fi < floorList.Count - 1 ? "," : "";
            sb.AppendLine($"    ] }}{fcomma}");
        }
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        return sb.ToString();
    }

    public void Dispose() { }

    private class ServiceEntry
    {
        public int TypeIndex;
        public string Name = "";
        public int FloorIndex;
        public Vector3 Coords;
    }
}
