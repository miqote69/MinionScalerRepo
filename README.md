# Minion Scaler

> [!CAUTION]
> ## Development build — use entirely at your own risk
>
> Minion Scaler is still under active development and may behave unpredictably.
>
> This plugin directly changes local game-object and draw-object scale values. It may display minions incorrectly, fail to restore their original scale, stop working after an FFXIV or Dalamud update, cause visual glitches, or cause the game to become unstable or crash.
>
> **No warranty or guarantee of any kind is provided.**
>
> The author does not guarantee stability, compatibility, successful restoration, safety, data integrity, or any result produced by this plugin. The author assumes no responsibility for crashes, lost or incorrect states, unexpected behavior, damage, or any other consequence caused directly or indirectly by using Minion Scaler.
>
> Use this plugin only if you understand and accept these risks.

Minion Scaler is a cosmetic Dalamud plugin that changes the displayed size of selected minions on your local game client.

It can resize your own minion or every currently visible instance of the same minion type.

These changes are local only and do not change server-side minion data or how the minion appears to other players.

## Features

### Visible minion list

- Lists targetable minions currently visible to the game client.
- Displays the minion's name and in-game icon.
- Marks your own minion with `(Mine)`.
- Groups matching minions by minion type.
- Shows unpinned minions in the `Visible` tab.
- Displays the number of matching entries in the tab title.

Only minions that are currently valid, visible to the object table, and targetable can appear in the Visible list.

### Pinned minions

- Saves individual minion scale settings.
- Keeps saved minions in a separate `Pinned` tab.
- Automatically reapplies saved settings when a matching minion becomes visible.
- Stores the minion name, icon, scale, and application scope.
- Displays the number of matching pinned entries in the tab title.
- Allows individual pinned settings to be deleted.

A pinned minion does not need to be currently visible for its setting to remain saved.

### Scale controls

Minions can be resized from:

- 0.10x to 10.00x

Scale can be adjusted with:

- A slider.
- A numeric input field.
- The `Default` button.

Modified scale controls are highlighted to make non-default values easier to identify.

Scale changes are previewed and applied locally while the matching minion is visible.

### Application scope

Each minion setting can use one of two scopes:

#### Everyone

Applies the selected scale to every visible instance of the same minion type.

#### Mine only

Applies the selected scale only to the minion detected as belonging to the local player.

The default scope for a new setting is `Everyone`.

### Name filter

The filter field searches both:

- Visible minions.
- Pinned minions.

Filtering is case-insensitive and uses the displayed minion name.

### Localization

- UI language can be selected from `Settings`: Automatic, English, Japanese, German, or French.
- `Automatic` follows the current FFXIV client language.
- Minion names always follow the FFXIV client language and are resolved from game data when available.
- Filtering and sorting use the localized minion names currently shown on screen, independent of the selected UI language.

### Target a minion

Click a minion's icon to target the closest currently visible and targetable instance of that minion type.

This can help identify which minion a list entry represents when several minions are nearby.

### Reset and delete controls

For an individual minion:

- `Default` returns its scale to `1.00x`.
- The pin button saves the current setting.
- The delete button removes a pinned setting and attempts to restore matching tracked minions.

The Settings page also provides:

- `Reset all pinned` — returns every pinned scale to `1.00x` while keeping the entries pinned.
- `Delete all pinned` — removes all saved minion settings and attempts to restore affected tracked minions.

### Original scale restoration

Before modifying a visible minion, Minion Scaler records its current local game-object and draw-object scale values.

The plugin attempts to restore those recorded values when:

- The scale is returned to default.
- A setting is deleted.
- A minion no longer matches the active setting.
- The plugin is unloaded.

Restoration is not guaranteed. Object recreation, territory changes, logout, FFXIV updates, Dalamud updates, invalid game objects, or plugin errors may prevent a complete restoration.

## Install from a custom repository

Add this URL to Dalamud's custom plugin repositories:

```text
https://raw.githubusercontent.com/miqote69/MinionScalerRepo/main/repo.json
```

### Installation steps

1. Open Dalamud settings.
2. Open the `Experimental` tab.
3. Add the custom plugin repository URL shown above.
4. Open the Dalamud Plugin Installer.
5. Search for `Minion Scaler`.
6. Install the plugin.

## Commands

Open Minion Scaler with any of the following commands:

```text
/minionscaler
/minionscale
/minionscalerconfig
```

All three commands open the Minion Scaler configuration window.

## Basic usage

1. Summon a minion or move near another visible minion.
2. Open Minion Scaler.
3. Select the `Visible` tab.
4. Find the minion by name or use the filter.
5. Adjust its scale using the slider or numeric input.
6. Select `Everyone` or `Mine only`.
7. Click the pin button to save the setting.

Saved settings can later be managed from the `Pinned` tab.

## Current development status

Minion Scaler currently provides:

- Detection of visible and targetable minions.
- Identification of the local player's minion.
- Client-language minion name resolution from game data.
- Selectable UI language independent of minion name language.
- Minion name and icon display.
- Visible and Pinned tabs.
- Minion name filtering.
- Local scale adjustment from `0.10x` to `10.00x`.
- Slider and numeric scale input.
- Everyone and Mine-only application scopes.
- Persistent pinned minion settings.
- Automatic application to newly visible matching minions.
- Individual reset and deletion.
- Reset-all and delete-all controls.
- Minion targeting by clicking its icon.
- Tracking and attempted restoration of original scale values.
- A custom high-resolution plugin icon.

The plugin writes to local FFXIV game-object scale and draw-object transform fields.

FFXIV, Dalamud, or FFXIVClientStructs updates may change these structures and cause the plugin to stop working or behave incorrectly.

Features may be incomplete, may change without notice, and may require adjustment after game or framework updates.

## Privacy and scope

Minion Scaler is a cosmetic, client-side plugin.

It does not:

- Automate gameplay.
- Control minion actions.
- Send gameplay commands.
- Send network requests.
- Upload minion information.
- Collect player data.

## License

Minion Scaler is licensed under the [MIT License](LICENSE).
