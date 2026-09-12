# Pack 'n' Strap ownership and B&A&HB compatibility plan

This document records the source audit and the first companion implementation
performed on 2026-09-12. It does not claim physical runtime acceptance and does
not change the profile. Pack 'n' Strap is to be installed and updated as its own
standalone mod; it is not bundled into B&A&HB.

## Pinned inputs

- Compatibility audit target: **SPT 4.1.5**. This is the exact version selected
  for the first combined build/test cycle, distinct from a supported range.
- Pack 'n' Strap: upstream tag **2.1.1**, commit
  `aa802d1bb2a4aefa96e3c14558b06a2bafe8cce1`, advertised for SPT 4.1.5 and
  WTT CommonLib 3.0.6.
- Full B&A&HB v0.2 implementation reserve: published PR #286 head
  `28bb0e8f90d86d2570d37a498effc868a29580cf`.
- Accepted B&A&HB v0.1 package: `runtime-belt-armband` at
  `e7f6a9cba15d17ad688b267a80392b37ba9500bc`, backed by tag
  `bahb-v0.1.0`.

PR #286 produced the complete v0.2 CI package `B&A&HB #2 MOD SPT RC1` in
Actions run `33839453178`, artifact ID `9924414624`, SHA-256
`31a06b0174f931716e1af4c16af760fd34e1f1daa7804b9f650d361be8a5084b`.
That Actions artifact is not a durable release asset and may expire. No durable
v0.2 runtime package has been published. The exact remote commit plus the
repository build/package workflow are therefore the reproducible full-v0.2
reserve. The reserve is not asserted compatible with SPT versions newer than
the one it was built and validated against.

Do not install the reserve beside the successor. Both use the same BepInEx and
server GUIDs and the same runtime paths; an in-place replacement is the only
supported package shape.

## What Pack 'n' Strap 2.1.1 owns

Pack 'n' Strap already supplies the standard product surface we do not intend
to rewrite:

- a catalogue of battle belts and fanny-pack style belt layouts;
- ordinary and secure purpose-built mini-containers and pouches;
- custom belt/container item classes and layout assets;
- ArmBand-hosted belt presentation in character, loot, Scav transfer,
  insurance, pre-raid and equipment-build screens;
- container destination priority, pickup, payment, grenade, unload,
  bindability, reachability and fast-access integration;
- its own exact belt-ID death-retention and insurance-loss policy;
- server item/parent/trader registration through WTT CommonLib.

Pack 'n' Strap uses the vanilla `EquipmentSlot.ArmBand` as its belt host. It
does not register B&A&HB's integer-backed slot 15 or HeadBand slot 16. Its
2.1.1 server GUID is `com.wtt.packnstrap`; its BeltSlot client declares that
GUID as a dependency. The release requires WTT CommonLib 3.0.6.

## Confirmed overlap and conflict surface

| Area | Pack 'n' Strap 2.1.1 | Full B&A&HB v0.2 reserve | Result |
| --- | --- | --- | --- |
| Equipment host | Reinterprets vanilla ArmBand as a belt/container host and changes the visible container-slot order | Extends ArmBand and registers dedicated slot 15 Belt plus slot 16 HeadBand | Concurrent ownership of ArmBand and equipment presentation; slot 15/16 themselves do not collide |
| UI | Patches container panels, inventory/loot/Scav/insurance/pre-raid/build screens and refreshes the ArmBand/belt projection | Patches container panels, dedicated Belt projection, compact Face/HeadBand and lifecycle slot views | Confirmed shared UI boundaries; patch order can change presentation and refresh behavior |
| Filters and routing | Registers its belt/container classes and replaces or extends loot, pickup, payment, grenade, bind/reach and unload routing | Extends ArmBand filters and adds exact B&A item routing, unload and reload fallbacks | Confirmed shared inventory-routing boundaries; foreign filters must remain Pack 'n' Strap-owned |
| Reload and quick access | Adds ArmBand to fast-access/bind arrays and provides reachability/unload/grenade behavior | Adds dedicated Belt slot 15 and scoped Reload/QuickReload fallback for the B&A Magazine Belt | Both write fast-access/bind behavior; no source evidence proves their combined ordering or restoration safe |
| Death and insurance | Patches `IsItemKeptAfterDeath` and `HandleInsuredItemLostEvent` for Pack 'n' Strap belt IDs when its setting enables retention | Patches the same server methods for exact B&A wearable roots and descendants | Confirmed same patch targets; ownership sets differ, but ordering and combined results require runtime proof |
| Persistence | Publishes many upstream item/grid/assort IDs; Forge warns that profile changes may be permanent | Owns immutable B&A template, parent, grid, assort and slot IDs plus backup-first cleanup/migration | No GUID/ID collision found in the inspected sources; no Pack 'n' Strap migration/cleanup contract was found, so removal safety is not assumed |
| Plugin identity | `com.wtt.packnstrap`, plus its bundled BeltSlot client and CommonLib dependency | `com.admiralam.spt.belt-armband-inventory` and `.server` | GUIDs differ, but GUID separation does not remove the shared patch/host conflicts |

Pack 'n' Strap's `InventoryController.ReplaceInventory` prefix skips the native
method, and its container-priority prefix replaces the native result. Those are
especially strong ownership points and make a broad "both full mods enabled"
mode unsuitable as the first compatibility implementation.

## Chosen path: ship only the missing Admiral features

The active runtime line is a narrow Admiral companion for Pack 'n' Strap.
Standard belts, pouches, mini-containers, their art/layouts and their ArmBand
UI/routing come from the separately installed Pack 'n' Strap. B&A&HB must not
register or patch an equivalent feature when Pack 'n' Strap already supplies
it. This absence-of-equivalent test, rather than provenance or licensing, is
the product boundary.

