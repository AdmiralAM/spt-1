# External compatibility ownership audit

This audit separates active runtime seams from historical notes and optional private build tooling at Belt HEAD.

## Active TGC 3.0.0 seams

- Client classification grants the five exact TGC belt templates Belt pickup, reload, loot, unload, build and Scav behavior.
- Server finalization moves those five templates from `ArmBand` to slot15, admits two exact pouch templates to supported Gamma containers, removes the Tool Box, and migrates legacy profile roots.
- These paths now remain inert until Admiral Compatibility Suite claims `ExternalCompatibilityApi` contract v1 through `TryClaimTgc300`. Missing Suite, a wrong contract version, or a duplicate owner is a no-op. Belt retains the exact IDs and mechanics so persistent identities and profile migration do not move across assemblies.

## Active Pack 'n' Strap 2.1.1 seams

- Companion mode suppresses competing Belt routes and product offers while retaining exact Admiral HeadBand, Dogtag, wallet payment and money-deposit behavior.
- Belt no longer detects the foreign BepInEx GUID or server assembly. Admiral Compatibility Suite must claim contract v1 through `TryClaimPackNStrap211` in each loaded Belt client/server assembly before Belt initialization.
- `SecureContainerCompatibilityPolicy` and final server filter work still recognize the established Pack 'n' Strap parent identities. These are Belt-owned host/filter mechanics; discovery and ownership selection are Suite-owned.

## Private build path

- `tools/Import-PackNStrapLocal.ps1`, conditional project properties, `LocalPackNStrapImport`, and imported JsonTypes registration are an opt-in private build pipeline, not a runtime adapter for an installed foreign mod.
- The public/default build does not compile third-party source or embed third-party assets. This change neither copied nor moved third-party material.

## Historical material

- `docs/archaeology.md`, `docs/packnstrap-compatibility.md`, RC checklists, integrated-repair notes and operational-hotfix fixtures record earlier investigations or validation candidates. They do not select external ownership at runtime.

The only supported Suite boundary is `ExternalCompatibilityApi` v1. It exposes exact product/version claims, no reflection surface, no arbitrary template registration and no mutable Belt internals.
