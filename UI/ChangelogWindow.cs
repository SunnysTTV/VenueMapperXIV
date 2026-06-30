using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace VenueMapper.UI;

public class ChangelogWindow : Window, IDisposable
{
    private string selectedVersion = "";

    public ChangelogWindow()
        : base("VenueMapper Changelog##ChangelogModal",
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoResize)
    {
        Size = new Vector2(500, 500);
        SizeCondition = ImGuiCond.Always;
    }

    public void Open(string? version = null)
    {
        selectedVersion = version ?? (ChangelogData.Versions.Length > 1 ? ChangelogData.Versions[1].Ver :
                         ChangelogData.Versions.Length > 0 ? ChangelogData.Versions[0].Ver : "");
        IsOpen = true;
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
        if (ImGui.BeginTable("##clLayout", 2, ImGuiTableFlags.BordersInnerV))
        {
            ImGui.TableSetupColumn("##clVersions", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn("##clDetails");

            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            if (ImGui.BeginChild("##clVerScroll", new Vector2(-1, -1)))
            {
                var curVer = ChangelogData.Versions.Length > 0 ? ChangelogData.Versions[0].Ver : "";
                if (ChangelogData.Versions.Length > 0)
                {
                    ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f), ChangelogData.Versions[0].Date);
                    ImGui.Separator();
                }
                var curSel = selectedVersion == curVer;
                ImGui.PushStyleColor(ImGuiCol.Text,          curSel ? UIConstants.Glow : UIConstants.Primary);
                ImGui.PushStyleColor(ImGuiCol.Header,        UIConstants.WithAlpha(UIConstants.Primary, 0.15f));
                ImGui.PushStyleColor(ImGuiCol.HeaderHovered, UIConstants.WithAlpha(UIConstants.Primary, 0.22f));
                if (ImGui.Selectable(curVer, curSel))
                    selectedVersion = curVer;
                ImGui.PopStyleColor(3);
                ImGui.TextColored(UIConstants.WithAlpha(UIConstants.Primary, 0.45f), Lang.CurrentTag);
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                var lastDate = "";
                foreach (var (ver, date) in ChangelogData.Versions)
                {
                    if (ver == curVer) continue;
                    if (date != lastDate)
                    {
                        if (lastDate.Length > 0) ImGui.Spacing();
                        ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f), date);
                        ImGui.Separator();
                        lastDate = date;
                    }
                    var sel = selectedVersion == ver;
                    var col = sel ? UIConstants.Glow : UIConstants.TextPrimary;
                    ImGui.PushStyleColor(ImGuiCol.Text, col);
                    if (ImGui.Selectable(ver, sel))
                        selectedVersion = ver;
                    ImGui.PopStyleColor();
                }
                ImGui.EndChild();
            }

            ImGui.TableSetColumnIndex(1);
            var isCur = ChangelogData.Versions.Length > 0 && selectedVersion == ChangelogData.Versions[0].Ver;
            ImGui.TextColored(UIConstants.Primary, selectedVersion);
            if (isCur)
            {
                ImGui.SameLine(0, 6);
                ImGui.TextColored(UIConstants.WithAlpha(UIConstants.Glow, 0.55f), Lang.CurRelease);
            }
            ImGui.Separator();
            ImGui.Spacing();

            if (ChangelogData.Changelogs.TryGetValue(selectedVersion, out var sections))
                UIConstants.DrawChangelog(sections);
            else
                ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f), Lang.NoChangelog);

            ImGui.EndTable();
        }
    }

    public void Dispose() { }
}
