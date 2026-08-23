# Phase 14 — anchored marker interaction

Phase 14 introduced the marker-only interaction prototype. Phase 16 supersedes its single-hovered-view lifetime with persistent per-view registration; the current approved default is upper-left.

## User interaction

1. Entering an EFT `ItemView` resolves the item template id and binds the overlay to that exact view.
2. A compact outlined marker is attached inside the upper-left corner of the item cell; F12 size defaults to 14 px and supports 10–28 px.
3. Hovering the item body does not open the three-line detail panel.
4. Moving the cursor onto the marker reveals Value, requirement fulfillment, Keep and the selected deeper details.
5. Exiting the item clears presentation hover state while the lifecycle-owned marker remains attached.

Every state uses `ⓘ`. Color represents only an unmet requirement, in priority order `Quest Now → Hideout → Quest Later → Default`; fulfilled requirements, price and generic Keep use Default.

## Runtime and performance boundary

- no `Update()` loop or network polling;
- each supported lifecycle-owned item cell receives one lightweight child text/outline marker;
- registered cells are retained without scanning, while tooltip hit testing examines only the currently hovered cell;
- marker classifications are shared immutable objects;
- the existing cached presentation text and one-shot Phase 13 snapshot remain unchanged;
- a missing/destroyed `RectTransform` suppresses only the visual marker and cannot block plugin loading.

Physical in-game validation remains the final visual and interaction gate.
