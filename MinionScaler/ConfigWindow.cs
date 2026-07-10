using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace MinionScaler;

public sealed class ConfigWindow : Window, IDisposable
{
    private static readonly Vector2 IconSize = new(36.0f, 36.0f);
    private static readonly Vector4 AccentColor = new(0.42f, 0.78f, 1.0f, 1.0f);
    private static readonly Vector4 AccentFrameColor = new(0.12f, 0.28f, 0.38f, 1.0f);

    private readonly Plugin plugin;
    private string nameFilter = string.Empty;
    private ViewMode currentView = ViewMode.Minions;

    public ConfigWindow(Plugin plugin)
        : base($"{Plugin.DisplayName} v{Plugin.DisplayVersion}###MinionScalerConfig")
    {
        this.plugin = plugin;
        Flags |= ImGuiWindowFlags.MenuBar;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 320),
            MaximumSize = new Vector2(840, 720),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        DrawMenuBar();

        if (currentView == ViewMode.Settings)
        {
            DrawSettings();
            return;
        }

        var config = plugin.Configuration;
        var availableSize = ImGui.GetContentRegionAvail();
        var filterWidth = Math.Min(360.0f, availableSize.X);

        ImGui.SetNextItemWidth(filterWidth);
        ImGui.InputTextWithHint("##minion-filter", "Filter", ref nameFilter, 128);

        var visibleMinions = plugin.GetVisibleMinions()
            .Where(minion => !config.MinionScales.ContainsKey(minion.Key))
            .Where(MatchesFilter)
            .ToArray();
        var pinnedMinions = config.MinionScales.Values
            .Where(MatchesFilter)
            .OrderBy(x => x.Name)
            .ToArray();

