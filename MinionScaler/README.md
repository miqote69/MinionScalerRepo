# Minion Scaler

Dalamud plugin prototype that changes visible minion size locally.

## Use

- Command: `/minionscaler`
- Alias: `/minionscale`
- Settings command: `/minionscalerconfig`
- Saved minion settings can apply to only your minion or to everyone using that minion.
- Use the `Default` button or remove a saved setting to restore tracked minions to their original scale.

## Localization

- UI language can be selected from `Settings`: Automatic, English, Japanese, German, or French.
- `Automatic` follows the current FFXIV client language.
- Minion names always follow the FFXIV client language and are resolved from game data when available.
- The filter and sorting use the localized minion names currently shown on screen, independent of the selected UI language.

## Notes

This is a cosmetic client-side plugin prototype. It does not automate gameplay, send network requests, or collect player data. It writes to local object scale and draw-object transform fields, so it may need adjustment when FFXIV or Dalamud changes object structures.
