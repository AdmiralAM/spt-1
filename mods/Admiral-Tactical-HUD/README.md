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
- HUD edit mode;
- HUD assets and their validation/optimization pipeline.

The `1.13.3` line is the only maintained version. Legacy Tactical HUD versions and retired combined HUD/Item-Intelligence concepts are not maintained runtime products.

## Two population modes

### Compact

`Admiral Tactical HUD.dll` contains the original compact population strip together with player status. Compact PMC, Scav, Boss and reinforced-enemy roles use the established Admiral atlas icons; the later Bot Census replacements remain available only as fallback for those roles.

### Full Census

`Admiral Tactical HUD Full Census.dll` is a separate optional BepInEx plugin and is disabled by default. Enable it from its F12 configuration when a detailed census is wanted. It provides separate rows for PMC, Scav, Raider, Rogue, Boss, Guard, Goons, Cultist, Infected, BTR, known custom-faction ranges, Other and Total Bots, with configurable row visibility and split/merge behavior.

The two client DLLs are independent. For a single population presentation, keep Compact population enabled or enable Full Census and disable the Compact `Population > Enabled` setting.

## Icon architecture

The Full Census glyph files are vendored unchanged from the MIT-licensed `CameronsWorks/BotCensus` project. Its vanilla role classification and known custom-faction range fallback inform the current Full Census implementation. This is not a complete Bot Census port: the upstream live MoreBotsAPI registry, custom boss/escort shaping and typed Fika source are not currently integrated. Attribution and the upstream license are preserved under `client/assets/botcensus/` and `THIRD-PARTY-LICENSES/`.

The original Admiral Tactical HUD sprite atlas and approved source cells are **retained, not deleted**. They remain the reserve/fallback source for status, body-part and self icons and for any population glyph that cannot be loaded from the embedded Bot Census set.

Both the Bot Census population glyphs and the Admiral reserve atlas are embedded in the relevant client assemblies. The external `assets/hud-sprites.png` remains packaged for compatibility, but losing that file no longer removes all HUD icons.

## Source layout

- `client/` — Compact and Full Census BepInEx sources plus maintained HUD assets.
- `server/` — SPT server companion.
- `docs/compass-navigation-roadmap.md` — deferred compass, Dynamic Maps and mono-audio accessibility design draft.
- `tools/` — deterministic asset, optics and hot-path validation tools.
- `THIRD-PARTY-LICENSES/` — retained licenses for incorporated third-party material.

Generated build output is never a source of truth and is not committed to the module tree.

## RC2 runtime installation

The RC2 package installs to:

- `BepInEx/plugins/Admiral Tactical HUD/Admiral Tactical HUD.dll`
- `BepInEx/plugins/Admiral Tactical HUD/Admiral Tactical HUD Full Census.dll`
- `BepInEx/plugins/Admiral Tactical HUD/assets/hud-sprites.png`
- `SPT_Runtime/user/mods/Admiral Tactical HUD/Admiral Tactical HUD Server.dll`

Before copying RC2, remove every path listed under `removeBeforeInstall` in the packaged
`SPT_Runtime/user/mods/Admiral Tactical HUD/cleanup-manifest.json`. Then remove the two
existing directories listed under `replace` and copy the packaged `BepInEx/` and
`SPT_Runtime/` folders into the SPT root. This prevents an old DLL or duplicate atlas from
being loaded beside RC2; the manifest is the exact machine-readable replacement contract.

RC2 is not promoted as stable until Compact population icons, Full Census, status icons and raid/menu lifecycle pass a physical SPT smoke test. The experimental kill feed has been removed from this release line; any future replacement requires a separate readable and non-distracting design.
