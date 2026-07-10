using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace MinionScaler;

public sealed class ConfigWindow : Window, IDisposable
{
    private static readonly Vector2 IconSize = new(36.0f, 36.0f);

    private readonly Plugin plugin;
    private string nameFilter = string.Empty;

    public ConfigWindow(Plugin plugin)
        : base($"{Plugin.DisplayName} v{Plugin.DisplayVersion}###MinionScalerConfig")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new System.Numerics.Vector2(620, 320),
            MaximumSize = new System.Numerics.Vector2(840, 720),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        var config = plugin.Configuration;
        var availableSize = ImGui.GetContentRegionAvail();
        var itemSpacing = ImGui.GetStyle().ItemSpacing;
        var columnWidth = Math.Max(250.0f, (availableSize.X - itemSpacing.X) * 0.5f);
        var filterWidth = Math.Min(360.0f, availableSize.X);

        ImGui.SetNextItemWidth(filterWidth);
        ImGui.InputText("Filter", ref nameFilter, 128);

        var childHeight = Math.Max(180.0f, ImGui.GetContentRegionAvail().Y - ImGui.GetTextLineHeightWithSpacing());

        ImGui.BeginGroup();

        ImGui.TextUnformatted("Visible minions");

        var visibleMinions = plugin.GetVisibleMinions()
            .Where(minion => !config.MinionScales.ContainsKey(minion.Key))
            .Where(MatchesFilter)
            .ToArray();

        if (ImGui.BeginChild("##visible-minions-scroll", new Vector2(columnWidth, childHeight), false))
        {
            if (visibleMinions.Length == 0)
                ImGui.TextDisabled("No unpinned minions are currently visible.");

            foreach (var minion in visibleMinions)
            {
                ImGui.PushID($"visible-{minion.Key}");

                DrawMinionLabel(minion.Name, minion.IsOwn, minion.IconId, minion.Key);
                DrawScaleControls(minion.Key, minion.Name, minion.IconId, false);
                ImGui.Separator();

                ImGui.PopID();
            }
        }

        ImGui.EndChild();
        ImGui.EndGroup();

        ImGui.SameLine();
        ImGui.BeginGroup();

        ImGui.TextUnformatted("Pinned minions");

        if (ImGui.BeginChild("##pinned-minions-scroll", new Vector2(columnWidth, childHeight), false))
        {
            if (config.MinionScales.Count == 0)
                ImGui.TextDisabled("Pinned minions will appear here.");

            foreach (var setting in config.MinionScales.Values.Where(MatchesFilter).OrderBy(x => x.Name).ToArray())
            {
                ImGui.PushID($"pinned-{setting.Key}");
                var iconId = setting.IconId != 0 ? setting.IconId : plugin.GetIconIdForKey(setting.Key);
                DrawMinionLabel(setting.Name, false, iconId, setting.Key);
                DrawScaleControls(setting.Key, setting.Name, iconId, true);
                ImGui.Separator();

                ImGui.PopID();
            }
        }

        ImGui.EndChild();
        ImGui.EndGroup();
    }

    private bool MatchesFilter(MinionEntry minion)
    {
        return MatchesFilter(minion.Name);
    }

    private bool MatchesFilter(MinionScaleSetting setting)
    {
        return MatchesFilter(setting.Name);
    }

    private bool MatchesFilter(string name)
    {
        return string.IsNullOrWhiteSpace(nameFilter)
            || name.Contains(nameFilter, StringComparison.CurrentCultureIgnoreCase);
    }

    private void DrawMinionLabel(string name, bool isOwn, uint iconId, string key)
    {
        if (plugin.TryGetIconTexture(iconId, out var icon))
        {
            ImGui.Image(icon.Handle, IconSize);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Target this minion");

            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                plugin.TargetClosestMinion(key);

            ImGui.SameLine();
        }

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(isOwn ? $"{name} (Mine)" : name);
    }

    private void DrawScaleControls(string key, string name, uint iconId, bool isSaved)
    {
        var scale = plugin.GetScaleForKey(key);
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var itemSpacing = ImGui.GetStyle().ItemSpacing.X;
        var labelWidth = ImGui.CalcTextSize("Scale").X;
        var inputWidth = 58.0f;
        var sliderWidth = Math.Max(80.0f, availableWidth - labelWidth - inputWidth - (itemSpacing * 3.0f));

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Scale");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(sliderWidth);
        if (ImGui.SliderFloat("##scale-slider", ref scale, 0.1f, 10.0f, "%.2fx"))
        {
            if (isSaved)
                plugin.UpdateSavedMinionScale(key, scale);
            else
                plugin.SetPreviewScale(key, scale);
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(inputWidth);
        if (ImGui.InputFloat("##scale-input", ref scale, 0.0f, 0.0f, "%.2f"))
        {
            if (isSaved)
                plugin.UpdateSavedMinionScale(key, scale);
            else
                plugin.SetPreviewScale(key, scale);
        }

        var applyToAll = plugin.GetApplyToAllForKey(key);
        ImGui.AlignTextToFramePadding();
        if (ImGui.RadioButton("Everyone", applyToAll))
        {
            if (isSaved)
                plugin.UpdateSavedApplyToAll(key, true);
            else
                plugin.SetPreviewApplyToAll(key, true);
        }

        ImGui.SameLine();
        if (ImGui.RadioButton("Mine only", !applyToAll))
        {
            if (isSaved)
                plugin.UpdateSavedApplyToAll(key, false);
            else
                plugin.SetPreviewApplyToAll(key, false);
        }

        if (!isSaved)
        {
            ImGui.SameLine();
            if (ImGui.Button("Pin"))
            {
                plugin.SaveMinionScale(key, name, iconId);
            }
        }

        if (ImGui.GetContentRegionAvail().X < 150.0f)
            ImGui.NewLine();
        else
            ImGui.SameLine();

        if (ImGui.Button("Default"))
        {
            if (isSaved)
                plugin.ResetSavedMinionScale(key);
            else
                plugin.ResetMinionScale(key);
        }

        if (isSaved)
        {
            var deleteText = FontAwesomeIcon.Trash.ToIconString();
            ImGui.NewLine();
            using (plugin.PushIconFont())
            {
                AlignNextItemToRight(ImGui.CalcTextSize(deleteText).X + ImGui.GetStyle().FramePadding.X * 2.0f);

                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.55f, 0.08f, 0.08f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.75f, 0.12f, 0.12f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.95f, 0.18f, 0.18f, 1.0f));
                if (ImGui.Button(deleteText))
                {
                    plugin.DeleteMinionScale(key);
                }

                ImGui.PopStyleColor(3);
            }
        }
    }

    private static void AlignNextItemToRight(float itemWidth)
    {
        var availableWidth = ImGui.GetContentRegionAvail().X;
        if (availableWidth > itemWidth)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + availableWidth - itemWidth);
    }
}
