# Phase 14 — anchored marker interaction

Phase 14 introduced the marker-only interaction prototype. Phase 16 supersedes its single-hovered-view lifetime and upper-right placement with persistent per-view registration and an upper-left default.

## User interaction

1. Entering an EFT `ItemView` resolves the item template id and binds the overlay to that exact view.
2. A 16–22 px marker is drawn inside the upper-right corner of the item rectangle.
3. Hovering the item body does not open the three-line detail panel.
4. Moving the cursor onto the marker reveals price, value-per-slot and Safe-to-Sell/Keep detail.
5. Exiting the item clears both the presentation state and the marker anchor.

Marker meanings:

- green `✓` — Safe to Sell;
- amber `!` — Keep/required;
- blue `i` — neutral or no requirement record;
- grey `…` — live item data is loading;
- red `×` — live item data is unavailable.

## Runtime and performance boundary

- no `Update()` loop or network polling;
- no persistent GameObject is added to every inventory item;
- only the currently hovered `ItemView` is retained;
- marker classifications are shared immutable objects;
- the existing cached presentation text and one-shot Phase 13 snapshot remain unchanged;
- a missing/destroyed `RectTransform` suppresses only the visual marker and cannot block plugin loading.

Physical in-game validation remains the final visual and interaction gate.
