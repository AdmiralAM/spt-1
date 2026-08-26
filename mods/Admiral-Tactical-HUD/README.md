# Admiral Tactical HUD

Stable tactical HUD module for SPT 4.1.x.

| Component | Version |
| --- | --- |
| Client | `1.13.3` |
| Server companion | `1.13.3` |

## Scope

Admiral Tactical HUD owns only HUD functionality:

- population display;
- player status display;
- kill feed;
- HUD edit mode;
- HUD assets and their validation/optimization pipeline.

The `1.13.3` line is the only maintained version. Legacy Tactical HUD version records and retired combined HUD/Item-Intelligence concepts are not part of the maintained package.

## Kill feed

Kill-feed rows use compact weapon names instead of weapon-class icons. The runtime resolves the weapon once when the death is captured, normalizes it to a short readable form (for example `AK-105`, `M4A1`, `MP7A2`), and renders that text with a compact system font, dark outline and shadow. Role, hit-location and status iconography remains unchanged.

## Source layout

- `client/` — BepInEx client source and maintained HUD assets.
- `server/` — SPT server companion.
- `tools/` — deterministic asset, optics, and hot-path validation tools.

Generated build output is never a source of truth and is not committed to the module tree.

## Runtime installation

The final package installs to:

- `BepInEx/plugins/Admiral Tactical HUD/Admiral Tactical HUD.dll`
- `BepInEx/plugins/Admiral Tactical HUD/assets/hud-sprites.png`
- `SPT_Runtime/user/mods/Admiral Tactical HUD/Admiral Tactical HUD Server.dll`

Copy the packaged `BepInEx/` and `SPT_Runtime/` folders into the SPT root. Future `1.13.3` rebuilds use the same stable paths and replace the existing files in place.
