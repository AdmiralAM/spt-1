# Admiral Compatibility Suite

One user-facing compatibility package for Admiral extensions of third-party SPT
mods. Components remain isolated internally so one upstream update cannot disable
the whole suite.

## Ownership boundary

- Item Intelligence owns its optional Amands Sense presentation.
- Admiral Trader owns Painter, Artem and TGC storefront/quest consolidation.
- Compatibility Suite owns Foldables Extended, Stackable Armor Plates, fixes to
  third-party behavior, and cross-mod adapters currently embedded in Belt.
- Belt keeps its native Belt/ArmBand/HeadBand product behavior. Its UI Fixes,
  Use Items Anywhere, TGC and Pack 'n' Strap adapters are migration candidates.

`manifests/compatibility-ownership.json` is the machine-readable authority. The
audit tool inventories dependencies, Harmony patches, reflection, template
mutation and file/config replacement without treating every reflection use as a
foreign integration.

## Initial components

- `SPT-Foldables-Extended`
- `SPT-Stackable-Armor-Plates`
- Suppressor Balance (server-side): leaves the suppressor's existing ergonomics,
  recoil, loudness and thermal stats alone; halves only durability-burn penalty
  above neutral `1.0`, and adds small bonuses to accuracy (`+1.5`), muzzle
  velocity (`+2`) and effective range (`+2`). Covers suppressors in the vanilla
  category and mod-added subcategories.
  It disables itself if standalone BetterSuppressors is loaded, avoiding a double
  adjustment. This component is inspired by the upstream BetterSuppressors
  balance approach; its implementation is independent and contains no copied
  upstream source or assets.
- Healing Interrupt (separate client DLL, runtime candidate): End cancels the
  current medical/consumable action using EFT's own cancellation and weapon
  switch, then bypasses the default 600 ms consumable outro while retaining
  its callback. Does not clear inventory events or force-reset hands. The
  emergency HandsAreNotBusy reset must not share End; neither should Dynamic
  Maps. The outro transition was informed by Manimal's Quick Cancel (MIT,
  copyright 2026 danauraborealis), but unlike that mod it never throws the
  medical item onto the ground. Physical in-raid acceptance remains required.

They retain their existing GUIDs and runtime paths while being distributed and
versioned as Suite components.

The installed Use Items Anywhere 2.1.3 DLL remains upstream-owned and
unmodified. Admiral currently extends it from Belt-side compatibility code; the
Suite now owns an idempotent external adapter that makes dedicated Belt slot15
follow every ArmBand-enabled Use Items Anywhere list. The foreign DLL remains
untouched. Removal of the superseded Belt-side copy is a separate Belt-owned
change, so mixed-version installations remain safe during migration.

The Suite also owns the narrow UI Fixes bridge. It patches only UI Fixes'
in-raid `LoadAmmoByType` replacement and appends ammo returned by Belt's public
`BeltAccessApi` v1. Belt retains the complete native reload/access engine. A
missing API, contract mismatch or changed UI Fixes call site fails closed and
leaves the native/UI Fixes result untouched.

## TGC and Pack 'n' Strap migration

The remaining Belt integrations are no longer represented by a vague
"embedded" label. The ownership manifest pins the reviewed Belt head and the
exact runtime files involved. TGC currently crosses slot15 filters, pickup,
fast access, secure-container policy and existing-profile migration. Pack 'n'
Strap crosses companion detection, slot publication, secure-container policy,
fast access and the opt-in private importer. These are real runtime seams, not
mere documentation references.

They move only after Belt publishes a minimal versioned handoff. Belt continues
to own slot15 behavior and profile identities; Pack 'n' Strap retains all of
its content/assets; Compatibility Suite will own only optional cross-mod
orchestration. The audit report now labels runtime, test, documentation and
build-tool evidence separately so historical notes cannot be mistaken for a
loaded patch.
