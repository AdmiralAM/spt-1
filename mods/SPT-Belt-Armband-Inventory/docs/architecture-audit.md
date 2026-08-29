# B&A&HB #2 MOD SPT runtime architecture audit

This audit describes the current magazine-only ArmBand-hosted runtime candidate.
Phase 1 is deliberately kept narrow until the complete physical lifecycle gate passes.

## Active layers

| Layer | Current responsibility | Mutation model | Status |
|---|---|---|---|
| Runtime type | Registers the searchable/container item and template | Ownership-tracked `JsonTypes` mappings | Proven by runtime test; fail-safe rollback |
| Native grid | Creates `GeneratedGridsView` from the real item grid | No custom layout prefab | Proven; exact `1x2` |
| Native window | Opens the equipped searchable item through EFT `GridWindow` | RC-only compact sizing | Native path; no ContainersPanel projection |
| Deferred window work | Finishes late RC window sizing | Event-triggered bounded coroutine | Max 30 frames after an RC window event; no idle loop |
| Loot/unload | Adds the magazine belt to placement/reload-relevant container paths | Targeted result projection | Optional; vanilla fallback |
| Reachability | Adds ArmBand to the vanilla reachable/bindable container arrays | Ownership-safe static-array replacement | Active for magazine/reload behavior |
| Pickup | Falls back to empty compatible ArmBand only when vanilla has no destination | Result post-processing | Optional; vanilla result always wins |
| Scav/build | Preserves the ArmBand container through special lifecycle paths | Targeted result/instance mutation | Optional; fail closed |
| Merge | Uses item-owned merge semantics for empty/container ArmBand only | Getter result override | Plain occupied armbands retain vanilla behavior |
| Death/insurance | Retains only the explicitly protected RC root and descendants | Server-side item-tree policy | Template-scoped; patch registration fail-soft |

## Dormant Phase 2+ capabilities

The current RC accepts **magazines only**. Therefore the following code paths are
not installed in Phase 1:

- grenade-slot enumeration;
- grenade fast-access UI synchronization;
- payment-source enumeration;
- legacy ContainersPanel BELT-row projection.

Their policy/source scaffolding may remain for later concrete wearable variants,
but capability gates keep them inactive now. This reduces Harmony surface and avoids
patching unrelated EFT systems before they are actually needed.

## Ownership and compatibility rules

1. A temporary/static value is restored only while the current value is still the
   value installed by this mod.
2. Runtime `JsonTypes` item/template/constructor registrations are prepared before
   mutation and rolled back on partial installation or plugin disposal when still owned.
3. Vanilla operation results win; fallback behavior is used only when the vanilla path
   yields no valid result.
4. A missing SPT/EFT boundary disables only the affected optional feature.
5. A plain occupied ArmBand keeps vanilla merge semantics and never becomes a container.
6. Death/insurance retention requires the explicit RC template ID, not merely `ArmBand`.
7. No category may invent an EFT equipment enum value. Belt and HeadBand remain design
   categories until their real host boundaries are proven.

## Performance contract

Production code is event/interaction driven:

- no `ItemView.Update`;
- no production `MonoBehaviour.Update`;
- no scene-wide `FindObjectsOfType` scan;
- no hierarchy-wide polling;
- reusable reflection member/type lookup is cached;
- the only deferred UI retry is started by an observed RC `GridWindow`, is capped at
  30 frames, and stops immediately when sizing succeeds or the cap is reached.

CI executes `tools/check_hotpaths.py` before regression tests/builds.

## Pack 'n' Strap boundary

Pack 'n' Strap remains an archaeology reference for searchable-item contracts,
persistence and lifecycle intent. B&A&HB #2 does not port its old UI/polling stack.
The current implementation uses SPT 4.1.3 item registration plus EFT native
`GridWindow` / `GeneratedGridsView` behavior.

## Current completion gate

Before broader product work, one exact-SHA build must prove in one continuous test:

1. clean client/server startup;
2. RC equips in ArmBand;
3. native searchable window opens;
4. exact `1x2` rendering;
5. magazine remove + insert;
6. close/reopen persistence;
7. loaded RC unequip/re-equip persistence;
8. automatic pickup to empty compatible ArmBand;
9. magazine reachability/reload from the belt;
10. profile/raid boundary persistence without duplication or loss.

Only after those pass does development move to the broader Belt / Armband / HeadBand
concept.