        if (ImGui.BeginTabBar("##minion-tabs"))
        {
            if (ImGui.BeginTabItem($"Visible ({visibleMinions.Length})"))
            {
                DrawVisibleMinions(visibleMinions);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem($"Pinned ({pinnedMinions.Length})"))
            {
                DrawPinnedMinions(pinnedMinions);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawMenuBar()
    {
        if (!ImGui.BeginMenuBar())
            return;

        if (ImGui.MenuItem("Minions", string.Empty, currentView == ViewMode.Minions))
            currentView = ViewMode.Minions;

        if (ImGui.MenuItem("Settings", string.Empty, currentView == ViewMode.Settings))
            currentView = ViewMode.Settings;

        ImGui.EndMenuBar();
    }

    private void DrawVisibleMinions(IReadOnlyList<MinionEntry> minions)
    {
        if (ImGui.BeginChild("##visible-minions-scroll", Vector2.Zero, false))
        {
            if (minions.Count == 0)
                ImGui.TextDisabled("No unpinned minions are currently visible.");

            foreach (var minion in minions)
            {
                ImGui.PushID($"visible-{minion.Key}");
                DrawMinionRow(minion.Key, minion.Name, minion.IsOwn, minion.IconId, false);
                ImGui.Separator();
                ImGui.PopID();
            }
        }

        ImGui.EndChild();
    }

    private void DrawPinnedMinions(IReadOnlyList<MinionScaleSetting> settings)
    {
        if (ImGui.BeginChild("##pinned-minions-scroll", Vector2.Zero, false))
        {
            if (settings.Count == 0)
                ImGui.TextDisabled("Pinned minions will appear here.");

            foreach (var setting in settings)
            {
                ImGui.PushID($"pinned-{setting.Key}");
                var iconId = setting.IconId != 0 ? setting.IconId : plugin.GetIconIdForKey(setting.Key);
                DrawMinionRow(setting.Key, setting.Name, false, iconId, true);
                ImGui.Separator();
                ImGui.PopID();
            }
        }

        ImGui.EndChild();
    }

    private void DrawSettings()
    {
        var hasPinned = plugin.Configuration.MinionScales.Count > 0;

        if (!hasPinned)
            ImGui.BeginDisabled();

        if (ImGui.Button("Reset all pinned"))
            plugin.ResetAllSavedMinionScales();

        ImGui.SameLine();
        PushDangerButtonColors();
        if (ImGui.Button("Delete all pinned"))
            plugin.DeleteAllMinionScales();
        ImGui.PopStyleColor(3);

        if (!hasPinned)
            ImGui.EndDisabled();
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

    private void DrawMinionRow(string key, string name, bool isOwn, uint iconId, bool isSaved)
    {
        DrawMinionHeader(key, name, isOwn, iconId, isSaved);
        DrawScaleControls(key, isSaved);
    }

    private void DrawMinionHeader(string key, string name, bool isOwn, uint iconId, bool isSaved)
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

        var buttonWidth = GetHeaderButtonWidth(isSaved);
        AlignNextItemToRight(buttonWidth);
        DrawDefaultButton(key, isSaved);

        ImGui.SameLine();
        if (isSaved)
            DrawDeleteButton(key);
        else
            DrawPinButton(key, name, iconId);
    }

    private void DrawScaleControls(string key, bool isSaved)
    {
        var scale = plugin.GetScaleForKey(key);
        var modified = plugin.IsScaleModified(key);
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var itemSpacing = ImGui.GetStyle().ItemSpacing.X;
        var labelWidth = ImGui.CalcTextSize("Scale").X;
        var inputWidth = 58.0f;
        var sliderWidth = Math.Max(80.0f, availableWidth - labelWidth - inputWidth - (itemSpacing * 3.0f));

        using var style = modified ? new ScaleHighlightScope() : null;

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
    }

    private void DrawDefaultButton(string key, bool isSaved)
    {
        if (ImGui.Button("\u21ba"))
        {
            if (isSaved)
                plugin.ResetSavedMinionScale(key);
            else
                plugin.ResetMinionScale(key);
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Default");
    }

    private void DrawPinButton(string key, string name, uint iconId)
    {
        const string pinText = "\uf08d";
        using (plugin.PushIconFont())
        {
            if (ImGui.Button(pinText))
                plugin.SaveMinionScale(key, name, iconId);
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Pin this minion");
    }

    private void DrawDeleteButton(string key)
    {
        var deleteText = FontAwesomeIcon.Trash.ToIconString();
        using (plugin.PushIconFont())
        {
            PushDangerButtonColors();
            if (ImGui.Button(deleteText))
                plugin.DeleteMinionScale(key);
            ImGui.PopStyleColor(3);
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Delete");
    }

    private static float GetHeaderButtonWidth(bool isSaved)
    {
        var style = ImGui.GetStyle();
        var defaultWidth = ImGui.CalcTextSize("\u21ba").X + style.FramePadding.X * 2.0f;
        var actionText = isSaved ? FontAwesomeIcon.Trash.ToIconString() : "\uf08d";
        var actionWidth = ImGui.CalcTextSize(actionText).X + style.FramePadding.X * 2.0f;

        return defaultWidth + actionWidth + style.ItemSpacing.X;
    }

    private static void AlignNextItemToRight(float itemWidth)
    {
        var availableWidth = ImGui.GetContentRegionAvail().X;
        if (availableWidth > itemWidth)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + availableWidth - itemWidth);
        else
            ImGui.NewLine();
    }

    private static void PushDangerButtonColors()
    {
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.55f, 0.08f, 0.08f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.75f, 0.12f, 0.12f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.95f, 0.18f, 0.18f, 1.0f));
    }

    private sealed class ScaleHighlightScope : IDisposable
    {
        public ScaleHighlightScope()
        {
            ImGui.PushStyleColor(ImGuiCol.Text, AccentColor);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, AccentFrameColor);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.16f, 0.34f, 0.48f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.18f, 0.40f, 0.56f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.SliderGrab, AccentColor);
            ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, new Vector4(0.62f, 0.88f, 1.0f, 1.0f));
        }

        public void Dispose()
        {
            ImGui.PopStyleColor(6);
        }
    }

    private enum ViewMode
    {
        Minions,
        Settings,
    }
}
