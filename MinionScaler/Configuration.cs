using Dalamud.Configuration;

namespace MinionScaler;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public Dictionary<string, MinionScaleSetting> MinionScales { get; set; } = new();
}

[Serializable]
public sealed class MinionScaleSetting
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = "Unknown minion";

    public float Scale { get; set; } = 1.5f;

    public bool ApplyToAll { get; set; }
}
