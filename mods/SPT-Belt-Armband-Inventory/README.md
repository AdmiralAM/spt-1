# B&A&HB #2 MOD SPT

Wearable inventory extension for SPT 4.1.x. The current validated implementation
uses the real EFT `ArmBand` equipment slot as the host for a dedicated searchable
container item. Belt and HeadBand remain later product categories; they are not
activated until the ArmBand implementation passes its full physical lifecycle gate.

Current module version: **0.1.0**.

## Phase 1: magazine belt on ArmBand

The current RC is intentionally narrow:

- dedicated custom searchable item/template runtime identity;
- real `ArmBand` equipment host;
- one exact native `1x2` grid;
- `MAGAZINE`-only filter;
- native EFT `GridWindow` + `GeneratedGridsView` presentation;
- compact RC-only window sizing;
- loot/unload priority integration;
- reachable-container / reload integration;
- automatic pickup fallback into an empty compatible ArmBand slot;
- equipment-build, Scav, merge, death and insurance lifecycle handling.

Ordinary armbands remain ordinary armbands. The old experimental `ContainersPanel`
BELT-row projection is not installed in production.

Grenade-view and payment-source patches are deliberately dormant in Phase 1 because
the current RC cannot contain grenades or money. Those capabilities return only when
a concrete later wearable variant requires them.

## Performance contract

The client is interaction/event driven:

- no `ItemView.Update` polling;
- no production `MonoBehaviour.Update` loop;
- no scene-wide object scans;
- no hierarchy-wide polling;
- deferred compact-window work only after an RC window is observed;
- deferred work is bounded and terminates when its queue drains;
- reusable reflection lookups are cached.

CI runs a deterministic hot-path guard before tests/builds to reject regression to
those patterns.

## Repository layout

- `src/` — client runtime type registration and ArmBand inventory integrations;
- `server/` — SPT 4.1.3 item/trader/lifecycle integration;
- `tests/` — regression coverage for the current runtime contracts;
- `tools/` — hot-path validation;
- `docs/` — runtime architecture, archaeology and later wearable design.

## Compatibility

Pack 'n' Strap and Trenchfoot BeltSlot are reference/archaeology sources, not runtime
dependencies. If legacy `Trenchfoot-BeltSlot.dll` is installed, remove or disable it
before using this module so two implementations do not patch the same host behavior.

The server project targets SPT 4.1.3 `SPTushonka.*` packages.

## Current gate

PR #64 remains a runtime candidate until one exact-SHA artifact passes the complete
ArmBand lifecycle in one continuous physical test: open the native `1x2` window,
remove/insert a magazine, close/reopen, unequip/re-equip loaded, auto-pickup into an
empty ArmBand, use magazine reachability/reload, and cross profile/raid persistence
without duplication or loss.

Only after that gate passes does development move on to the broader Belt / Armband /
HeadBand product concept.
