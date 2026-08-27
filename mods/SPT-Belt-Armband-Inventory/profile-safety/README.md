# B&A&HB #2 MOD SPT — profile / uninstall safety

This directory is the recovery contract for SPT 4.1.3 profiles that contain persistent B&A&HB data.

## Authoritative persistent identities

`persistent-identities.json` is the machine-readable contract. Existing IDs are immutable and must not be renamed or reused.

### Item template IDs

- `68ac00000000000000000001` — ArmBand magazine-belt runtime candidate
- `68ac00000000000000000006` — Wrist Wallet
- `68ac0000000000000000000c` — dedicated magazine belt item
- `68ac0000000000000000000f` — emergency HeadBand item

### Parent IDs

- `68ac00000000000000000004` — searchable template parent
- `68ac00000000000000000005` — belt item parent
- `68ac0000000000000000000b` — HeadBand item parent

### Slot IDs

- wire slot IDs: `15`, `16`
- slot Mongo IDs: `68ac00000000000000000009`, `68ac0000000000000000000a`
- semantic slot identities: `BAndHBBelt`, `BAndHBHeadBand`

### Grid IDs

- `68ac00000000000000000002`
- `68ac00000000000000000007`
- `68ac0000000000000000000d`
- `68ac00000000000000000010`

### Trader assort IDs

- `68ac00000000000000000003`
- `68ac00000000000000000008`
- `68ac0000000000000000000e`
- `68ac00000000000000000011`

The server-side `PersistentIdentityManifest` mirrors this contract and CI regression coverage rejects drift between the JSON contract and runtime identities.

## Where persistent references can remain

An item instance with one of the item template IDs can survive in the PMC/Scav inventory tree, stash or equipment. References to that instance can also survive in nested container children, mail/message item payloads, insurance payloads and equipment/weapon build records. Those stale references are unsafe once the corresponding template is no longer registered in the items database.

## Safe disable / uninstall procedure

1. Exit raids and stop SPT before changing profile files.
2. While B&A&HB is still installed, remove its wearable items from equipment/stash when practical and collect or discard pending mail/insurance returns containing them.
3. Back up the affected profile JSON before cleanup.
4. Run `Clean-BAndHBProfile.ps1 -ProfilePath <profile.json>`. By default the tool writes a sibling `<profile>.bahb-clean.json` and does **not** overwrite the original.
5. Review the reported removal locations. Replace the original profile with the cleaned copy only after keeping the backup.
6. Disable/remove B&A&HB and start SPT with the cleaned profile.

## Recovery if the mod was already removed

If SPT already reports `InvalidModdedItemException` / `item found in profile that does not exist in items db`, keep the server stopped. Either temporarily restore the exact B&A&HB build that created the items and clean them while it is installed, or run the offline cleanup script directly against a backup/copy of the profile. The script uses only `persistent-identities.json`; it does not require B&A&HB item templates to be registered.

The cleanup is ownership-scoped: it removes B&A&HB template instances and serialized descendants/direct `parentId`/`itemId` references to those removed instances. It does not intentionally remove unrelated vanilla/mod items. A second cleanup pass is expected to be a no-op.

## Proof boundary

CI regression proves the deterministic recovery policy, identity-contract parity and package contents. It does **not** prove the exact physical SPT 4.1.3 profile-load lifecycle. Physical runtime evidence remains a separate gate before this behavior can be described as runtime-proven.
