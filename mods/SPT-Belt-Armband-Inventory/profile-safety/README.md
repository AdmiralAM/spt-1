# B&A&HB #2 MOD SPT — profile / uninstall safety

This directory is the recovery contract for SPT 4.1.3 profiles that contain persistent B&A&HB data. Stable v0.1.0 and development v0.2.0 share the existing distributed identities; v0.2.0 additionally distributes the Utility HeadBand cigarettes grid ID listed below.

## Authoritative persistent identities

`persistent-identities.json` is the machine-readable contract. Existing IDs are immutable and must not be renamed or reused.

### Item template IDs

- `68ac00000000000000000001` — Magazine Armband
- `68ac00000000000000000006` — Wrist Wallet
- `68ac0000000000000000000c` — Magazine Belt
- `68ac0000000000000000000f` — Utility HeadBand

### Parent IDs

- `68ac00000000000000000004` — searchable template parent
- `68ac00000000000000000005` — belt item parent
- `68ac0000000000000000000b` — HeadBand item parent

### Slot IDs

- wire slot IDs: `15`, `16`
- slot Mongo IDs: `68ac00000000000000000009`, `68ac0000000000000000000a`
- semantic slot identities: `BAndHBBelt`, `BAndHBHeadBand`

### Grid IDs

- `68ac00000000000000000002` — Magazine Armband `main`
- `68ac00000000000000000007` — Wrist Wallet `main`
- `68ac0000000000000000000d` — Magazine Belt `main`
- `68ac00000000000000000010` — Utility HeadBand `main` (currency/wallet)
- `68ac00000000000000000012` — Utility HeadBand `cigarettes` (introduced by v0.2.0)

### Trader assort IDs

- `68ac00000000000000000003` — Magazine Armband offer
- `68ac00000000000000000008` — Wrist Wallet offer
- `68ac0000000000000000000e` — Magazine Belt offer
- `68ac00000000000000000011` — Utility HeadBand offer

The server-side `PersistentIdentityManifest` mirrors the machine-readable groups, and CI regression coverage rejects drift between `persistent-identities.json`, server/runtime constants and slot semantic IDs.

## v0.2.0 split-grid migration boundary

On first v0.2.0 profile load, `BAndHBHeadBandSplitGridV1` converts actionable Utility HeadBand children from the old single 1x2 layout to the two native 1x1 grids before profile deserialization.

- one currency/wallet root is normalized into `main`;
- one cigarette root is normalized into `cigarettes`;
- PMC same-category overflow or unknown direct roots are moved to the PMC sorting table without deleting their descendant subtrees;
- Scav normally has no sorting table, so unclassifiable/overflow roots are preserved rather than deleted; after all actionable normalization is complete they do not keep the migration permanently pending;
- the migration is idempotent for fully actionable normalized content.

Because older builds do not know the v0.2.0 `cigarettes` grid identity, restoring the **pre-v0.2.0 profile backup** is the preferred rollback path.

## Where persistent references can remain

An item instance with one of the item template IDs can survive in the PMC/Scav inventory tree, stash or equipment. References to that instance can also survive in nested container children, mail/message item payloads, insurance payloads and equipment/weapon build records. Those stale references are unsafe once the corresponding template is no longer registered in the items database.

## Safe disable / uninstall procedure

1. Exit raids and stop SPT before changing profile files.
2. While B&A&HB is still installed, remove its wearable items from equipment/stash when practical and collect or discard pending mail/insurance returns containing them.
3. Back up the affected profile JSON before cleanup.
4. Run `Clean-BAndHBProfile.ps1 -ProfilePath <profile.json>`. By default the tool writes a sibling `<profile>.bahb-clean.json` and does **not** overwrite the original.
5. Review the reported removal locations. Replace the original profile with the cleaned copy only after keeping the backup.
6. Disable/remove B&A&HB and start SPT with the cleaned profile.

## Safe downgrade procedure

Treat a downgrade exactly like disable/uninstall unless the target build's packaged `persistent-identities.json` contains every B&A&HB identity already present in the profile **and** the target build understands the corresponding storage shape. Identity presence alone is not sufficient for v0.2.0 → v0.1.0 because v0.1.0 does not understand the new HeadBand `cigarettes` grid shape.

Preferred v0.2.0 → v0.1.0 rollback: stop SPT, restore the pre-v0.2.0 profile backup, then restore the complete stable v0.1.0 package.

For an uncertain or incompatible downgrade without a suitable backup: stop SPT, back up the profile, run the current build's packaged cleanup tool against a copy, verify the reported B&A&HB removals, then install the older build and start SPT with the cleaned profile. Do not reuse or renumber IDs to make an older build accept newer profile data.

## Recovery if the mod was already removed

If SPT already reports `InvalidModdedItemException` / `item found in profile that does not exist in items db`, keep the server stopped. Either temporarily restore the exact B&A&HB build that created the items and clean them while it is installed, or run the offline cleanup script directly against a backup/copy of the profile. The script uses only `persistent-identities.json`; it does not require B&A&HB item templates to be registered.

The cleanup is ownership-scoped: it removes B&A&HB template instances and serialized descendants/direct `parentId`/`itemId` references to those removed instances. It does not intentionally remove unrelated vanilla/mod items. A second cleanup pass is expected to be a no-op.

## Proof boundary

CI regression proves the deterministic recovery policy, persistent-identity parity, v0.2.0 split-grid migration behavior and package contents. It does **not** prove the exact physical SPT 4.1.3 profile-load lifecycle. Physical runtime evidence remains a separate combined gate before v0.2.0 is promoted from development candidate to stable release.
