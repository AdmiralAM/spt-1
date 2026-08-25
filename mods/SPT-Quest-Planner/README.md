# SPT Quest Planner

Standalone quest-planning module for SPT 4.1.x. Current client/server version: **0.9.4**.

Quest Planner is independent from Item Intelligence, Belt/Armband Inventory, Pause, and Tactical HUD. It owns its data contracts, server extraction, domain model, client cache, tests, persistence, recommendation logic, and presentation lifecycle.

## Purpose

The planner derives player-relative quest information from authoritative SPT data and turns it into two user-facing workflows:

- **Plan a Raid** — rank actionable locations, explain why a raid is recommended, show preparation requirements, let the player select a persistent Active Raid Plan, and present a concise in-raid checklist.
- **What to Do Next** — rank active/available quest progression targets, explain blockers/item burden/unlocks, and let the player keep a persistent progression focus.

Under those workflows the planner owns:

- quest topology and prerequisite chains;
- active, available, completed, and future progression state;
- quest conditions and item requirements;
- outstanding-versus-owned requirement calculations;
- readable quest/objective/location labels;
- bounded candidate selection and recommendation ranking;
- raid-oriented objective grouping and preparation checks;
- persistent planner state for active raid/progression selections;
- read-only selection/active-plan snapshots for future narrow integration with other modules.

## Architecture

- `server/` — extracts authoritative quest/profile data and exposes planner snapshots.
- `src/` — shared contracts, normalized quest/domain models, extraction, projection, and evaluation logic.
- `client/` — BepInEx client, bounded refresh/cache lifecycle, recommendation engine, raid-plan provider, persistence, and planner presentation.
- `tests/` — regression coverage for domain, extraction, projection, ranking, persistence semantics, and presentation contracts.
- `docs/` — architecture and durable design notes.

The planner does not use per-frame server calls or full graph recomputation. Expensive topology/database work is cached or refreshed at bounded lifecycle events.

## Independence contract

Quest Planner must not depend on another SPT mod's UI classes, runtime controllers, or private registries. Shared data may be introduced only through a narrow, explicit, versioned contract when duplication is materially worse than the coupling.

Item Intelligence can expose similar quest/item facts, but it is not Quest Planner's source of truth. The current client exposes read-only planner selection/active-plan snapshots so future integrations can consume planner decisions without owning planner internals.

## Validation and physical-test builds

Pull requests that change this module run the dedicated [Quest Planner Validate](https://github.com/AdmiralAM/spt-1/actions/workflows/quest-planner-validate.yml) workflow. The workflow tests the domain/runtime contracts, builds the client and server from the exact candidate SHA, and produces one installable `quest-planner-runtime-v<version>-<sha>` GitHub Actions artifact.

The physical-test artifact contains:

- `BepInEx/plugins/SPT-Quest-Planner/` — client DLL;
- `SPT_Runtime/user/mods/SPT-Quest-Planner/` — server DLL;
- `runtime-manifest.json` — version, exact source commit/PR/run provenance, automated-validation state, and runtime-validation state;
- `README.md` — candidate install/status note.

A candidate artifact is **not** runtime-accepted merely because CI passed. While Issue #80 remains open, `runtime_validation` must remain `pending` until the live SPT gate passes.

### Current runtime gate

Quest Planner 0.9.4 is feature-frozen pending physical validation. The live gate must verify:

1. F9 initial load has no schema/empty-response errors;
2. manual Refresh completes cleanly;
3. quest/inventory changes are reflected after refresh;
4. F9 can remain open long enough to expose any stale-open-window behavior without request spam/freezes;
5. closing/reopening F9 preserves and revalidates Active Raid Plan/progression focus correctly;
6. client/server logs contain no Quest Planner exceptions, schema mismatch, empty response, repeated-request spam, or stale-plan failures.

PR #81 is historical evidence only. Its 15-second `VisibleRefreshGate` must not be merged/rebased wholesale. Port that bounded-refresh concept onto current source only if the live 0.9.4 test proves meaningful open-window staleness.

### Permanent runtime channel

`runtime-quest-planner` is reserved as the future install-only Quest Planner publication channel. It must **not** be populated with an unvalidated candidate. After a candidate passes the physical SPT gate, publication may deliberately promote that exact accepted package to `runtime-quest-planner` with `runtime_validation=passed` and explicit source provenance.

See [foundation architecture](docs/foundation-architecture.md) for the original data-path and domain constraints. That document is architectural history; current source and tests are authoritative where implementation has advanced beyond the foundation milestone.
