using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SceneObject = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Object;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace MinionScaler;

public sealed unsafe class Plugin : IDalamudPlugin
{
    public const string DisplayName = "Minion Scaler";

    private const string CommandName = "/minionscaler";
    private const string CommandAlias = "/minionscale";
    private const string ConfigCommandName = "/minionscalerconfig";

    [PluginService] private static IDalamudPluginInterface PluginInterface { get; set; } = null!;
    [PluginService] private static ICommandManager CommandManager { get; set; } = null!;
    [PluginService] private static IObjectTable ObjectTable { get; set; } = null!;
    [PluginService] private static IFramework Framework { get; set; } = null!;
    [PluginService] private static IPluginLog Log { get; set; } = null!;

    private readonly Dictionary<ulong, float> originalScales = new();
    private readonly Dictionary<ulong, DrawScale> originalDrawScales = new();
    private readonly Dictionary<string, float> previewScales = new();
    private readonly ConfigWindow configWindow;
    private readonly WindowSystem windowSystem = new("MinionScaler");

    public Configuration Configuration { get; }

    public static string DisplayVersion =>
        typeof(Plugin).Assembly
            .GetCustomAttributes(false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()
            ?.InformationalVersion
            .Split('+')[0]
        ?? typeof(Plugin).Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";

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
            var id = obj.GameObjectId;
            seenThisFrame.Add(id);

            var multiplier = GetScaleForMinion(minion);
            var gameObject = (GameObject*)obj.Address;
            if (Math.Abs(multiplier - 1.0f) < 0.001f)
            {
                RestoreTrackedMinion(id, gameObject);
                continue;
            }

            if (!originalScales.TryGetValue(id, out var originalScale))
            {
                originalScale = gameObject->Scale;
                originalScales[id] = originalScale;
            }

            gameObject->Scale = originalScale * multiplier;
            ApplyDrawObjectScale(id, gameObject, multiplier);
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

    public float GetScaleForMinion(MinionEntry minion)
    {
        if (previewScales.TryGetValue(minion.Key, out var previewScale))
            return Math.Clamp(previewScale, 0.1f, 10.0f);

        return Configuration.MinionScales.TryGetValue(minion.Key, out var setting)
            ? Math.Clamp(setting.Scale, 0.1f, 10.0f)
            : 1.0f;
    }

    public void SetPreviewScale(MinionEntry minion, float scale)
    {
        previewScales[minion.Key] = Math.Clamp(scale, 0.1f, 10.0f);
    }

    public void ResetMinionScale(MinionEntry minion)
    {
        previewScales[minion.Key] = 1.0f;
        Configuration.MinionScales.Remove(minion.Key);
        Save();
    }

    public void SaveMinionScale(MinionEntry minion)
    {
        Configuration.MinionScales[minion.Key] = new MinionScaleSetting
        {
            Key = minion.Key,
            Name = minion.Name,
            Scale = GetScaleForMinion(minion),
        };

        Save();
    }

    public void DeleteMinionScale(string key)
    {
        previewScales[key] = 1.0f;
        if (Configuration.MinionScales.Remove(key))
            Save();
    }

    private static MinionEntry CreateMinionEntry(Dalamud.Game.ClientState.Objects.Types.IGameObject obj)
    {
        var name = obj.Name.ToString();
        if (string.IsNullOrWhiteSpace(name))
            name = $"Minion {obj.BaseId}";

        var key = obj.BaseId != 0
            ? $"data:{obj.BaseId}"
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
                RestoreTrackedMinion(id, gameObject);
            }

            ClearTrackedMinion(id);
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
                RestoreTrackedMinion(id, gameObject);
            }
        }

        originalScales.Clear();
        originalDrawScales.Clear();
    }

    private void ApplyDrawObjectScale(ulong id, GameObject* gameObject, float multiplier)
    {
        var drawObject = gameObject->DrawObject;
        if (drawObject == null)
            return;

        var sceneObject = (SceneObject*)drawObject;
        if (!originalDrawScales.TryGetValue(id, out var originalScale))
        {
            originalScale = DrawScale.From(sceneObject);
            originalDrawScales[id] = originalScale;
        }

        originalScale.ApplyTo(sceneObject, multiplier);
        drawObject->NotifyTransformChanged();
    }

    private void RestoreDrawObjectScale(ulong id, GameObject* gameObject)
    {
        if (!originalDrawScales.TryGetValue(id, out var originalScale))
            return;

        var drawObject = gameObject->DrawObject;
        if (drawObject == null)
            return;

        var sceneObject = (SceneObject*)drawObject;
        originalScale.ApplyTo(sceneObject, 1.0f);
        drawObject->NotifyTransformChanged();
    }

    private void RestoreTrackedMinion(ulong id, GameObject* gameObject)
    {
        if (originalScales.TryGetValue(id, out var originalScale))
            gameObject->Scale = originalScale;

        RestoreDrawObjectScale(id, gameObject);
        ClearTrackedMinion(id);
    }

    private void ClearTrackedMinion(ulong id)
    {
        originalScales.Remove(id);
        originalDrawScales.Remove(id);
    }
}

public sealed record MinionEntry(string Key, string Name);

internal readonly record struct DrawScale(float X, float Y, float Z)
{
    public static unsafe DrawScale From(SceneObject* sceneObject)
    {
        return new DrawScale(sceneObject->Scale.X, sceneObject->Scale.Y, sceneObject->Scale.Z);
    }

    public unsafe void ApplyTo(SceneObject* sceneObject, float multiplier)
    {
        sceneObject->Scale.X = X * multiplier;
        sceneObject->Scale.Y = Y * multiplier;
        sceneObject->Scale.Z = Z * multiplier;
    }
}