Targeted reuse from Pack 'n' Strap is allowed when it is needed to implement a
compatibility bridge or an Admiral feature that the standalone Pack 'n' Strap
does not provide. Such reuse stays limited to that missing behavior and records
its upstream file/commit plus the concrete compatibility reason. This does not
justify bundling the complete upstream mod or maintaining a second copy of
functionality already supplied by the standalone installation.

The Admiral-owned active surface will be:

1. **Utility HeadBand** in the immutable B&A HeadBand slot 16, including its
   two `1x1` utility cells and the existing backup-first profile migration.
2. **B&A&HB Dogtag Case** in the vanilla Dogtag slot, retaining canonical EFT
   Dogtag Case geometry and dogtag-only filters. It remains outside death
   retention, insurance suppression, fast access and equipment-build extension.
3. **Exact B&A-owned protection compatibility** for legacy/current B&A roots:
   Magazine Armband `68ac00000000000000000001`, Wrist Wallet
   `68ac00000000000000000006`, Magazine Belt
   `68ac0000000000000000000c`, and Utility HeadBand
   `68ac0000000000000000000f`, plus their serialized descendants only. Existing
   family settings remain the policy input. The Dogtag Case
   `68ac00000000000000000013` is excluded.

No Pack 'n' Strap template, parent, grid, assort or descendant receives Admiral
protection automatically. Compatibility code must never add upstream IDs to an
Admiral ownership manifest or mutate upstream filters to simulate support.
Legacy B&A identities stay immutable and cleanup remains ownership-bounded even
when their products are no longer newly offered by the active add-on.

The current repository change contains no Pack 'n' Strap code, bundles or art.
Tests pin its public GUID and expected ownership boundaries. A later change may include a small,
documented upstream-derived compatibility fragment under the rule above; the
Pack 'n' Strap installation itself remains external.

## Implemented companion gate

Client and server startup now detect Pack 'n' Strap through its public identity
(`com.wtt.packnstrap` on BepInEx and `WTT-PackNStrapServer` on the server). When
present, B&A&HB:

1. omits new Magazine Armband and Wrist Wallet templates and all three B&A
   standard-belt Ragman offers; the exact legacy Magazine Belt template remains
   registered only so profiles that already contain it are loadable;
2. omits the B&A Belt parent mapping and Belt row projection;
3. stops before B&A loot, unload, Scav, fast-access, merge, pickup, payment and
   equipment-build patches are installed;
4. retains Utility HeadBand slot 16 and its UI, Dogtag Case, profile migration,
   and exact-ID death/insurance protection.

The legacy Magazine Belt registration does not add it to Pack 'n' Strap filters,
does not publish a B&A Belt slot or trader offer, and does not extend Admiral
protection to any Pack 'n' Strap or Content Backport item. Removing this
persistent template while an existing profile contains
`68ac0000000000000000000c` causes SPT to invalidate the whole profile before
the HeadBand migration can run.

Automated build/regression proof is required before producing the combined
package. Physical runtime acceptance remains a later gate and must distinguish
the exact SPT 4.1.5 build from the declared `~4.1.0` support range.

## Installed WTT stack audit (2026-09-12)

The exact SPT 4.1.5 startup inventory was re-read after installation rather
than inferred from folder names:

| Installed module | Runtime version | Runtime GUID | Result with B&A&HB companion |
| --- | --- | --- | --- |
| WTT Armory | `2.0.5` | `com.wtt.armory` | Loads; weapon/ammunition content has no B&A persistent-ID or protection-root overlap |
| WTT Artem Revival | `3.0.0` | `com.crackbone.artem-wtt` | Loads; this is the installed WTT component that actually has version 3.0.0 |
| WTT Content Backport | `2.0.1` | `com.wtt.contentbackport` | Loads; extends armbands, containers and dogtag classes without receiving Admiral protection |
| WTT CommonLib | `3.0.6` | `com.wtt.commonlib` | Loads and serves its custom-parent/recipe/bundle endpoints; remains an external dependency of WTT, not a new B&A dependency |
| WTT Pack 'n' Strap | `2.1.1` | `com.wtt.packnstrap` | Loads and selects B&A companion mode |

Content Backport adds ordinary ArmBand templates under the vanilla armband
parent and adds its own secure/simple containers and dogtag variants. None of
the inspected 24-character template IDs collides with the B&A persistent
identity manifest. Its ordinary ArmBands have no B&A grid or custom searchable
parent, and B&A does not append those IDs to its owned families. Its containers
retain their own filters and item classes; HeadBand slot 16 accepts only the
exact B&A HeadBand product contract.

The first combined server startup exposed one real boundary: Content Backport's
expanded dogtag taxonomy means the live canonical EFT Dogtag Case grid no longer
satisfies B&A's untested strict source-owner preflight. Dogtag Case has not yet
passed its separate runtime milestone. B&A now fails that optional component
closed: it publishes neither the Dogtag Case template nor its Ragman offer,
leaves all foreign filters untouched, logs the exact reason, and continues
starting HeadBand plus exact Admiral-owned protection. This is an optional
compatibility gate and adds no WTT reference or mandatory dependency.

Admiral protection remains limited to the four exact B&A wearable roots listed
above and their descendants. Content Backport ArmBands, secure containers,
simple containers, dogtags and any Pack 'n' Strap products are outside that
set. Pack 'n' Strap may apply its own retention policy to its own products; that
behavior is not an Admiral protection claim.
