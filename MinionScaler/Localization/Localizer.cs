using Dalamud.Game;
using Dalamud.Plugin.Services;
using System.Globalization;

namespace MinionScaler.Localization;

public sealed class Localizer
{
    private readonly Configuration configuration;
    private readonly IClientState clientState;

    public Localizer(Configuration configuration, IClientState clientState)
    {
        this.configuration = configuration;
        this.clientState = clientState;
    }

    public UiLanguage EffectiveLanguage => configuration.UiLanguage == UiLanguage.Automatic
        ? FromClientLanguage(clientState.ClientLanguage)
        : configuration.UiLanguage;

    public string Get(UiTextKey key, params object[] arguments)
    {
        var value = Strings.TryGetValue(EffectiveLanguage, out var language)
            && language.TryGetValue(key, out var localized)
                ? localized
                : English[key];

        return arguments.Length == 0
            ? value
            : string.Format(CultureInfo.CurrentCulture, value, arguments);
    }

    public string GetLanguageName(UiLanguage language) => language switch
    {
        UiLanguage.Automatic => Get(UiTextKey.Automatic),
        UiLanguage.English => Get(UiTextKey.English),
        UiLanguage.Japanese => Get(UiTextKey.Japanese),
        UiLanguage.German => Get(UiTextKey.German),
        UiLanguage.French => Get(UiTextKey.French),
        _ => language.ToString(),
    };

    public static UiLanguage FromClientLanguage(ClientLanguage language) => language switch
    {
        ClientLanguage.Japanese => UiLanguage.Japanese,
        ClientLanguage.German => UiLanguage.German,
        ClientLanguage.French => UiLanguage.French,
        _ => UiLanguage.English,
    };

    private static readonly IReadOnlyDictionary<UiTextKey, string> English = new Dictionary<UiTextKey, string>
    {
        [UiTextKey.Automatic] = "Automatic",
        [UiTextKey.English] = "English",
        [UiTextKey.Japanese] = "Japanese",
        [UiTextKey.German] = "German",
        [UiTextKey.French] = "French",
        [UiTextKey.Minions] = "Minions",
        [UiTextKey.Settings] = "Settings",
        [UiTextKey.UiLanguage] = "UI language",
        [UiTextKey.GameDataLanguage] = "Game data language: {0}",
        [UiTextKey.Visible] = "Visible",
        [UiTextKey.Pinned] = "Pinned",
        [UiTextKey.Filter] = "Filter",
        [UiTextKey.Mine] = "Mine",
        [UiTextKey.Scale] = "Scale",
        [UiTextKey.Everyone] = "Everyone",
        [UiTextKey.MineOnly] = "Mine only",
        [UiTextKey.Default] = "Default",
        [UiTextKey.TargetThisMinion] = "Target this minion",
        [UiTextKey.PinThisMinion] = "Pin this minion",
        [UiTextKey.Delete] = "Delete",
        [UiTextKey.ResetAllPinned] = "Reset all pinned",
        [UiTextKey.DeleteAllPinned] = "Delete all pinned",
        [UiTextKey.NoVisibleMinions] = "No unpinned minions are currently visible.",
        [UiTextKey.NoPinnedMinions] = "Pinned minions will appear here.",
    };

