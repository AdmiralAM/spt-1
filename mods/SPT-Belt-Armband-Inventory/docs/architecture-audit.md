# B&A&HB runtime architecture audit

This is the single-pass audit of the current ArmBand-hosted runtime candidate.
It records which layers are active, what they are allowed to mutate, and which
future categories may reuse them.

## Active layers

| Layer | Current responsibility | Mutation model | Status |
|---|---|---|---|
| Runtime type | Registers the searchable/container item and template | Ownership-tracked `JsonTypes` mappings | Proven by runtime test; fail-safe rollback |
| Native grid | Creates `GeneratedGridsView` from the real item grids | No custom layout prefab | Proven; `1x2` renders as two cells |
| Native window | Opens the equipped searchable item through EFT `GridWindow` | RC-only deterministic compact sizing | Proven native path; no ContainersPanel projection |
| Deferred runtime work | Retries late GridWindow sizing and refreshes fast-access state | Event-triggered coroutine, active only while work is pending | No production `Update()` loop |
| Loot/unload | Adds the container to placement priority | Read-only/result projection | Optional; vanilla fallback |
| Fast access | Adds reachable slots and synchronizes grenade state | Owned static-array replacement / event hooks | Ownership-safe; optional |
| Pickup/payment | Extends vanilla slot selection when compatible | Result post-processing | Optional; vanilla fallback |
| Scav/build | Preserves intended behavior through special lifecycle paths | Targeted result or instance mutation | Optional; fail closed |
| Death/insurance | Retains only the explicitly protected RC root and descendants | Server-side item-tree policy | Template-scoped; ordinary armbands keep vanilla rules |

## Ownership rules

1. A temporary/static value is restored only while the current value is still
   the value installed by this mod.
2. Runtime `JsonTypes` item/template/constructor registrations are prepared before
   mutation and rolled back on partial installation or plugin disposal when still owned.
3. A result list is never destructively rewritten when an ownership-safe projection
   or targeted append/remove is sufficient.
4. A missing obfuscated member disables only the affected optional feature.
5. A plain or empty ArmBand never becomes a container through category metadata.
6. Death/insurance retention is explicit per protected template; merely occupying
   `ArmBand` does not grant protected-container semantics.
7. No category may invent an EFT equipment enum value. Belt and HeadBand remain
   design categories until their real host boundaries are proven.

## Pack 'n' Strap boundary

Pack 'n' Strap remains a reference for server persistence, searchable-item
contracts and lifecycle intent. Its UI implementation is not copied wholesale.
The current client uses the SPT 4.1.3 searchable-item runtime contract and EFT's
native generated-grid window. The superseded ContainersPanel BELT-row projection,
panel-refresh chain and one-off runtime discovery/dump plugins have been removed
from production source.

## Performance contract

Production code is interaction/event driven. There is no `ItemView.Update`, no
scene-wide object scan, no hierarchy-wide polling scan, and no persistent Unity
`Update()` callback. Late UI work is scheduled only when a real RC GridWindow or
fast-access event queues work. CI executes `tools/check_hotpaths.py` so these
constraints are enforced rather than documented only.

## Deferred work

- pass the final continuous ArmBand RC physical Gate A before feature expansion;
- prove a real Belt host slot before enabling a second host;
- prove a real HeadBand host slot before enabling that category;
- only introduce custom layout prefabs when an actual asset and registration key
  exist;
- add category-specific lifecycle policies after the shared contract is reused
  by a second host.

## Audit conclusion

The current branch has one validated searchable/container runtime implementation
plus independently fail-soft lifecycle integrations. The safe extension point is
the category/capability/grid policy layer after Gate A, not another global UI
projection. The current pre-gate work should remain focused on correctness,
ownership-safe rollback, persistence, compatibility, and eliminating runtime hot
paths.
