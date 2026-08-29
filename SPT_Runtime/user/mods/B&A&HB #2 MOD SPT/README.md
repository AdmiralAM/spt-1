# B&A&HB #2 MOD SPT

Stable wearable-inventory runtime for **SPT 4.1.3**.

## Stable status

Version **0.1.0** is the first physically accepted stable runtime line. The accepted runtime identity is derived from commit `d6336f290361b16c4aa54f9d7dddfe0e8f7f9bbf` and preserves all distributed template/grid/assort IDs and dedicated equipment slot values.

This stable source contains production runtime code and profile-recovery material only. Development tests, RC checklists, diagnostic tooling and temporary evidence are intentionally excluded from the stable module tree.

## Wearable system

- **ArmBand family** — searchable ArmBand-hosted accessories, including the proven `1x2` magazine container and Wrist Wallet support.
- **Belt** — dedicated equipment slot **15** with the accepted `2x2` magazine-only container runtime.
- **HeadBand** — dedicated equipment slot **16** with the accepted compact `1x2` utility-container runtime and exact item filtering.

The stable line preserves native EFT/SPT inventory behavior and does not use permanent per-frame inventory polling, scene-wide scans or global UI refresh loops.

## Protection

F12 exposes independent `Protected` / `LostOnDeath` behavior for ArmBand, Belt and HeadBand. Protection is exact-template/root scoped and includes descendants of the protected wearable. Death retention and insurance-loss suppression are installed as one atomic server feature.

## Install layout

Client DLL:

`SPT_Runtime/BepInEx/plugins/SPT Belt Armband Inventory v0.1.0.dll`

Server DLL and recovery material:

`SPT_Runtime/user/mods/B&A&HB #2 MOD SPT/`

The package must use one `SPT_Runtime` root. Legacy `Trenchfoot-BeltSlot.dll` must not be installed alongside B&A&HB because both target overlapping equipment behavior.

## Profile safety

Persistent B&A&HB IDs are immutable. Backup-first cleanup/recovery material is maintained under `profile-safety/`. Do not manually remove arbitrary profile nodes when disabling or uninstalling the mod.

## Stable runtime contract

The accepted SPT 4.1.3 baseline includes:

- first-open Character → Items presentation without refresh workarounds;
- responsive pre-raid/insurance navigation;
- dedicated slot 15/16 persistence and native container opening;
- bounded GridWindow sizing;
- PMC lifecycle persistence;
- exact family death/insurance protection behavior;
- startup-bound compatibility discovery with no idle inventory scanning.

Further product/design work is developed separately and must not modify this stable source until it passes its own release gate.
