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

## Validation and publication

Pull requests that change this module run the dedicated Quest Planner test/client/server workflow. CI build outputs are GitHub Actions artifacts and are not committed to `main`.

Quest Planner does not currently have a permanent install-only runtime branch in the suite publication workflow. Until that channel is deliberately added, use validated development builds/CI artifacts rather than treating another module's runtime branch as a distribution channel.

See [foundation architecture](docs/foundation-architecture.md) for the original data-path and domain constraints. That document is architectural history; current source and tests are authoritative where implementation has advanced beyond the foundation milestone.
