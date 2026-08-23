# Phase 16 — persistent per-item markers

Phase 16 corrects the remaining mismatch between the Phase 15 preview and the approved Discussion UX.

## Runtime contract

- every live supported EFT `ItemView`/`ItemCell` receives one persistent child UI `ⓘ` marker;
- supported `Init`/`Show`/`SetItem` lifecycle methods register or refresh a pooled view and `Kill`/`OnDisable`/`Close` unregister it;
- pointer exit clears only hover detail state and never removes the marker;
- hovering the item body does nothing; details appear only while the pointer is inside the marker rectangle;
- the default marker position is the item cell's upper-left corner, with live F12 X/Y offsets;
- pooled, killed or destroyed views cannot leave stale markers behind.

## Performance boundary

- no inventory hierarchy scan and no `Update()` polling;
- one lightweight text/outline child is created at lifecycle registration; there is no rectangular background or bitmap asset;
- template ids are resolved only at `ItemView`/`ItemCell` lifecycle and hover events, including nested item contexts;
- presentation text is cached and reprojected only when the immutable data snapshot changes;
- marker rendering is handled by the cell UI; the repaint path checks only the currently hovered cell's marker rectangle before drawing details.

Physical SPT 4.1.2 validation remains required for final placement, supported ItemView coverage and interaction feel.
