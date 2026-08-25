# B&A&HB runtime architecture audit

This is the single-pass audit of the current ArmBand-hosted runtime candidate.
It records which layers are active, what they are allowed to mutate, and which
future categories may reuse them.

## Active layers

| Layer | Current responsibility | Mutation model | Status |
|---|---|---|---|
| Runtime type | Registers the searchable/container item and template | Type tables during item-type initialization | Proven by runtime test |
| Native grid | Creates `GeneratedGridsView` from the real item grids | No custom layout prefab | Proven; `1x2` renders as two cells |
| ContainersPanel | Projects the validated ArmBand host as `BELT` | Temporary slot-array replacement | Active; ownership-safe restore |
| Panel refresh | Refreshes an already-open panel after host changes | Harmony event hooks | Optional; fails closed |
| Loot/unload | Adds the container to placement priority | Read-only result projection | Optional; vanilla fallback |
| Fast access | Adds reachable slots and synchronizes grenade state | Owned static-array replacement / event hooks | Ownership-safe; optional |
| Pickup/payment | Extends vanilla slot selection when compatible | Result post-processing | Optional; vanilla fallback |
| Scav/build/death | Preserves the container through special lifecycle paths | Targeted result or instance mutation | Optional; fail closed |

## Ownership rules

1. A temporary array is restored only while the current reference is still the
   array installed by this mod.
2. A result list is never modified in place when a copied projection is enough.
3. A missing obfuscated member disables only the affected feature.
4. A plain or empty ArmBand never becomes a container through category metadata.
5. No category may invent an EFT equipment enum value. Belt and HeadBand remain
   design categories until their real host boundaries are proven.

## Pack 'n' Strap boundary

Pack 'n' Strap remains a reference for server persistence, searchable-item
contracts and lifecycle intent. Its UI implementation is not copied wholesale.
The current client uses the native generated-grid path because a custom
`GridLayoutComponent` without a matching client prefab produces no contained
view. This is a deliberate compatibility boundary, not an unfinished fallback.

## Deferred work

- prove a real Belt host slot before enabling a second host;
- prove a real HeadBand host slot before enabling that category;
- only introduce custom layout prefabs when an actual asset and registration key
  exist;
- add category-specific lifecycle policies after the shared contract is reused
  by a second host;
- perform the next manual runtime pass only after a code change requires it.

## Audit conclusion

The current code has one validated runtime implementation and several optional
compatibility layers. The safe extension point is the category/grid policy layer,
not another global slot-sequence patch. Presentation changes remain isolated to
the existing `DynamicBeltPatches` lifecycle and must retain ownership checks.
