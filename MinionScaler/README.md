# Minion Scaler

Dalamud plugin prototype that changes visible minion size locally.

## Use

- Command: `/minionscaler`
- Alias: `/minionscale`
- Settings command: `/minionscalerconfig`
- Saved minion settings can apply to only your minion or to everyone using that minion.
- Use the `Default` button or remove a saved setting to restore tracked minions to their original scale.

## Notes

This is a cosmetic client-side plugin prototype. It does not automate gameplay, send network requests, or collect player data. It writes to local object scale and draw-object transform fields, so it may need adjustment when FFXIV or Dalamud changes object structures.
