using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using VenueMapper.Models;

namespace VenueMapper.UI;

public class OwnerSubmitWindow : Window, IDisposable
{
    private readonly VenueMapperPlugin plugin;

    private string clubName = "";
    private string discordName = "";
    private string description = "";
    private string selectedDc = "";
    private string selectedServer = "";
    private string ward = "";
    private string plot = "";
    private int districtIndex;

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
    private static readonly string[] ServiceTypes =
        ["bar", "dj_booth", "gambling", "entrance", "upstairs", "downstairs", "vip", "bath", "spa", "event", "stage"];
    private static readonly string[] FloorNames =
        ["ground", "second", "cellar"];

    public OwnerSubmitWindow(VenueMapperPlugin plugin)
        : base("Venue Owner Setup##OwnerSubmit",
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoResize)
    {
        this.plugin = plugin;
        Size = new Vector2(525, 525);
        SizeCondition = ImGuiCond.Always;
    }

    public override void PreDraw()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg, UIConstants.Background);
        ImGui.PushStyleColor(ImGuiCol.TitleBg, UIConstants.WithAlpha(UIConstants.Primary, 0.2f));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, UIConstants.WithAlpha(UIConstants.Primary, 0.3f));
        ImGui.PushStyleColor(ImGuiCol.Border, UIConstants.WithAlpha(UIConstants.Glow, 0.5f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1.5f);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(4);
    }

    public override void Draw()
    {
        ImGui.TextColored(UIConstants.Primary, Lang.OwnerTitle);
        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f),
            Lang.OwnerDesc);
        ImGui.Spacing();

        if (ImGui.BeginTabBar("##ownerTabs"))
        {
            if (ImGui.BeginTabItem(Lang.VenueInfo))
            {
                DrawVenueInfo();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem($"{Lang.Links}##tab_links"))
            {
                DrawLinks();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Services##tab_svc"))
            {
                DrawServices();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem(Lang.Export))
            {
                DrawExport();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }

        if (copied && ImGui.GetTime() - copiedTime < 3.0)
        {
            ImGui.Spacing();
            ImGui.TextColored(UIConstants.Glow, $"{Lang.Copied} {copiedWhat}");
        }
    }

    private void DrawVenueInfo()
    {
        ImGui.Spacing();
        PushFieldStyle();

        Field(Lang.VenueName, ref clubName, "Your Venue Name");
        Field(Lang.YourDiscord, ref discordName, "username");
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.TextSecondary, Lang.Datacenter);
        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##dc", selectedDc.Length > 0 ? selectedDc : Lang.SelectHint))
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
            ImGui.EndCombo();
        }

        ImGui.TextColored(UIConstants.TextSecondary, Lang.Server);
        ImGui.SetNextItemWidth(-1);
        var dcServers = ServerData.GetServers(selectedDc);
        if (ImGui.BeginCombo("##server", selectedServer.Length > 0 ? selectedServer : Lang.SelectHint))
        {
            foreach (var srv in dcServers)
            {
                if (ImGui.Selectable(srv, srv == selectedServer))
                    selectedServer = srv;
            }
            ImGui.EndCombo();
        }

        ImGui.TextColored(UIConstants.TextSecondary, Lang.HousingDist);
        ImGui.SetNextItemWidth(-1);
        ImGui.Combo("##district", ref districtIndex, Districts, Districts.Length);

        var halfW = (ImGui.GetContentRegionAvail().X - 8) / 2f;
        ImGui.TextColored(UIConstants.TextSecondary, Lang.Ward);
        ImGui.SameLine(halfW + 8);
        ImGui.TextColored(UIConstants.TextSecondary, Lang.Plot);
        ImGui.SetNextItemWidth(halfW);
        ImGui.InputTextWithHint("##ward", "1-30", ref ward, 8);
        ImGui.SameLine(0, 8);
        ImGui.SetNextItemWidth(halfW);
        ImGui.InputTextWithHint("##plot", "1-60", ref plot, 8);

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

        ColorField("Primary", ref colPriVec, ref colorPrimary);
        ImGui.SameLine(0, 8);
        ColorField("Accent", ref colAccVec, ref colorAccent);
        ImGui.SameLine(0, 8);
        ColorField("Secondary", ref colSecVec, ref colorSecondary);

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

        ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.WithAlpha(UIConstants.Glow, 0.15f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UIConstants.WithAlpha(UIConstants.Glow, 0.3f));
        ImGui.PushStyleColor(ImGuiCol.Text, UIConstants.Glow);
        if (ImGui.Button(Lang.AddService, new Vector2(-1, 24)))
            services.Add(new ServiceEntry());
        ImGui.PopStyleColor(3);

        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.4f),
            Lang.CoordsTip);

        ImGui.Spacing();

        PushFieldStyle();
        for (var i = 0; i < services.Count; i++)
        {
            var svc = services[i];
            ImGui.PushID(i);

            var label = svc.Name.Length > 0 ? svc.Name : $"Service #{i + 1}";
            if (ImGui.CollapsingHeader(label, ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextColored(UIConstants.TextSecondary, Lang.ServiceType);
                ImGui.SetNextItemWidth(-1);
                ImGui.Combo("##type", ref svc.TypeIndex, ServiceTypes, ServiceTypes.Length);

                Field(Lang.ServiceName, ref svc.Name, "e.g. Main Bar");

                ImGui.TextColored(UIConstants.TextSecondary, Lang.Floor);
                ImGui.SetNextItemWidth(-1);
                ImGui.Combo("##floor", ref svc.FloorIndex, FloorNames, FloorNames.Length);

                ImGui.TextColored(UIConstants.TextSecondary, Lang.Coordinates);
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.6f);
                ImGui.DragFloat3("##coords", ref svc.Coords, 0.1f);
                ImGui.SameLine();

                ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.WithAlpha(UIConstants.Glow, 0.15f));
                ImGui.PushStyleColor(ImGuiCol.Text, UIConstants.Glow);
                if (ImGui.Button(Lang.UseMyPos))
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
                ImGui.PopStyleColor(2);

                ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.WithAlpha(new Vector4(1, 0.2f, 0.2f, 1), 0.2f));
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0.4f, 0.4f, 1));
                if (ImGui.Button($"{Lang.Delete}##del", new Vector2(-1, 20)))
                    services.RemoveAt(i);
                ImGui.PopStyleColor(2);
            }

            ImGui.PopID();
            ImGui.Spacing();
        }
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

        var valid = clubName.Length > 0 && discordName.Length > 0 && selectedDc.Length > 0 && selectedServer.Length > 0 && ward.Length > 0 && plot.Length > 0;
        if (!valid)
        {
            ImGui.TextColored(new Vector4(1, 0.3f, 0.3f, 0.8f), Lang.FillRequired);
            ImGui.Spacing();
        }

        ImGui.TextColored(UIConstants.Primary, Lang.OptForm);
        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f),
            Lang.OptFormDesc);
        ImGui.Spacing();

        if (!valid) ImGui.BeginDisabled();

        ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.WithAlpha(new Vector4(0.2f, 0.5f, 1f, 1f), 0.25f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UIConstants.WithAlpha(new Vector4(0.2f, 0.5f, 1f, 1f), 0.45f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.4f, 0.7f, 1f, 1f));
        if (ImGui.Button(Lang.OpenForm, new Vector2(-1, 28)))
        {
            var formUrl = GenerateGoogleFormUrl();
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                { FileName = formUrl, UseShellExecute = true }); } catch { }
            copied = true; copiedTime = ImGui.GetTime(); copiedWhat = "Form opened";
        }
        ImGui.PopStyleColor(3);

        if (!valid) ImGui.EndDisabled();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.Glow, Lang.OptDiscord);
        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f),
            Lang.OptDiscordDesc);
        ImGui.Spacing();

        if (!valid) ImGui.BeginDisabled();

        var jsonCopied = copyJsonStart > 0 && (ImGui.GetTime() - copyJsonStart) < 3.0;
        if (jsonCopied)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.WithAlpha(new Vector4(0.2f, 1f, 0.5f, 1f), 0.25f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UIConstants.WithAlpha(new Vector4(0.2f, 1f, 0.5f, 1f), 0.35f));
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.2f, 1f, 0.5f, 1f));
            ImGui.Button($"{Lang.Copied}##copyJson", new Vector2(-1, 28));
            ImGui.PopStyleColor(3);
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.WithAlpha(UIConstants.Primary, 0.25f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UIConstants.WithAlpha(UIConstants.Primary, 0.45f));
            ImGui.PushStyleColor(ImGuiCol.Text, UIConstants.Primary);
            if (ImGui.Button("Copy JSON##copyJson", new Vector2(-1, 28)))
            {
                ImGui.SetClipboardText(FormatJson());
                copyJsonStart = ImGui.GetTime();
            }
            ImGui.PopStyleColor(3);
        }

        if (!valid) ImGui.EndDisabled();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f), Lang.Preview);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, UIConstants.WithAlpha(UIConstants.CardBackground, 0.3f));
        if (ImGui.BeginChild("##preview", new Vector2(-1, 140), true))
        {
            ImGui.PushTextWrapPos(0);
            ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.6f), FormatJson());
            ImGui.PopTextWrapPos();
            ImGui.EndChild();
        }
        ImGui.PopStyleColor();

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
        if (services.Count == 0) return "";
        var sb = new System.Text.StringBuilder();
        sb.Append("[");
        for (var i = 0; i < services.Count; i++)
        {
            var s = services[i];
            if (i > 0) sb.Append(",");
            sb.Append($"{{\"type\":\"{ServiceTypes[s.TypeIndex]}\",\"name\":{Newtonsoft.Json.JsonConvert.ToString(s.Name)},\"floor\":\"{FloorNames[s.FloorIndex]}\",\"x\":{s.Coords.X:F1},\"y\":{s.Coords.Y:F1},\"z\":{s.Coords.Z:F1}}}");
        }
        sb.Append("]");
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
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"venueId\": {J(clubName.ToLowerInvariant().Replace(" ", ""))},");
        sb.AppendLine($"  \"name\": {J(clubName)},");
        sb.AppendLine($"  \"address\": {J($"{selectedDc} - {selectedServer} - {Districts[districtIndex]} - Ward {ward} - Plot {plot}")},");
        sb.AppendLine($"  \"datacenter\": {J(selectedDc)},");
        sb.AppendLine($"  \"server\": {J(selectedServer)},");
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
                sb.AppendLine($"      {{ \"type\": \"{ServiceTypes[s.TypeIndex]}\", \"label\": {Newtonsoft.Json.JsonConvert.ToString(s.Name)}, \"x\": {s.Coords.X:F2}, \"y\": {s.Coords.Y:F2}, \"z\": {s.Coords.Z:F2} }}{comma}");
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
