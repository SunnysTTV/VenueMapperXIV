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
        selectedVersion = version ?? (ChangelogData.Versions.Length > 0 ? ChangelogData.Versions[0].Ver : "");
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
                var lastDate = "";
                foreach (var (ver, date) in ChangelogData.Versions)
                {
                    if (ver == ChangelogData.Versions[0].Ver) continue;
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
            ImGui.TextColored(UIConstants.Primary, selectedVersion);
            ImGui.Separator();
            ImGui.Spacing();

            if (ChangelogData.Changelogs.TryGetValue(selectedVersion, out var changes))
                foreach (var c in changes) ImGui.BulletText(c);
            else
                ImGui.TextColored(UIConstants.WithAlpha(UIConstants.TextSecondary, 0.5f), "No changelog.");

            ImGui.EndTable();
        }
    }

    public void Dispose() { }
}
