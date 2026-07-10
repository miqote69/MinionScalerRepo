# Minion Scaler

Dalamud plugin prototype that changes visible minion size locally.

## Install from a custom repository

Add this URL in Dalamud:

```text
https://raw.githubusercontent.com/miqote69/MinionScalerRepo/main/repo.json
```

In-game:

1. Open Dalamud settings.
2. Open the Experimental tab.
3. Add the custom plugin repository URL above.
4. Open the plugin installer and install `Minion Scaler`.

## Use

- Command: `/minionscaler`
- Alias: `/minionscale`
- Settings command: `/minionscalerconfig`
- Saved minion settings can apply to only your minion or to everyone using that minion.
- Use the `Default` button or remove a saved setting to restore tracked minions to their original scale.

## Notes

This is a cosmetic client-side plugin prototype. It does not automate gameplay, send network requests, or collect player data. It writes to local object scale and draw-object transform fields, so it may need adjustment when FFXIV or Dalamud changes object structures.
