using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using MinionScaler.Localization;
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
            MaximumSize = new Vector2(840, 10000),
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
        ImGui.InputTextWithHint("##minion-filter", plugin.Localizer.Get(UiTextKey.Filter), ref nameFilter, 128);

        var visibleMinions = plugin.GetVisibleMinions()
            .Where(minion => !config.MinionScales.ContainsKey(minion.Key))
            .Where(MatchesFilter)
            .ToArray();
        var pinnedMinions = plugin.GetPinnedMinions()
            .Where(MatchesFilter)
            .ToArray();

        if (ImGui.BeginTabBar("##minion-tabs"))
        {
            if (ImGui.BeginTabItem($"{plugin.Localizer.Get(UiTextKey.Visible)} ({visibleMinions.Length})###visible-minions-tab"))
            {
                DrawVisibleMinions(visibleMinions);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem($"{plugin.Localizer.Get(UiTextKey.Pinned)} ({pinnedMinions.Length})###pinned-minions-tab"))
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

        if (ImGui.MenuItem($"{plugin.Localizer.Get(UiTextKey.Minions)}###menu-minions", string.Empty, currentView == ViewMode.Minions))
            currentView = ViewMode.Minions;

        if (ImGui.MenuItem($"{plugin.Localizer.Get(UiTextKey.Settings)}###menu-settings", string.Empty, currentView == ViewMode.Settings))
            currentView = ViewMode.Settings;

        ImGui.EndMenuBar();
    }

    private void DrawVisibleMinions(IReadOnlyList<MinionEntry> minions)
    {
        if (ImGui.BeginChild("##visible-minions-scroll", Vector2.Zero, false))
        {
            if (minions.Count == 0)
                ImGui.TextDisabled(plugin.Localizer.Get(UiTextKey.NoVisibleMinions));

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

    private void DrawPinnedMinions(IReadOnlyList<MinionEntry> settings)
    {
        if (ImGui.BeginChild("##pinned-minions-scroll", Vector2.Zero, false))
        {
            if (settings.Count == 0)
                ImGui.TextDisabled(plugin.Localizer.Get(UiTextKey.NoPinnedMinions));

            foreach (var minion in settings)
            {
                ImGui.PushID($"pinned-{minion.Key}");
                DrawMinionRow(minion.Key, minion.Name, false, minion.IconId, true);
                ImGui.Separator();
                ImGui.PopID();
            }
        }

        ImGui.EndChild();
    }

    private void DrawSettings()
    {
        DrawUiLanguageSelector();
        ImGui.TextDisabled(plugin.Localizer.Get(UiTextKey.GameDataLanguage, plugin.ClientLanguage));
        ImGui.Separator();

        var hasPinned = plugin.Configuration.MinionScales.Count > 0;

        if (!hasPinned)
            ImGui.BeginDisabled();

        if (ImGui.Button($"{plugin.Localizer.Get(UiTextKey.ResetAllPinned)}###reset-all-pinned"))
            plugin.ResetAllSavedMinionScales();

        ImGui.SameLine();
        PushDangerButtonColors();
        if (ImGui.Button($"{plugin.Localizer.Get(UiTextKey.DeleteAllPinned)}###delete-all-pinned"))
            plugin.DeleteAllMinionScales();
        ImGui.PopStyleColor(3);

        if (!hasPinned)
            ImGui.EndDisabled();
    }

    private void DrawUiLanguageSelector()
    {
        var current = plugin.Configuration.UiLanguage;
        ImGui.SetNextItemWidth(220.0f);
        if (!ImGui.BeginCombo($"{plugin.Localizer.Get(UiTextKey.UiLanguage)}###ui-language-combo", plugin.Localizer.GetLanguageName(current)))
            return;

        foreach (var language in Enum.GetValues<UiLanguage>())
        {
            if (ImGui.Selectable($"{plugin.Localizer.GetLanguageName(language)}###ui-language-{language}", current == language))
                plugin.SetUiLanguage(language);
        }

        ImGui.EndCombo();
    }

    private bool MatchesFilter(MinionEntry minion)
    {
        return MatchesFilter(minion.Name);
    }

    private bool MatchesFilter(string name)
    {
        return plugin.MinionNameContains(name, nameFilter);
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
            ImGui.Image(icon!.Handle, IconSize);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(plugin.Localizer.Get(UiTextKey.TargetThisMinion));

            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                plugin.TargetClosestMinion(key);

            ImGui.SameLine();
        }

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(isOwn ? $"{name} ({plugin.Localizer.Get(UiTextKey.Mine)})" : name);

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
        var imguiStyle = ImGui.GetStyle();
        var itemSpacing = imguiStyle.ItemSpacing.X;
        var scaleText = plugin.Localizer.Get(UiTextKey.Scale);
        var labelWidth = ImGui.CalcTextSize(scaleText).X;
        var inputWidth = 58.0f;
        var defaultButtonWidth = ImGui.CalcTextSize("\u21ba").X + (imguiStyle.FramePadding.X * 2.0f);
        var scrollbarPadding = imguiStyle.ScrollbarSize + itemSpacing;
        var sliderWidth = Math.Max(80.0f, availableWidth - labelWidth - inputWidth - defaultButtonWidth - scrollbarPadding - (itemSpacing * 4.0f));

        using var style = modified ? new ScaleHighlightScope() : null;

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(scaleText);
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

        ImGui.SameLine();
        DrawDefaultButton(key, isSaved);

        var applyToAll = plugin.GetApplyToAllForKey(key);
        ImGui.AlignTextToFramePadding();
        if (ImGui.RadioButton($"{plugin.Localizer.Get(UiTextKey.Everyone)}###apply-everyone", applyToAll))
        {
            if (isSaved)
                plugin.UpdateSavedApplyToAll(key, true);
            else
                plugin.SetPreviewApplyToAll(key, true);
        }

        ImGui.SameLine();
        if (ImGui.RadioButton($"{plugin.Localizer.Get(UiTextKey.MineOnly)}###apply-mine-only", !applyToAll))
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
            ImGui.SetTooltip(plugin.Localizer.Get(UiTextKey.Default));
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
            ImGui.SetTooltip(plugin.Localizer.Get(UiTextKey.PinThisMinion));
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
            ImGui.SetTooltip(plugin.Localizer.Get(UiTextKey.Delete));
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