    private static readonly IReadOnlyDictionary<UiLanguage, IReadOnlyDictionary<UiTextKey, string>> Strings =
        new Dictionary<UiLanguage, IReadOnlyDictionary<UiTextKey, string>>
        {
            [UiLanguage.English] = English,
            [UiLanguage.Japanese] = Translate(new Dictionary<UiTextKey, string>
            {
                [UiTextKey.Automatic] = "自動",
                [UiTextKey.English] = "英語",
                [UiTextKey.Japanese] = "日本語",
                [UiTextKey.German] = "ドイツ語",
                [UiTextKey.French] = "フランス語",
                [UiTextKey.Minions] = "ミニオン",
                [UiTextKey.Settings] = "設定",
                [UiTextKey.UiLanguage] = "UI言語",
                [UiTextKey.GameDataLanguage] = "ゲームデータ言語: {0}",
                [UiTextKey.Visible] = "表示中",
                [UiTextKey.Pinned] = "ピン留め",
                [UiTextKey.Filter] = "フィルター",
                [UiTextKey.Mine] = "自分",
                [UiTextKey.Scale] = "サイズ",
                [UiTextKey.Everyone] = "すべて",
                [UiTextKey.MineOnly] = "自分のみ",
                [UiTextKey.Default] = "デフォルト",
                [UiTextKey.TargetThisMinion] = "このミニオンをターゲット",
                [UiTextKey.PinThisMinion] = "このミニオンをピン留め",
                [UiTextKey.Delete] = "削除",
                [UiTextKey.ResetAllPinned] = "すべてデフォルトに戻す",
                [UiTextKey.DeleteAllPinned] = "すべてのピン留めを削除",
                [UiTextKey.NoVisibleMinions] = "現在表示されている未登録のミニオンはありません。",
                [UiTextKey.NoPinnedMinions] = "ピン留めしたミニオンがここに表示されます。",
            }),
            [UiLanguage.German] = Translate(new Dictionary<UiTextKey, string>
            {
                [UiTextKey.Automatic] = "Automatisch",
                [UiTextKey.English] = "Englisch",
                [UiTextKey.Japanese] = "Japanisch",
                [UiTextKey.German] = "Deutsch",
                [UiTextKey.French] = "Französisch",
                [UiTextKey.Minions] = "Begleiter",
                [UiTextKey.Settings] = "Einstellungen",
                [UiTextKey.UiLanguage] = "UI-Sprache",
                [UiTextKey.GameDataLanguage] = "Spielsprache: {0}",
                [UiTextKey.Visible] = "Sichtbar",
                [UiTextKey.Pinned] = "Angeheftet",
                [UiTextKey.Filter] = "Filter",
                [UiTextKey.Mine] = "Eigener",
                [UiTextKey.Scale] = "Größe",
                [UiTextKey.Everyone] = "Alle",
                [UiTextKey.MineOnly] = "Nur eigener",
                [UiTextKey.Default] = "Standard",
                [UiTextKey.TargetThisMinion] = "Diesen Begleiter anvisieren",
                [UiTextKey.PinThisMinion] = "Diesen Begleiter anheften",
                [UiTextKey.Delete] = "Löschen",
                [UiTextKey.ResetAllPinned] = "Alle angehefteten zurücksetzen",
                [UiTextKey.DeleteAllPinned] = "Alle angehefteten löschen",
                [UiTextKey.NoVisibleMinions] = "Derzeit sind keine nicht angehefteten Begleiter sichtbar.",
                [UiTextKey.NoPinnedMinions] = "Angeheftete Begleiter werden hier angezeigt.",
            }),
            [UiLanguage.French] = Translate(new Dictionary<UiTextKey, string>
            {
                [UiTextKey.Automatic] = "Automatique",
                [UiTextKey.English] = "Anglais",
                [UiTextKey.Japanese] = "Japonais",
                [UiTextKey.German] = "Allemand",
                [UiTextKey.French] = "Français",
                [UiTextKey.Minions] = "Mascottes",
                [UiTextKey.Settings] = "Paramètres",
                [UiTextKey.UiLanguage] = "Langue de l'interface",
                [UiTextKey.GameDataLanguage] = "Langue des données du jeu : {0}",
                [UiTextKey.Visible] = "Visibles",
                [UiTextKey.Pinned] = "Épinglées",
                [UiTextKey.Filter] = "Filtrer",
                [UiTextKey.Mine] = "À moi",
                [UiTextKey.Scale] = "Taille",
                [UiTextKey.Everyone] = "Tout le monde",
                [UiTextKey.MineOnly] = "Moi seulement",
                [UiTextKey.Default] = "Par défaut",
                [UiTextKey.TargetThisMinion] = "Cibler cette mascotte",
                [UiTextKey.PinThisMinion] = "Épingler cette mascotte",
                [UiTextKey.Delete] = "Supprimer",
                [UiTextKey.ResetAllPinned] = "Réinitialiser les épinglées",
                [UiTextKey.DeleteAllPinned] = "Supprimer toutes les épinglées",
                [UiTextKey.NoVisibleMinions] = "Aucune mascotte non épinglée n'est visible pour le moment.",
                [UiTextKey.NoPinnedMinions] = "Les mascottes épinglées apparaîtront ici.",
            }),
        };

    private static IReadOnlyDictionary<UiTextKey, string> Translate(IReadOnlyDictionary<UiTextKey, string> overrides)
        => English.ToDictionary(pair => pair.Key, pair => overrides.TryGetValue(pair.Key, out var value) ? value : pair.Value);
}
