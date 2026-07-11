using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using MinionScaler.Localization;
using System.Linq.Expressions;
using System.Reflection;
using System.Numerics;
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
    [PluginService] private static ITargetManager TargetManager { get; set; } = null!;
    [PluginService] private static IFramework Framework { get; set; } = null!;
    [PluginService] private static IClientState ClientState { get; set; } = null!;
    [PluginService] private static IDataManager DataManager { get; set; } = null!;
    [PluginService] private static ITextureProvider TextureProvider { get; set; } = null!;
    [PluginService] private static IPluginLog Log { get; set; } = null!;

    private readonly Dictionary<ulong, TrackedScale> trackedScales = new();
    private readonly Dictionary<string, float> previewScales = new();
    private readonly Dictionary<string, bool> previewApplyToAll = new();
    private readonly Dictionary<uint, uint> iconIdsByCompanionId = new();
    private readonly Dictionary<(ClientLanguage Language, uint CompanionId), string> namesByLanguageAndCompanionId = new();
    private readonly List<ClientStateSubscription> clientStateSubscriptions = new();
    private readonly ConfigWindow configWindow;
    private readonly WindowSystem windowSystem = new("MinionScaler");

    public Configuration Configuration { get; }

    public Localizer Localizer { get; }

    public ClientLanguage ClientLanguage => ClientState.ClientLanguage;

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
        Localizer = new Localizer(Configuration, ClientState);

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

        SubscribeClientStateEvent("ZoneInit");
        SubscribeClientStateEvent("TerritoryChanged");
        SubscribeClientStateEvent("Logout");
    }

    public void Dispose()
    {
        UnsubscribeClientStateEvents();
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

    public void SetUiLanguage(UiLanguage language)
    {
        if (Configuration.UiLanguage == language)
            return;

        Configuration.UiLanguage = language;
        Save();
    }

    public void ToggleConfigUi()
    {
        configWindow.Toggle();
    }

    public IDisposable PushIconFont()
    {
        return PluginInterface.UiBuilder.IconFontFixedWidthHandle.Push();
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
        var hasLocalPlayer = localPlayer != null;
        var localEntityId = hasLocalPlayer ? GetObjectIdPart(localPlayer!.EntityId) : 0;
        var localGameObjectId = hasLocalPlayer ? GetObjectIdPart(localPlayer!.GameObjectId) : 0;
        var seenThisFrame = new HashSet<ulong>();

        foreach (var obj in ObjectTable.CharacterManagerObjects)
        {
            if (obj.ObjectKind != ObjectKind.Companion || obj.Address == nint.Zero || !obj.IsValid())
                continue;

            var isOwn = IsOwnedByLocalPlayer(obj, hasLocalPlayer, localEntityId, localGameObjectId);
            var minion = CreateMinionEntry(obj, isOwn);
            var id = obj.GameObjectId;
            seenThisFrame.Add(id);

            var gameObject = (GameObject*)obj.Address;
            var drawObject = gameObject->DrawObject;
            if (drawObject == null)
            {
                RestoreTrackedMinion(id, gameObject);
                continue;
            }

            if (!ShouldApplyScale(minion.Key, isOwn))
            {
                RestoreTrackedMinion(id, gameObject);
                continue;
            }

            var multiplier = GetScaleForKey(minion.Key);
            if (Math.Abs(multiplier - 1.0f) < 0.001f)
            {
                RestoreTrackedMinion(id, gameObject);
                continue;
            }

            var gameObjectAddress = obj.Address;
            var drawObjectAddress = (nint)drawObject;
            var sceneObject = (SceneObject*)drawObject;

            if (trackedScales.TryGetValue(id, out var trackedScale)
                && (trackedScale.GameObjectAddress != gameObjectAddress || trackedScale.DrawObjectAddress != drawObjectAddress))
            {
                trackedScales.Remove(id);
            }

            if (!trackedScales.TryGetValue(id, out trackedScale))
            {
                trackedScale = new TrackedScale(
                    id,
                    gameObjectAddress,
                    drawObjectAddress,
                    gameObject->Scale,
                    DrawScale.From(sceneObject));
                trackedScales[id] = trackedScale;
            }

            gameObject->Scale = trackedScale.GameScale * multiplier;
            trackedScale.DrawScale.ApplyTo(sceneObject, multiplier);
            drawObject->NotifyTransformChanged();
        }

        RestoreNoLongerMatchingMinions(seenThisFrame);
    }

    public IReadOnlyList<MinionEntry> GetVisibleMinions()
    {
        var localPlayer = ObjectTable.LocalPlayer;
        var hasLocalPlayer = localPlayer != null;
        var localEntityId = hasLocalPlayer ? GetObjectIdPart(localPlayer!.EntityId) : 0;
        var localGameObjectId = hasLocalPlayer ? GetObjectIdPart(localPlayer!.GameObjectId) : 0;

        return ObjectTable.CharacterManagerObjects
            .Where(IsTargetableCompanion)
            .Select(obj => CreateMinionEntry(obj, IsOwnedByLocalPlayer(obj, hasLocalPlayer, localEntityId, localGameObjectId)))
            .GroupBy(x => x.Key)
            .Select(group =>
            {
                var ownEntry = group.FirstOrDefault(x => x.IsOwn);
                var entry = ownEntry ?? group.First();
                return entry with { IsOwn = ownEntry != null };
            })
            .OrderByDescending(x => x.IsOwn)
            .ThenBy(x => x.Name, GameTextComparison.GetComparer(ClientState.ClientLanguage))
            .ToArray();
    }

    public IReadOnlyList<MinionEntry> GetPinnedMinions()
    {
        return Configuration.MinionScales.Values
            .Select(setting =>
            {
                var companionId = TryGetCompanionId(setting.Key, out var parsedCompanionId) ? parsedCompanionId : 0;
                var iconId = setting.IconId != 0 ? setting.IconId : GetIconIdForKey(setting.Key);
                var name = ResolveMinionName(setting.Key, null, setting.Name);
                return new MinionEntry(setting.Key, companionId, name, false, iconId);
            })
            .OrderBy(x => x.Name, GameTextComparison.GetComparer(ClientState.ClientLanguage))
            .ToArray();
    }

    public bool MinionNameContains(string name, string filter)
    {
        return string.IsNullOrWhiteSpace(filter)
            || GameTextComparison.Contains(name, filter, ClientState.ClientLanguage);
    }

    public void TargetClosestMinion(string key)
    {
        var localPlayer = ObjectTable.LocalPlayer;
        var closest = ObjectTable.CharacterManagerObjects
            .Where(IsTargetableCompanion)
            .Where(obj => CreateMinionKey(obj) == key)
            .OrderBy(obj => localPlayer == null ? 0.0f : Vector3.DistanceSquared(localPlayer.Position, obj.Position))
            .FirstOrDefault();

        if (closest != null)
            TargetManager.Target = closest;
    }

    public float GetScaleForMinion(MinionEntry minion)
    {
        return GetScaleForKey(minion.Key);
    }

    public float GetScaleForKey(string key)
    {
        if (previewScales.TryGetValue(key, out var previewScale))
            return Math.Clamp(previewScale, 0.1f, 10.0f);

        return Configuration.MinionScales.TryGetValue(key, out var setting)
            ? Math.Clamp(setting.Scale, 0.1f, 10.0f)
            : 1.0f;
    }

    public void SetPreviewScale(MinionEntry minion, float scale)
    {
        SetPreviewScale(minion.Key, scale);
    }

    public void SetPreviewScale(string key, float scale)
    {
        previewScales[key] = Math.Clamp(scale, 0.1f, 10.0f);
    }

    public void UpdateSavedMinionScale(string key, float scale)
    {
        scale = Math.Clamp(scale, 0.1f, 10.0f);
        previewScales[key] = scale;

        if (Configuration.MinionScales.TryGetValue(key, out var setting))
        {
            setting.Scale = scale;
            Save();
        }
    }

    public bool GetApplyToAllForMinion(MinionEntry minion)
    {
        return GetApplyToAllForKey(minion.Key);
    }

    public bool GetApplyToAllForKey(string key)
    {
        if (previewApplyToAll.TryGetValue(key, out var applyToAll))
            return applyToAll;

        return !Configuration.MinionScales.TryGetValue(key, out var setting) || setting.ApplyToAll;
    }

    public void SetPreviewApplyToAll(MinionEntry minion, bool applyToAll)
    {
        SetPreviewApplyToAll(minion.Key, applyToAll);
    }

    public void SetPreviewApplyToAll(string key, bool applyToAll)
    {
        previewApplyToAll[key] = applyToAll;
    }

    public void UpdateSavedApplyToAll(string key, bool applyToAll)
    {
        previewApplyToAll[key] = applyToAll;

        if (Configuration.MinionScales.TryGetValue(key, out var setting))
        {
            setting.ApplyToAll = applyToAll;
            Save();
        }
    }

    public void ResetMinionScale(MinionEntry minion)
    {
        ResetMinionScale(minion.Key);
    }

    public void ResetMinionScale(string key)
    {
        previewScales[key] = 1.0f;
        previewApplyToAll.Remove(key);
        Configuration.MinionScales.Remove(key);
        Save();
    }

    public void ResetSavedMinionScale(string key)
    {
        previewScales[key] = 1.0f;

        if (Configuration.MinionScales.TryGetValue(key, out var setting))
        {
            setting.Scale = 1.0f;
            Save();
        }
    }

    public void ResetAllSavedMinionScales()
    {
        foreach (var setting in Configuration.MinionScales.Values)
        {
            setting.Scale = 1.0f;
            previewScales[setting.Key] = 1.0f;
        }

        Save();
    }

    public void SaveMinionScale(MinionEntry minion)
    {
        SaveMinionScale(minion.Key, minion.Name, minion.IconId);
    }

    public void SaveMinionScale(string key, string name, uint iconId = 0)
    {
        Configuration.MinionScales[key] = new MinionScaleSetting
        {
            Key = key,
            Name = name,
            IconId = iconId != 0 ? iconId : GetIconIdForKey(key),
            Scale = GetScaleForKey(key),
            ApplyToAll = GetApplyToAllForKey(key),
        };

        Save();
    }

    public void DeleteMinionScale(string key)
    {
        previewScales[key] = 1.0f;
        previewApplyToAll.Remove(key);
        if (Configuration.MinionScales.Remove(key))
            Save();
    }

    public void DeleteAllMinionScales()
    {
        foreach (var key in Configuration.MinionScales.Keys.ToArray())
        {
            previewScales[key] = 1.0f;
            previewApplyToAll.Remove(key);
        }

        Configuration.MinionScales.Clear();
        Save();
    }

    public bool IsScaleModified(string key)
    {
        return Math.Abs(GetScaleForKey(key) - 1.0f) > 0.001f;
    }

    private bool ShouldApplyScale(string key, bool isOwn)
    {
        if (!previewScales.ContainsKey(key) && !Configuration.MinionScales.ContainsKey(key))
            return false;

        return isOwn || GetApplyToAllForKey(key);
    }

    private static bool IsTargetableCompanion(IGameObject obj)
    {
        return obj.ObjectKind == ObjectKind.Companion
            && obj.Address != nint.Zero
            && obj.IsValid()
            && obj.IsTargetable;
    }

    public bool TryGetIconTexture(uint iconId, out IDalamudTextureWrap? texture)
    {
        texture = null;
        if (iconId == 0)
            return false;

        try
        {
            texture = TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrEmpty();
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to load minion icon {IconId}.", iconId);
            return false;
        }
    }

    public uint GetIconIdForKey(string key)
    {
        return TryGetCompanionId(key, out var companionId) ? GetIconIdForCompanionId(companionId) : 0;
    }

    private static bool IsOwnedByLocalPlayer(Dalamud.Game.ClientState.Objects.Types.IGameObject obj, bool hasLocalPlayer, uint localEntityId, uint localGameObjectId)
    {
        if (!hasLocalPlayer)
            return false;

        var ownerId = GetObjectIdPart(obj.OwnerId);
        var objectId = GetObjectIdPart(obj.GameObjectId);

        return IsSameObjectId(ownerId, localEntityId)
            || IsSameObjectId(ownerId, localGameObjectId)
            || IsSameObjectId(objectId, localEntityId)
            || IsSameObjectId(objectId, localGameObjectId);
    }

    private static bool IsSameObjectId(uint left, uint right)
    {
        return left != 0 && right != 0 && left == right;
    }

    private static uint GetObjectIdPart(ulong id)
    {
        return (uint)(id & 0xFFFFFFFF);
    }

    private MinionEntry CreateMinionEntry(Dalamud.Game.ClientState.Objects.Types.IGameObject obj, bool isOwn)
    {
        var objectName = obj.Name.ToString();
        if (string.IsNullOrWhiteSpace(objectName))
            objectName = $"Minion {obj.BaseId}";

        var key = CreateMinionKey(obj, objectName);
        var name = ResolveMinionName(key, objectName, null);

        return new MinionEntry(key, obj.BaseId, name, isOwn, GetIconIdForCompanionId(obj.BaseId));
    }

    private static string CreateMinionKey(IGameObject obj)
    {
        var name = obj.Name.ToString();
        if (string.IsNullOrWhiteSpace(name))
            name = $"Minion {obj.BaseId}";

        return CreateMinionKey(obj, name);
    }

    private static string CreateMinionKey(IGameObject obj, string name)
    {
        return obj.BaseId != 0
            ? $"data:{obj.BaseId}"
            : $"name:{name}";
    }

    private uint GetIconIdForCompanionId(uint companionId)
    {
        if (companionId == 0)
            return 0;

        if (iconIdsByCompanionId.TryGetValue(companionId, out var iconId))
            return iconId;

        try
        {
            iconId = DataManager.GetExcelSheet<Companion>().TryGetRow(companionId, out var companion)
                ? (uint)companion.Icon
                : 0;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to read Companion icon for row {CompanionId}.", companionId);
            iconId = 0;
        }

        iconIdsByCompanionId[companionId] = iconId;
        return iconId;
    }

    private string ResolveMinionName(string key, string? objectName, string? savedName)
    {
        if (TryGetCompanionId(key, out var companionId)
            && TryGetCompanionName(companionId, out var companionName))
            return companionName;

        if (!string.IsNullOrWhiteSpace(objectName))
            return objectName;

        if (!string.IsNullOrWhiteSpace(savedName))
            return savedName;

        return TryGetCompanionId(key, out companionId)
            ? $"Minion {companionId}"
            : key.StartsWith("name:", StringComparison.Ordinal) ? key[5..] : "Unknown minion";
    }

    private bool TryGetCompanionName(uint companionId, out string name)
    {
        name = string.Empty;
        if (companionId == 0)
            return false;

        var language = ClientState.ClientLanguage;
        var cacheKey = (language, companionId);
        if (namesByLanguageAndCompanionId.TryGetValue(cacheKey, out var cachedName))
        {
            name = cachedName;
            return !string.IsNullOrWhiteSpace(name);
        }

        try
        {
            if (DataManager.GetExcelSheet<Companion>(language).TryGetRow(companionId, out var companion))
                name = companion.Singular.ToString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to read Companion name for row {CompanionId} in {Language}.", companionId, language);
        }

        namesByLanguageAndCompanionId[cacheKey] = name;
        return !string.IsNullOrWhiteSpace(name);
    }

    private static bool TryGetCompanionId(string key, out uint companionId)
    {
        companionId = 0;
        return key.StartsWith("data:", StringComparison.Ordinal)
            && uint.TryParse(key.AsSpan(5), out companionId);
    }

    private void RestoreNoLongerMatchingMinions(HashSet<ulong> stillMatching)
    {
        foreach (var (id, trackedScale) in trackedScales.ToArray())
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
        foreach (var (id, trackedScale) in trackedScales.ToArray())
        {
            var obj = ObjectTable.SearchById(id);
            if (obj != null && obj.Address != nint.Zero && obj.IsValid() && obj.ObjectKind == ObjectKind.Companion)
            {
                var gameObject = (GameObject*)obj.Address;
                RestoreTrackedMinion(id, gameObject);
            }
        }

        trackedScales.Clear();
    }

    private void RestoreDrawObjectScale(ulong id, GameObject* gameObject)
    {
        if (!trackedScales.TryGetValue(id, out var trackedScale))
            return;

        var drawObject = gameObject->DrawObject;
        if (drawObject == null)
            return;

        var sceneObject = (SceneObject*)drawObject;
        trackedScale.DrawScale.ApplyTo(sceneObject, 1.0f);
        drawObject->NotifyTransformChanged();
    }

    private void RestoreTrackedMinion(ulong id, GameObject* gameObject)
    {
        if (trackedScales.TryGetValue(id, out var trackedScale))
            gameObject->Scale = trackedScale.GameScale;

        RestoreDrawObjectScale(id, gameObject);
        ClearTrackedMinion(id);
    }

    private void ClearTrackedMinion(ulong id)
    {
        trackedScales.Remove(id);
    }

    private void ClearScaleCacheForWorldChange()
    {
        trackedScales.Clear();
    }

    private void SubscribeClientStateEvent(string eventName)
    {
        var eventInfo = typeof(IClientState).GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public);
        if (eventInfo?.EventHandlerType == null)
            return;

        try
        {
            var invoke = eventInfo.EventHandlerType.GetMethod("Invoke");
            if (invoke == null || invoke.ReturnType != typeof(void))
                return;

            var method = typeof(Plugin).GetMethod(nameof(ClearScaleCacheForWorldChange), BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
                return;

            var parameters = invoke.GetParameters()
                .Select(parameter => Expression.Parameter(parameter.ParameterType, parameter.Name))
                .ToArray();
            var body = Expression.Call(Expression.Constant(this), method);
            var handler = Expression.Lambda(eventInfo.EventHandlerType, body, parameters).Compile();

            eventInfo.AddEventHandler(ClientState, handler);
            clientStateSubscriptions.Add(new ClientStateSubscription(eventInfo, handler));
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to subscribe to client state event {EventName}.", eventName);
        }
    }

    private void UnsubscribeClientStateEvents()
    {
        foreach (var subscription in clientStateSubscriptions)
        {
            try
            {
                subscription.EventInfo.RemoveEventHandler(ClientState, subscription.Handler);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to unsubscribe from client state event {EventName}.", subscription.EventInfo.Name);
            }
        }

        clientStateSubscriptions.Clear();
    }
}

public sealed record MinionEntry(string Key, uint CompanionId, string Name, bool IsOwn, uint IconId);

internal sealed record ClientStateSubscription(EventInfo EventInfo, Delegate Handler);

internal readonly record struct TrackedScale(
    ulong GameObjectId,
    nint GameObjectAddress,
    nint DrawObjectAddress,
    float GameScale,
    DrawScale DrawScale);

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
