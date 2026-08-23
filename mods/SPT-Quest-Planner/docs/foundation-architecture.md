# Quest Planner — Foundation Architecture

## Goal

Create a planner that answers, from authoritative SPT runtime data:

1. What quests are active now?
2. What quests are reachable later and through which prerequisite chain?
3. What items/levels/trader states/other conditions are still required?
4. Which requirements are outstanding versus already satisfied?
5. What should be kept now because it will be required by reachable future quests?

Presentation is explicitly out of scope for this phase.

## Runtime data path to prove

The implementation must verify each boundary rather than infer it from type names or unit tests:

`SPT quest database -> player PMC profile quest state -> server snapshot/route -> serialized contract -> client bootstrap/cache -> normalized planner graph -> outstanding requirement calculation -> presentation`

The first implementation milestone is a diagnostic-capable data snapshot that proves quest IDs, statuses, prerequisite conditions and item conditions survive each boundary.

## Module boundaries

### Server

Owns authoritative extraction from SPT server APIs and emits a compact planner snapshot. It should expose only data required to construct the planner graph and calculate player-relative state.

### Core/domain

Pure logic with no Unity/UI dependencies:

- normalized quest nodes;
- prerequisite edges;
- condition groups;
- item requirement records;
- player quest-state projection;
- graph traversal/reachability;
- outstanding requirement aggregation;
- cycle detection and malformed-data guards.

### Client adapter

Fetches/refreshes snapshots at bounded lifecycle events and owns the local immutable/cached planner state. No per-frame server calls or full graph rebuilds.

### Presentation

Deferred until the domain model is validated. It may query planner state but must not contain requirement calculation logic.

## Initial normalized model

Minimum conceptual entities:

- `QuestNode`: quest id, trader id, name/localization key, level gate, repeatable flag, start/finish conditions.
- `QuestState`: Locked, Available, Started, Success, Failed, Unknown.
- `PrerequisiteEdge`: source quest, target quest, required source status, logical grouping metadata.
- `RequirementCondition`: normalized condition kind plus source condition id.
- `ItemRequirement`: template id(s), required count, FIR/other flags where applicable, phase/context.
- `PlannerSnapshot`: revision/timestamp, player projection, quest nodes, condition data.
- `PlannerGraph`: immutable indexed representation derived from snapshot.

Exact field shapes must follow observed SPT 4.1.x runtime objects, not assumptions from older SPT/AKI versions.

## Performance rules

- Build quest graph once per snapshot revision, not per frame.
- Index nodes and item requirements by IDs/templates for O(1)-style lookup where practical.
- Recompute only player-dependent outstanding values when profile state changes.
- Avoid reflection in hot paths; if reflection is required for obfuscated EFT client types, resolve/cache members once.
- Do not deserialize the same server payload repeatedly.
- Large custom quest packs are a required stress case.

## Relationship to Item Intelligence

Item Intelligence currently has a server-side requirement snapshot path based on PMC profile plus `TemplateTable.Quests` and hideout data. That proves useful SPT services and tables exist, but Quest Planner must not import Item Intelligence runtime/presentation classes as dependencies.

Two acceptable future approaches after measurement:

1. Quest Planner owns its own planner-specific route and DTOs; or
2. a deliberately shared, versioned data-contract layer is extracted if both modules need identical authoritative data and duplicate work is material.

Default for foundation work: keep Quest Planner independent and own its route.

## First archaeology targets

Inspect and document current SPT 4.1.x representations for:

- `TemplateTable.Quests` quest object shape;
- PMC profile quest/status storage;
- start conditions and prerequisite quest conditions;
- finish conditions, especially item handover/FIR/count semantics;
- player level/trader loyalty/reputation gates;
- repeatable/daily quest separation;
- localization/name resolution;
- profile lifecycle points that can trigger a bounded refresh.

## Stop condition for foundation phase

Do not start planner UI until all of the following are true:

- authoritative quest + profile paths are identified;
- normalized model maps real SPT data without lossy assumptions;
- prerequisite graph can be built and validated;
- outstanding item requirements can be calculated for at least representative active and future quests;
- refresh strategy has no per-frame polling;
- malformed/custom quest data fails safely.
