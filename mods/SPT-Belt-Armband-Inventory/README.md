# B&A&HB #2 MOD SPT

Wearable inventory extension for SPT 4.1.3. Current development is the profile-safe dedicated wearable package recorded by the repository `belt` workstream and PR #64.

Current module version: **0.1.0**.

## Current runtime model

B&A&HB keeps three explicit wearable categories with narrow item-specific capabilities:

- **ArmBand** — the proven searchable-container foundation on the vanilla `ArmBand` equipment host. The runtime candidate remains exact native `1x2`, magazine-only, with bounded event-driven container integration.
- **Belt** — dedicated equipment pseudo-slot **15**, positioned between Pockets and Backpack. The exact Magazine Belt uses its own persistent parent/item/grid/assort identities and a `2x2` magazine-only grid.
- **HeadBand** — dedicated equipment pseudo-slot **16**, presented as a compact strip immediately above Headwear. The exact Emergency HeadBand uses its own persistent parent/item/grid/assort identities and a `1x1` medical-only grid for the current release candidate.

Distributed identities are immutable. They are recorded in the persistent identity manifest and must never be renamed, reused for another category, or silently dropped.

## Dedicated presentation and routing

The client projects Belt and HeadBand through native EFT equipment/slot boundaries rather than permanent UI polling:

- dedicated equipment slots are registered against the SPT 4.1.3 `InventoryEquipment` boundary;
- exact item filters prevent cross-category placement;
- Belt and HeadBand use dedicated wire slot IDs `15` and `16`;
- HeadBand presentation is created only from the native `SlotView.Show` lifecycle; the earlier provisional `EquipmentTab.Awake` Headwear clone was removed after physical RC evidence showed a broken first-entry layout;
- exact dedicated-item Alt-pickup resolves to the matching dedicated slot when it is empty and compatible;
- visible dedicated captions are owned by dedicated presentation policy, including EN/RU labels; the Belt ContainersPanel row receives a final bounded post-`Show` numeric-caption normalization because physical RC evidence proved the outer row could otherwise retain `15`;
- existing ArmBand container mechanics remain isolated from the new dedicated locations.

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

PR #64 is the single implementation/evidence record. Automated profile-safety, dedicated-slot, lifecycle/native-polish and release-hardening work is represented by the exact-head module CI and packaged artifact. The only remaining acceptance boundary is the one combined physical SPT 4.1.3 runtime gate described in `docs/RC1-runtime-checklist.md`.

The user decides when to execute that artifact. Internal commits, CI runs and artifacts are evidence, not stable acceptance. On physical FAIL the first concrete boundary is remediated automatically; on PASS the controller owns deliberate stable/publication promotion.
