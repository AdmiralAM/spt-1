# B&A&HB #2 MOD SPT runtime architecture audit — archived Phase 1 snapshot

> **Historical evidence only.** This audit describes the original magazine-only ArmBand-hosted candidate before dedicated Belt/HeadBand hosts and the current v0.2.0 product/reload work existed. Statements below such as “current RC”, `ConceptOnly`, and the old completion gate are retained to document that phase and are not current authority. Active architecture is `../DESIGN-SPT-4.1.3-BELT.md`; active product/gate authority is `../README.md`, `product-concept.md`, and `RC1-runtime-checklist.md` under Issue #285 / PR #286.

This audit describes the then-current magazine-only ArmBand-hosted runtime candidate.
Phase 1 was deliberately kept narrow until its complete physical lifecycle gate passed.

## Active layers at that snapshot

| Layer | Then-current responsibility | Mutation model | Status at snapshot |
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

## Dormant Phase 2+ capabilities at that snapshot

The Phase 1 RC accepted **magazines only**. Therefore the following code paths were not installed in that phase:

- grenade-slot enumeration;
- grenade fast-access UI synchronization;
- payment-source enumeration;
- legacy ContainersPanel BELT-row projection.

Their policy/source scaffolding could remain for later concrete wearable variants, but capability gates kept them inactive then. This reduced Harmony surface and avoided patching unrelated EFT systems before they were needed.

## Ownership and compatibility rules

1. A temporary/static value is restored only while the current value is still the value installed by this mod.
2. Runtime `JsonTypes` item/template/constructor registrations are prepared before mutation and rolled back on partial installation or plugin disposal when still owned.
3. Vanilla operation results win; fallback behavior is used only when the vanilla path yields no valid result.
4. A missing SPT/EFT boundary disables only the affected optional feature.
5. A plain occupied ArmBand keeps vanilla merge semantics and never becomes a container.
6. Death/insurance retention requires the explicit RC template ID, not merely `ArmBand`.
7. At this historical point no category could invent an EFT equipment enum value; Belt and HeadBand were still design categories pending host proof. Current persistent dedicated slot15/slot16 design supersedes this historical constraint.

## Performance contract

Production code was event/interaction driven:

- no `ItemView.Update`;
- no production `MonoBehaviour.Update`;
- no scene-wide `FindObjectsOfType` scan;
- no hierarchy-wide polling;
- reusable reflection member/type lookup was cached;
- the only deferred UI retry was started by an observed RC `GridWindow`, capped at 30 frames, and stopped when sizing succeeded or the cap was reached.

The hot-path principle remains current even though the surrounding Phase 1 product scope is historical.

## Pack 'n' Strap boundary

Pack 'n' Strap remained an archaeology reference for searchable-item contracts, persistence and lifecycle intent. B&A&HB #2 did not port its old UI/polling stack. The implementation used SPT 4.1.3 item registration plus EFT native `GridWindow` / `GeneratedGridsView` behavior.

## Historical completion gate

The Phase 1 exact-SHA continuous test required:

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

That gate preceded the broader Belt / ArmBand / HeadBand product line. Current v0.2.0 acceptance is defined only by `RC1-runtime-checklist.md`.
