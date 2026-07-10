using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace MinionScaler;

public sealed class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public ConfigWindow(Plugin plugin)
        : base("Minion Scaler###MinionScalerConfig")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new System.Numerics.Vector2(420, 260),
            MaximumSize = new System.Numerics.Vector2(760, 620),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        var config = plugin.Configuration;

        var ownOnly = config.OwnMinionOnly;
        if (ImGui.Checkbox("Only my minion", ref ownOnly))
        {
            config.OwnMinionOnly = ownOnly;
            plugin.Save();
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Visible minions");

        var visibleMinions = plugin.GetVisibleMinions();
        if (visibleMinions.Count == 0)
            ImGui.TextDisabled("No matching minions are currently visible.");

        foreach (var minion in visibleMinions)
        {
            ImGui.PushID($"visible-{minion.Key}");

            var configured = config.MinionScales.TryGetValue(minion.Key, out var setting);
            ImGui.TextUnformatted(minion.Name);
            ImGui.SameLine();

            if (configured)
            {
                ImGui.TextDisabled("Configured");
            }
            else if (ImGui.Button("Add"))
            {
                config.MinionScales[minion.Key] = new MinionScaleSetting
                {
                    Key = minion.Key,
                    Name = minion.Name,
                    Scale = 1.5f,
                };
                plugin.Save();
            }

            if (configured && setting != null)
            {
                var scale = setting.Scale;
                ImGui.SetNextItemWidth(220);
                if (ImGui.SliderFloat("Scale", ref scale, 0.1f, 10.0f, "%.2fx"))
                {
                    setting.Scale = scale;
                    setting.Name = minion.Name;
                    plugin.Save();
                }
            }

            ImGui.PopID();
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Configured minions");

        if (config.MinionScales.Count == 0)
            ImGui.TextDisabled("Add a visible minion to configure it.");

        foreach (var setting in config.MinionScales.Values.OrderBy(x => x.Name).ToArray())
        {
            ImGui.PushID($"configured-{setting.Key}");

            ImGui.TextUnformatted(setting.Name);
            var scale = setting.Scale;
            ImGui.SetNextItemWidth(220);
            if (ImGui.SliderFloat("Scale", ref scale, 0.1f, 10.0f, "%.2fx"))
            {
                setting.Scale = scale;
                plugin.Save();
            }

            ImGui.SameLine();
            if (ImGui.Button("Reset"))
            {
                setting.Scale = 1.0f;
                plugin.Save();
            }

            ImGui.SameLine();
            if (ImGui.Button("Remove"))
            {
                config.MinionScales.Remove(setting.Key);
                plugin.Save();
            }

            ImGui.PopID();
        }
    }
}
