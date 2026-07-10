using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace MinionScaler;

public sealed class ConfigWindow : Window, IDisposable
{
    private static readonly Vector2 IconSize = new(24.0f, 24.0f);

    private readonly Plugin plugin;

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

        ImGui.TextUnformatted("Visible minions");

        var visibleMinions = plugin.GetVisibleMinions();
        if (visibleMinions.Count == 0)
            ImGui.TextDisabled("No matching minions are currently visible.");

        foreach (var minion in visibleMinions)
        {
            ImGui.PushID($"visible-{minion.Key}");

            DrawMinionLabel(minion.Name, minion.IsOwn, minion.IconId);
            DrawScaleControls(minion.Key, minion.Name, minion.IconId, false);

            ImGui.PopID();
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Saved minions");

        if (config.MinionScales.Count == 0)
            ImGui.TextDisabled("Saved minions will appear here.");

        foreach (var setting in config.MinionScales.Values.OrderBy(x => x.Name).ToArray())
        {
            ImGui.PushID($"saved-{setting.Key}");
            var iconId = setting.IconId != 0 ? setting.IconId : plugin.GetIconIdForKey(setting.Key);
            DrawMinionLabel(setting.Name, false, iconId);
            DrawScaleControls(setting.Key, setting.Name, iconId, true);

            ImGui.PopID();
        }
    }

    private void DrawMinionLabel(string name, bool isOwn, uint iconId)
    {
        if (plugin.TryGetIconTexture(iconId, out var icon))
        {
            ImGui.Image(icon.Handle, IconSize);
            ImGui.SameLine();
        }

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(isOwn ? $"{name} (Mine)" : name);
    }

    private void DrawScaleControls(string key, string name, uint iconId, bool isSaved)
    {
        var scale = plugin.GetScaleForKey(key);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Scale");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(240);
        if (ImGui.SliderFloat("##scale-slider", ref scale, 0.1f, 10.0f, "%.2fx"))
        {
            if (isSaved)
                plugin.UpdateSavedMinionScale(key, scale);
            else
                plugin.SetPreviewScale(key, scale);
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(90);
        if (ImGui.InputFloat("##scale-input", ref scale, 0.01f, 0.10f, "%.2f"))
        {
            if (isSaved)
                plugin.UpdateSavedMinionScale(key, scale);
            else
                plugin.SetPreviewScale(key, scale);
        }

        var applyToAll = plugin.GetApplyToAllForKey(key);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Apply");
        ImGui.SameLine();
        if (ImGui.RadioButton("Mine only", !applyToAll))
        {
            if (isSaved)
                plugin.UpdateSavedApplyToAll(key, false);
            else
                plugin.SetPreviewApplyToAll(key, false);
        }

        ImGui.SameLine();
        if (ImGui.RadioButton("Everyone", applyToAll))
        {
            if (isSaved)
                plugin.UpdateSavedApplyToAll(key, true);
            else
                plugin.SetPreviewApplyToAll(key, true);
        }

        if (!isSaved)
        {
            ImGui.SameLine();
            if (ImGui.Button("Save"))
            {
                plugin.SaveMinionScale(key, name, iconId);
            }
        }

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
            ImGui.SameLine();
            if (ImGui.Button("Delete"))
            {
                plugin.DeleteMinionScale(key);
            }
        }
    }
}
