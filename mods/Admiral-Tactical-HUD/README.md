# Admiral Tactical HUD

Admiral Tactical HUD `1.13.3` for SPT 4.1.x. The current PR package is **RC2** and remains under runtime smoke validation after the rejected first finalization candidate.

| Component | Version |
| --- | --- |
| Compact client | `1.13.3` |
| Full Census client | `1.13.3` |
| Server companion | `1.13.3` |

## Scope

Admiral Tactical HUD owns only HUD functionality:

- compact population display;
- optional Full Census population display;
- player status display;
- kill feed;
- HUD edit mode;
- HUD assets and their validation/optimization pipeline.

The `1.13.3` line is the only maintained version. Legacy Tactical HUD versions and retired combined HUD/Item-Intelligence concepts are not maintained runtime products.

## Two population modes

### Compact

`Admiral Tactical HUD.dll` contains the original compact population strip together with player status and kill feed. The compact population roles now prefer the cleaner Bot Census glyph set for PMC/Scav/Boss/Raider-compatible categories.

### Full Census

`Admiral Tactical HUD Full Census.dll` is a separate optional BepInEx plugin and is disabled by default. Enable it from its F12 configuration when a detailed census is wanted. It provides separate rows for PMC, Scav, Raider, Rogue, Boss, Guard, Goons, Cultist, Infected, BTR, known custom-faction ranges, Other and Total Bots, with configurable row visibility and split/merge behavior.

The two client DLLs are independent. For a single population presentation, keep Compact population enabled or enable Full Census and disable the Compact `Population > Enabled` setting.

## Icon architecture

Population glyphs compatible with the Full Census model are derived from the MIT-licensed `CameronsWorks/BotCensus` project. Attribution and the upstream license are preserved under `client/assets/botcensus/` and `THIRD-PARTY-LICENSES/`.

The original Admiral Tactical HUD sprite atlas and approved source cells are **retained, not deleted**. They remain the reserve/fallback source for status, body-part and self icons and for any population glyph that cannot be loaded from the embedded Bot Census set.

Both the Bot Census population glyphs and the Admiral reserve atlas are embedded in the relevant client assemblies. The external `assets/hud-sprites.png` remains packaged for compatibility, but losing that file no longer removes all HUD icons.

## Kill feed

Kill-feed rows use a compact weapon name instead of a weapon-class icon. The intended row is:

`killer icon -> short weapon text -> victim icon -> hit-location icon -> distance`

Weapon text is normalized to compact forms such as `AK-105`, `M4A1` or `MP7A2` and rendered with a compact font plus dark outline/shadow. Role and hit-location icons remain independent of the weapon text.

## Source layout

- `client/` — Compact and Full Census BepInEx sources plus maintained HUD assets.
- `server/` — SPT server companion.
- `tools/` — deterministic asset, optics and hot-path validation tools.
- `THIRD-PARTY-LICENSES/` — retained licenses for incorporated third-party material.

Generated build output is never a source of truth and is not committed to the module tree.

## RC2 runtime installation

The RC2 package installs to:

- `BepInEx/plugins/Admiral Tactical HUD/Admiral Tactical HUD.dll`
- `BepInEx/plugins/Admiral Tactical HUD/Admiral Tactical HUD Full Census.dll`
- `BepInEx/plugins/Admiral Tactical HUD/assets/hud-sprites.png`
- `SPT_Runtime/user/mods/Admiral Tactical HUD/Admiral Tactical HUD Server.dll`

Copy the packaged `BepInEx/` and `SPT_Runtime/` folders into the SPT root. Remove obsolete pre-Admiral Tactical HUD DLLs before testing so only one generation is loaded.

RC2 is not promoted as stable until Compact icons, Full Census, status icons and the weapon-text kill feed pass a physical SPT smoke test.
