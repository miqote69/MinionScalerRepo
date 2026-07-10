using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace MinionScaler;

public sealed unsafe class Plugin : IDalamudPlugin
{
    private const string CommandName = "/minionscaler";
    private const string CommandAlias = "/minionscale";
    private const string ConfigCommandName = "/minionscalerconfig";

    [PluginService] private static IDalamudPluginInterface PluginInterface { get; set; } = null!;
    [PluginService] private static ICommandManager CommandManager { get; set; } = null!;
    [PluginService] private static IObjectTable ObjectTable { get; set; } = null!;
    [PluginService] private static IFramework Framework { get; set; } = null!;
    [PluginService] private static IPluginLog Log { get; set; } = null!;

    private readonly Dictionary<ulong, float> originalScales = new();
    private readonly ConfigWindow configWindow;
    private readonly WindowSystem windowSystem = new("MinionScaler");

    public Configuration Configuration { get; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        configWindow = new ConfigWindow(this);
        windowSystem.AddWindow(configWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Minion Scaler settings.",
        });
        CommandManager.AddHandler(CommandAlias, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Minion Scaler settings.",
        });
        CommandManager.AddHandler(ConfigCommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Minion Scaler settings.",
        });

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(CommandAlias);
        CommandManager.RemoveHandler(ConfigCommandName);

        RestoreTrackedMinions();

        windowSystem.RemoveAllWindows();
        configWindow.Dispose();
    }

    public void Save()
    {
        PluginInterface.SavePluginConfig(Configuration);
    }

    public void ToggleConfigUi()
    {
        configWindow.Toggle();
    }

    private void OnCommand(string command, string args)
    {
        ToggleConfigUi();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        try
        {
            ApplyScaleToVisibleMinions();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to update minion scale.");
        }
    }

    private void ApplyScaleToVisibleMinions()
    {
        var localPlayer = ObjectTable.LocalPlayer;
        var localEntityId = localPlayer?.EntityId ?? 0;
        var seenThisFrame = new HashSet<ulong>();

        foreach (var obj in ObjectTable.CharacterManagerObjects)
        {
            if (obj.ObjectKind != ObjectKind.Companion || obj.Address == nint.Zero || !obj.IsValid())
                continue;

            if (Configuration.OwnMinionOnly && (localPlayer == null || obj.OwnerId != localEntityId))
                continue;

            var minion = CreateMinionEntry(obj);
            if (!Configuration.MinionScales.TryGetValue(minion.Key, out var setting))
                continue;

            var id = obj.GameObjectId;
            var gameObject = (GameObject*)obj.Address;
            var multiplier = Math.Clamp(setting.Scale, 0.1f, 10.0f);

            if (!originalScales.TryGetValue(id, out var originalScale))
            {
                originalScale = gameObject->Scale;
                originalScales[id] = originalScale;
            }

            gameObject->Scale = originalScale * multiplier;
            seenThisFrame.Add(id);
        }

        RestoreNoLongerMatchingMinions(seenThisFrame);
    }

    public IReadOnlyList<MinionEntry> GetVisibleMinions()
    {
        var localPlayer = ObjectTable.LocalPlayer;
        var localEntityId = localPlayer?.EntityId ?? 0;

        return ObjectTable.CharacterManagerObjects
            .Where(obj => obj.ObjectKind == ObjectKind.Companion && obj.Address != nint.Zero && obj.IsValid())
            .Where(obj => !Configuration.OwnMinionOnly || (localPlayer != null && obj.OwnerId == localEntityId))
            .Select(CreateMinionEntry)
            .GroupBy(x => x.Key)
            .Select(group => group.First())
            .OrderBy(x => x.Name)
            .ToArray();
    }

    private static MinionEntry CreateMinionEntry(Dalamud.Game.ClientState.Objects.Types.IGameObject obj)
    {
        var name = obj.Name.ToString();
        if (string.IsNullOrWhiteSpace(name))
            name = $"Minion {obj.DataId}";

        var key = obj.DataId != 0
            ? $"data:{obj.DataId}"
            : $"name:{name}";

        return new MinionEntry(key, name);
    }

    private void RestoreNoLongerMatchingMinions(HashSet<ulong> stillMatching)
    {
        foreach (var (id, originalScale) in originalScales.ToArray())
        {
            if (stillMatching.Contains(id))
                continue;

            var obj = ObjectTable.SearchById(id);
            if (obj != null && obj.Address != nint.Zero && obj.IsValid() && obj.ObjectKind == ObjectKind.Companion)
            {
                var gameObject = (GameObject*)obj.Address;
                gameObject->Scale = originalScale;
            }

            originalScales.Remove(id);
        }
    }

    private void RestoreTrackedMinions()
    {
        foreach (var (id, originalScale) in originalScales.ToArray())
        {
            var obj = ObjectTable.SearchById(id);
            if (obj != null && obj.Address != nint.Zero && obj.IsValid() && obj.ObjectKind == ObjectKind.Companion)
            {
                var gameObject = (GameObject*)obj.Address;
                gameObject->Scale = originalScale;
            }
        }

        originalScales.Clear();
    }
}

public sealed record MinionEntry(string Key, string Name);
