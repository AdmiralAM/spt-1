# B&A&HB #2 MOD SPT

Wearable inventory extension for SPT 4.1.x. The validated runtime foundation uses the
real EFT `ArmBand` equipment slot as the host for dedicated searchable container
items. Ordinary armbands remain ordinary. Belt and HeadBand remain later product
categories until their real EFT host boundaries are proven.

Current module version: **0.1.0**.

## Current ArmBand runtime foundation

The module has one shared searchable ArmBand runtime type with item-specific
descriptors. A descriptor owns geometry and optional integration capabilities so a
new wearable does not inherit behavior merely because it occupies `ArmBand`.

### Magazine belt runtime candidate

- dedicated custom searchable item/template runtime identity;
- real `ArmBand` equipment host;
- one exact native `1x2` grid;
- `MAGAZINE`-only filter;
- native EFT `GridWindow` + `GeneratedGridsView` presentation;
- exact-fit event-driven window sizing;
- loot/unload priority integration;
- reachable-container / reload integration;
- automatic pickup fallback into an empty compatible ArmBand slot;
- equipment-build, Scav, merge, death and insurance lifecycle handling.

### Wrist Wallet proof item

The current Phase 2 proof reuses only the already-proven searchable ArmBand host and
runtime type. It has independent item/grid/assort identities and capabilities:

- exact native `1x1` grid;
- `RUB`, `USD` and `EUR` only;
- native searchable `GridWindow` presentation;
- exact-fit sizing from the item descriptor;
- payment-source enumeration;
- equipment-build container validation;
- registered-wearable parent/child merge semantics.

It deliberately does **not** receive magazine loot/unload priority, reload/fast-access,
Scav restoration, pickup fallback, grenade behavior, or death-retention policy unless
those behaviors are separately justified and validated for this item.

The old experimental `ContainersPanel` BELT-row projection is not installed in
production.

## Capability isolation

Runtime behavior that can differ between wearable items is selected from the exact
item template descriptor. The magazine belt and Wrist Wallet therefore share a host
without sharing unrelated policies.

Host-level compatibility that is inherently attached to the EFT `ArmBand` slot is
kept narrow and ownership-safe. Plain armbands are never reclassified as searchable
wearable containers.

## Performance contract

The client is interaction/event driven:

- no `ItemView.Update` polling;
- no production `MonoBehaviour.Update` loop;
- no scene-wide object scans;
- no hierarchy-wide polling;
- deferred exact-fit window work only after a registered wearable window is observed;
- deferred work is bounded and terminates when its queue drains;
- reusable reflection lookups are cached.

CI runs a deterministic hot-path guard before tests/builds to reject regression to
those patterns.

## Repository layout

- `src/` — client runtime type registration and ArmBand wearable integrations;
- `server/` — SPT 4.1.3 item/trader/lifecycle integration;
- `tests/` — regression coverage for current runtime contracts;
- `tools/` — hot-path validation;
- `docs/` — runtime architecture, archaeology and later wearable design.

## Compatibility

Pack 'n' Strap and Trenchfoot BeltSlot are reference/archaeology sources, not runtime
dependencies. If legacy `Trenchfoot-BeltSlot.dll` is installed, remove or disable it
before using this module so two implementations do not patch the same host behavior.

The server project targets SPT 4.1.3 `SPTushonka.*` packages.

## Current runtime gate

The next physical runtime candidate must prove the shared ArmBand foundation and both
item descriptors together without regression:

- magazine belt still opens as exact native `1x2`, accepts magazines, and preserves
  its proven reachability/lifecycle behavior;
- Wrist Wallet opens as exact native `1x1`, accepts only supported currencies, and
  contributes those currencies through the vanilla payment-source path;
- switching between the two items does not leak item-specific behavior;
- plain armbands remain vanilla;
- no idle polling, duplication, loss, or persistence regression appears.

PR #64 remains Draft until the required exact-SHA physical runtime gate passes. Belt
and HeadBand host expansion remains out of scope for this ArmBand proof.