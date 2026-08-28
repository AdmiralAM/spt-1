# B&A&HB #2 MOD SPT

Wearable inventory extension for SPT 4.1.3. Current development is the profile-safe dedicated wearable package recorded by the repository `belt` workstream and PR #64.

Current module version: **0.1.0**.

## Current runtime model

B&A&HB keeps three explicit wearable categories with narrow item-specific capabilities:

- **ArmBand** — the proven searchable-container foundation on the vanilla `ArmBand` equipment host. The runtime candidate remains exact native `1x2`, magazine-only, with bounded event-driven container integration. Wrist Wallet is a separate ArmBand-family persistent item and is not silently granted unrelated capabilities.
- **Belt** — dedicated equipment pseudo-slot **15**, positioned between Pockets and Backpack. The exact Magazine Belt uses its own persistent parent/item/grid/assort identities and a `2x2` magazine-only grid in the current mechanical candidate.
- **HeadBand** — dedicated equipment pseudo-slot **16**, presented as a compact strip immediately above Headwear. The exact Utility HeadBand uses its own persistent parent/item/grid/assort identities and a narrow `1x2` utility grid. Its whitelist is exact-item only: RUB, USD, EUR, Apollo Soyuz, Malboro, Wilston, Strike and the small vanilla Wallet. It is deliberately not a generic secure or medical container.

Distributed identities are immutable. They are recorded in the persistent identity manifest and must never be renamed, reused for another category, or silently dropped.

## Death / insurance protection

F12 exposes an independent **Protection** setting for every wearable family:

- `ArmBand`: `Protected` / `LostOnDeath`;
- `Belt`: `Protected` / `LostOnDeath`;
- `HeadBand`: `Protected` / `LostOnDeath`.

All three default to `Protected`. Protection is exact-root scoped: only registered B&A&HB wearable templates in their intended equipment hosts are protected, together with their complete descendant inventory trees. An arbitrary vanilla or third-party item does not become protected merely because it occupies ArmBand or pseudo-slot 15/16. `LostOnDeath` removes B&A&HB special retention for that family and delegates normal loss behavior to SPT.

The server publishes protection roots as one immutable atomic snapshot. Death retention and insurance-loss filtering consume the same snapshot, and the two SPT 4.1 runtime patches are installed as one DI-managed feature with rollback if either half cannot bind. This prevents a protected tree from being retained while still generating false lost-insured events, or insurance suppression from surviving without matching death retention.

## Dedicated presentation and routing

The client projects Belt and HeadBand through native EFT equipment/slot boundaries rather than permanent UI polling:

- dedicated equipment slots are registered against the SPT 4.1.3 `InventoryEquipment` boundary;
- exact item filters prevent cross-category placement;
- Belt and HeadBand use dedicated wire slot IDs `15` and `16`;
- HeadBand presentation is created only from the native `SlotView.Show` lifecycle; the earlier provisional `EquipmentTab.Awake` Headwear clone was removed after physical RC evidence showed a broken first-entry layout;
- exact dedicated-item Alt-pickup resolves to the matching dedicated slot when it is empty and compatible;
- visible dedicated captions are owned by dedicated presentation policy, including EN/RU labels; the Belt ContainersPanel row receives a final bounded post-`Show` numeric-caption normalization because physical RC evidence proved the outer row could otherwise retain `15`;
- existing ArmBand container mechanics remain isolated from the new dedicated locations.

## Scav / PMC lifecycle boundary

PMC behavior remains on the ordinary inventory lifecycle. Scav `ReplaceInventory` compatibility is deliberately narrow: SPT/EFT runtime members `Inventory`, `Equipment`, `ContainedItem`, `Deleted` and `StringTemplateId` are resolved once during patch installation as a property or field, cached delegates are generated, and the postfix inspects only the three wearable equipment slots. There is no reflection scan per replacement, no scene/inventory sweep and no idle polling.

Only wearable descriptors with the explicit Scav-host restoration capability can clear the transient deleted flag. Unrelated items and unregistered templates fail closed.

## Profile and uninstall safety

B&A&HB persistent data is treated as a lifecycle contract, not only a runtime feature. The package includes:

- one authoritative persistent-identity manifest covering current distributed template, parent, slot, grid and assort IDs;
- backup-first ownership-scoped recovery tooling;
- deterministic cleanup regression coverage for stash/equipment, mail, insurance, build references and descendants while B&A&HB templates are absent;
- preservation of unrelated profile data and idempotent repeated cleanup;
- documented update/disable/uninstall recovery procedure packaged with the candidate.

Do not manually delete arbitrary profile nodes. Use the packaged recovery contract when disabling or uninstalling a build that has already written B&A&HB identities to a profile.

## Performance contract

Production client behavior is interaction/event driven:

- no `ItemView.Update` polling;
- no permanent production `MonoBehaviour.Update` loop;
- no scene-wide object scans;
- no hierarchy-wide polling;
- no repeated reflection in guarded hot paths where startup-bound delegates can be used;
- deferred GridWindow work is bounded and drains to zero;
- HeadBand visual creation is owned by native `SlotView.Show`, not `EquipmentTab.Awake`;
- Belt caption repair is bounded to the exact factory-created row at `ContainersPanel.Show` completion;
- server lifecycle target discovery is bounded and fails closed on zero/ambiguous candidates.

CI includes deterministic hot-path and runtime-contract guards.

## Repository layout

- `src/` — client runtime registration, dedicated equipment projection and wearable integrations;
- `server/` — SPT 4.1.3 item, slot, trader and lifecycle integration;
- `tests/` — deterministic profile, identity, routing, presentation and lifecycle regressions;
- `tools/` — validation and profile-recovery tooling;
- `docs/` — architecture, archaeology, runtime contracts and recovery documentation.

## Compatibility

Pack 'n' Strap and Trenchfoot BeltSlot are reference/archaeology sources, not runtime dependencies. If legacy `Trenchfoot-BeltSlot.dll` is installed, remove or disable it before using B&A&HB so two implementations do not patch the same host behavior.

The server project targets SPT 4.1.3 `SPTushonka.*` packages.

## Release-candidate boundary

PR #64 is the single implementation/evidence record. Automated profile-safety, dedicated-slot, lifecycle/native-polish and release-hardening work must be represented by one exact-head module CI and packaged artifact before another physical handoff is valid. `docs/RC1-runtime-checklist.md` tracks the combined runtime gate and must describe the actual current mechanics rather than historical candidates.

Internal commits, CI runs and artifacts are evidence, not stable acceptance. On physical FAIL the first concrete boundary is remediated automatically; on PASS the recorded stable-release phase proceeds under the user's standing authorization.
