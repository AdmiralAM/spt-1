# SPT Item Intelligence — Phase 1

Version `0.1.0` adds one shared semantic layer over item/template objects already available in SPT. It is a standalone mod with its own DLL and lifecycle; it has no Tactical HUD dependency and does not alter Tactical HUD output.

## Data model

Every resolved item becomes an immutable `ItemDefinition` with:

- normalized template id;
- normalized full and short names;
- one category;
- zero or more semantic tags;
- an explicit known/unknown state.

Supported categories: `food`, `meds`, `ammo`, `weapon`, `armor`, `backpack`, `container`, `key`, `quest`, `barter`, `unknown`.

Supported tags: `healing`, `hydration`, `energy`, `bleed`, `pain`, `fracture`, `antidote`, `stimulant`, `throwable`, `armor`, `storage`, `quest`, `unknown`.

## Resolution contract

`ItemRegistry` is the only classification entry point. Resolution priority is:

1. exact template definition registered by id;
2. cached definition for a previously resolved template;
3. registered parent-template category;
4. semantic matching of SPT type/property signals;
5. explicit `unknown` fallback.

`ItemIntelligenceRegistry.Shared` is the canonical process-wide registry. The reflection adapter accepts a live item or template without adding compile-time EFT/SPT client dependencies. It reads stable semantic members such as template/parent ids, localized names, `QuestItem`, `Caliber`, `WeaponClass`, `ArmorClass`, `Grids`, resource values and effect collections. Template results are cached; exact and parent registrations invalidate affected cached results.

## Phase boundary

Phase 1 deliberately does not add:

- item colors or context-sensitive status;
- a new HUD indicator;
- hover tooltips;
- `take / keep / ignore` decisions;
- price, flea, trader, hideout, crafting or quest-demand economics;
- inventory scanning or recurring Unity object searches;
- any change to the existing HUD clusters or their runtime behavior.

Those consumers can be added later on top of the same registry without creating competing item classifiers.
