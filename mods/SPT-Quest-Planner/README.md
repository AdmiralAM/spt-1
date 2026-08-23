# SPT Quest Planner

Standalone quest-planning module for SPT 4.1.x.

This module is intentionally independent from SPT Item Intelligence, SPT Belt/Armband Inventory, SPT Pause, and SPT Tactical HUD. It may consume shared data only through narrow explicit contracts; it must not depend on another mod's UI, lifecycle, internal registries, or runtime controller.

## Foundation phase

Initial work is architecture-only:

- map the real SPT 4.1.x quest/profile/database/runtime data path;
- define normalized quest, prerequisite, condition, item-requirement, trader, level and completion-state models;
- distinguish current actionable requirements from future requirements;
- build prerequisite graph and progression calculations outside presentation code;
- keep all expensive graph/database work event-driven or cached; no per-frame planner recomputation;
- establish a minimal independent server/client skeleton only after the runtime contracts are proven;
- no planner UI design until the data model and update boundaries are stable.

## Independence contract

`mods/SPT-Quest-Planner/` owns its source, server component, tests, documentation, configuration and build lifecycle.

Forbidden architectural dependencies:

- direct references to Item Intelligence presentation/runtime classes;
- calling Tactical HUD, Pause or Belt/Armband internals;
- using another mod's static registries as the planner source of truth;
- copying another module's cached state without a versioned contract.

Potential reuse is limited to stable DTO/HTTP-style contracts where doing so avoids duplicate server work. Quest Planner must still remain runnable and testable as its own module.

See `docs/foundation-architecture.md` for the first-pass architecture and research targets.
