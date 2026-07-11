using Dalamud.Configuration;

namespace MinionScaler;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public Dictionary<string, MinionScaleSetting> MinionScales { get; set; } = new();

    public UiLanguage UiLanguage { get; set; } = UiLanguage.Automatic;
}

public enum UiLanguage
{
    Automatic,
    English,
    Japanese,
    German,
    French,
}

[Serializable]
public sealed class MinionScaleSetting
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = "Unknown minion";

    public uint IconId { get; set; }

    public float Scale { get; set; } = 1.5f;

    public bool ApplyToAll { get; set; }
}
