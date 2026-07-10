using Dalamud.Interface.Windowing;
using ImGuiNET;

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
            MinimumSize = new System.Numerics.Vector2(320, 140),
            MaximumSize = new System.Numerics.Vector2(640, 360),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        var config = plugin.Configuration;

        var enabled = config.Enabled;
        if (ImGui.Checkbox("Enable", ref enabled))
        {
            config.Enabled = enabled;
            plugin.Save();
        }

        var ownOnly = config.OwnMinionOnly;
        if (ImGui.Checkbox("Only my minion", ref ownOnly))
        {
            config.OwnMinionOnly = ownOnly;
            plugin.Save();
        }

        var scale = config.ScaleMultiplier;
        ImGui.SetNextItemWidth(220);
        if (ImGui.SliderFloat("Scale", ref scale, 0.1f, 10.0f, "%.2fx"))
        {
            config.ScaleMultiplier = scale;
            plugin.Save();
        }

        if (ImGui.Button("Reset"))
        {
            config.ScaleMultiplier = 1.0f;
            plugin.Save();
        }
    }
}
