# B&A&HB #2 MOD SPT

Wearable inventory extension for SPT 4.1.3. Current development is the profile-safe dedicated wearable package recorded by the repository `belt` workstream and PR #64.

Current module version: **0.1.0**.

## Current runtime model

B&A&HB keeps three explicit wearable categories with narrow item-specific capabilities:

- **ArmBand** — the proven searchable-container foundation on the vanilla `ArmBand` equipment host. The runtime candidate remains exact native `1x2`, magazine-only, with bounded event-driven container integration.
- **Belt** — dedicated equipment pseudo-slot **15**, positioned between Pockets and Backpack. The exact Magazine Belt uses its own persistent parent/item/grid/assort identities and a `2x2` magazine-only grid.
- **HeadBand** — dedicated equipment pseudo-slot **16**, presented above Headwear. The exact Emergency HeadBand uses its own persistent parent/item/grid/assort identities and a `1x1` medical-only grid.

Distributed identities are immutable. They are recorded in the persistent identity manifest and must never be renamed, reused for another category, or silently dropped.

## Dedicated presentation and routing

The client projects Belt and HeadBand through native EFT equipment/slot boundaries rather than permanent UI polling:

- dedicated equipment slots are registered against the SPT 4.1.3 `InventoryEquipment` boundary;
- exact item filters prevent cross-category placement;
- Belt and HeadBand use dedicated wire slot IDs `15` and `16`;
- HeadBand presentation is created from the actual EquipmentTab/SlotView lifecycle and rebound to pseudo-slot 16;
- exact dedicated-item Alt-pickup resolves to the matching dedicated slot when it is empty and compatible;
- visible dedicated captions are owned by the dedicated presentation policy, including EN/RU labels;
- existing ArmBand container mechanics remain isolated from the new dedicated locations.

## Profile and uninstall safety

B&A&HB persistent data is treated as a lifecycle contract, not only a runtime feature. The package includes:

- one authoritative persistent-identity manifest covering current distributed template, parent, slot, grid and assort IDs;
- backup-first ownership-scoped recovery tooling;
- deterministic cleanup regression coverage for B&A&HB parent/child references while templates are absent;
- preservation of unrelated profile data;
- documented update/disable/uninstall recovery procedure packaged with the candidate.

Do not manually delete arbitrary profile nodes. Use the packaged recovery contract when disabling or uninstalling a build that has already written B&A&HB identities to a profile.

## Performance contract

Production client behavior is interaction/event driven:

- no `ItemView.Update` polling;
- no permanent production `MonoBehaviour.Update` loop;
- no scene-wide object scans;
- no hierarchy-wide polling;
- no repeated reflection in hot paths where startup-bound delegates can be used;
- deferred GridWindow work is bounded and drains to zero;
- dedicated-slot presentation is driven by native SlotView lifecycle calls.

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

## Active acceptance path

PR #64 remains the single implementation/evidence record. The current package must preserve the proven ArmBand/Belt behavior while completing dedicated HeadBand presentation, exact routing, EN/RU captions, lifecycle/persistence and profile-safe recovery. After that, work continues directly through full lifecycle/native polish and release hardening.

The user runtime gate remains queued by repository governance. CI, commits, PR state and intermediate artifacts are not acceptance by themselves; the final release candidate requires one exact-head batched SPT 4.1.3 runtime gate before stable promotion.
