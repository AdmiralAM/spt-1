# Phase 1 runtime contract — archived snapshot

> **Historical evidence only.** This file records the original ArmBand-hosted Phase 1 candidate and its then-current acceptance gate. That phase is complete and the references below (including PR #64 and magazine-only/current-candidate language) are intentionally preserved as history, not current authority. For active v0.2.0 behavior use `../README.md`, `../DESIGN-SPT-4.1.3-BELT.md`, `product-concept.md`, and `RC1-runtime-checklist.md` under Issue #285 / PR #286.

Target: SPT 4.1.3 client + server runtime.

## Core implementation

- The equipment host is the real EFT `ArmBand` slot.
- The RC belt has a dedicated custom searchable item/template runtime identity.
- The client registers that identity directly in the SPT 4.1.3 `JsonTypes` item/template/constructor tables before inventory data is consumed.
- The RC exposes exactly one native `1x2` grid filtered to `MAGAZINE`.
- Opening the equipped RC uses EFT's native searchable-item `GridWindow` and `GeneratedGridsView`; there is no production `ContainersPanel` BELT-row projection.
- `Slot.MergeContainerWithChildren` returns `InheritFromItem` for `ArmBand` so the equipped root and children follow the container lifecycle together.
- Ordinary armbands remain ordinary armbands. Container behavior is capability/runtime-contract gated.

## Lifecycle coverage

The ArmBand-hosted container is integrated with:

- native grid open / insert / remove behavior;
- loot placement priority;
- unload placement priority;
- automatic pickup fallback into an empty compatible ArmBand slot;
- grenade enumeration and fast-access refresh;
- generic bind/reachability slot lists;
- payment-source enumeration;
- equipment-build container validation;
- Scav ArmBand host restoration;
- death retention and insurance-loss filtering for the RC root and descendants only.

Every optional client integration fails soft. Server lifecycle patch registration is isolated so one SPT boundary failure does not prevent server startup.

## Performance contract

- No `ItemView.Update` polling.
- No production `MonoBehaviour.Update` loop.
- No scene-wide `FindObjectsOfType` scan.
- No hierarchy-wide polling.
- Deferred UI work is event-triggered only and terminates when its pending queue is empty.
- Compact RC `GridWindow` sizing retries are bounded to a short 30-frame window only after an RC window is actually observed.
- Reflection type/member discovery is cached where it is reused.

The CI hot-path guard rejects reintroduction of the prohibited polling patterns.

## Historical acceptance gate

Phase 1 was defined as incomplete until one exact-SHA artifact passed a continuous physical lifecycle test:

1. client/server start without B&A&HB errors;
2. RC equips into `ArmBand`;
3. RC opens as a native searchable container;
4. the visible grid is exactly `1x2` and contains no filler cells;
5. a magazine can be removed and inserted again;
6. close/reopen preserves the content state;
7. unequip/re-equip preserves the loaded belt;
8. automatic pickup can route a compatible RC into an empty ArmBand slot;
9. grenade/fast-access state refreshes after loaded-belt equip/remove;
10. profile/raid boundary persistence retains the RC and its children without duplication or loss.

The historical statement that PR #64 should remain a diagnostic/runtime candidate until this gate passed belongs to that completed phase. Current development authority is Issue #285 / PR #286.
