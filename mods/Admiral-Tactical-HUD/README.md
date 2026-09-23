# Admiral Tactical HUD

Admiral Tactical HUD `1.13.3` for SPT 4.1.x.

The current accepted runtime state is **Stable Beta**: it is user-confirmed playable and suitable for normal use, but it is **not final stable**. Further polish and expansion continue later from the same product line.

Compass builds from this PR are unaccepted previews and do not replace the accepted Stable Beta baseline until the in-game gate passes.

| Component | Version |
| --- | --- |
| Compact client | `1.13.3` |
| Full Census client | `1.13.3` |
| Server companion | `1.13.3` |
| Release status | **Stable Beta** |

Stable Beta runtime code baseline: `d7d63c0196af12141aedfc8f4896f0a9a84376ed`.

## Scope

Admiral Tactical HUD currently owns:

- compact population display;
- optional Full Census population display;
- player status display;
- optional item-gated compass heading strip (development candidate; physical raid acceptance pending);
- HUD edit mode;
- HUD assets and their validation/optimization pipeline.

The `1.13.3` line is the only maintained version. Legacy Tactical HUD versions and retired combined HUD/Item-Intelligence concepts are not maintained runtime products.

## Stable Beta boundary

The current working state is frozen as the **Stable Beta** gameplay baseline.

The unsuccessful experimental kill feed has been removed and is **not included**. Do not restore that implementation or treat its old weapon-text/weapon-icon contract as the next design.

Kill feed remains the **mandatory next product stage** and must be redesigned from first principles for clear, expressive, rapidly readable combat use with low visual noise and no obstruction of important interface information. No new kill-feed implementation is part of this Stable Beta scope.

See `RELEASE_NOTES.md` and `release-metadata.json` for the exact baseline contract.

## Two population modes

### Compact

`Admiral Tactical HUD.dll` contains the original compact population strip together with player status. Compact PMC, Scav, Boss and reinforced-enemy roles use the established Admiral atlas icons; the later Bot Census replacements remain available only as fallback for those roles. Compact stays a short unbacked summary and does not acquire the detailed Full Census rows.

### Full Census

`Admiral Tactical HUD Full Census.dll` is a separate optional BepInEx plugin and is disabled by default. Enable it from its F12 configuration when a detailed census is wanted. It provides separate rows for PMC, Scav, Raider, Rogue, Boss, Guard, Goons, Cultist, Infected, BTR, discovered custom factions, Other and Total Bots, with configurable row visibility and split/merge behavior. It deliberately uses the original Bot Census glyph family and its own backed panel rather than the Compact visual language.

MoreBotsAPI and Fika are soft integrations, never mandatory dependencies. With MoreBotsAPI present, Full Census reads its live role-to-faction registry and custom-role metadata, separates a registered custom boss from escorts when the faction actually has a leader, and still falls back to known role ranges if the bridge is unavailable. With Fika present, it prefers the shared player collection and includes observed AI; an incompatible or missing Fika bridge falls back to the local SPT player list.

The two client DLLs are independent. For a single population presentation, keep Compact population enabled or enable Full Census and disable the Compact `Population > Enabled` setting.

## Icon architecture

The Full Census glyph files are vendored unchanged from the MIT-licensed `CameronsWorks/BotCensus` project. Its vanilla role classification, MoreBotsAPI discovery/boss-shaping behavior, Fika shared-player semantics and known custom-faction range fallback inform the Full Census implementation. Admiral uses reflection-only optional adapters so neither integration can prevent the base HUD from loading. Attribution and the upstream license are preserved under `client/assets/botcensus/` and `THIRD-PARTY-LICENSES/`.

The original Admiral Tactical HUD sprite atlas and approved source cells are **retained, not deleted**. They remain the reserve/fallback source for status, body-part and self icons and for any population glyph that cannot be loaded from the embedded Bot Census set.

Both the Bot Census population glyphs and the Admiral reserve atlas are embedded in the relevant client assemblies. The external `assets/hud-sprites.png` remains packaged for compatibility, but losing that file no longer removes all HUD icons.

## Compass preview (C1–C2 source)

The Compact client now includes an optional heading strip. By default it appears in raid only while the vanilla EYE MK.2 compass is equipped in one of the three special slots. `Compass` settings in F12 control visibility, item requirement, degrees, Russian direction labels, scale, opacity and top offset. The strip uses the existing HUD raid lifecycle; its in-game orientation and slot behavior still need physical acceptance.

The compass can also show eligible exits (`В`), transits (`П`) and active quest objectives (`З`). The latter require the optional Dynamic Maps mod; the F12 `Compass` settings control each category. These markers still require in-game acceptance and are not part of the accepted Stable Beta baseline. The 80-degree heading projection and compass presentation are adapted from [Vinarator/Compass-HUD 1.1.2](https://github.com/Vinarat0r/Compass-HUD) under MIT; its license is preserved in `THIRD-PARTY-LICENSES/CompassHUD-LICENSE.txt`. No source or asset was taken from Immersive Compass because its published source URL is unavailable.

## Source layout

- `client/` — Compact and Full Census BepInEx sources plus maintained HUD assets.
- `server/` — SPT server companion.
- `docs/compass-navigation-roadmap.md` — compass donor audit and C1–C5 implementation contracts.
- `tools/` — deterministic asset, optics and hot-path validation tools.
- `THIRD-PARTY-LICENSES/` — retained licenses for incorporated third-party material.
- `RELEASE_NOTES.md` — Stable Beta release notes.
- `release-metadata.json` — machine-readable Stable Beta baseline metadata.

Generated build output is never a source of truth and is not committed to the module tree.

## Stable Beta installation

The Stable Beta package installs to:

- `BepInEx/plugins/Admiral Tactical HUD/Admiral Tactical HUD.dll`
- `BepInEx/plugins/Admiral Tactical HUD/Admiral Tactical HUD Full Census.dll`
- `BepInEx/plugins/Admiral Tactical HUD/assets/hud-sprites.png`
- `SPT_Runtime/user/mods/Admiral Tactical HUD/Admiral Tactical HUD Server.dll`

Before copying Stable Beta, remove every path listed under `removeBeforeInstall` in the packaged `SPT_Runtime/user/mods/Admiral Tactical HUD/cleanup-manifest.json`. Then remove the two existing directories listed under `replace` and copy the packaged `BepInEx/` and `SPT_Runtime/` folders into the SPT root.

Stable Beta is deliberately not final stable.
