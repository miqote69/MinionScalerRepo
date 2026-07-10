using Dalamud.Configuration;

namespace MinionScaler;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = true;

    public bool OwnMinionOnly { get; set; } = true;

    public float ScaleMultiplier { get; set; } = 1.5f;
}
