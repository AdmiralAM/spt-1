# Phase 16 — persistent per-item markers

Phase 16 corrects the remaining mismatch between the Phase 15 preview and the approved Discussion UX.

## Runtime contract

- every live supported EFT `ItemView` receives a persistent programmatically drawn `ⓘ` marker;
- `ItemView.Init` registers or refreshes a pooled view and `ItemView.Kill` unregisters it;
- pointer exit clears only hover detail state and never removes the marker;
- hovering the item body does nothing; details appear only while the pointer is inside the marker rectangle;
- the default marker position is the item cell's upper-right corner, with live F12 X/Y offsets;
- pooled, killed or destroyed views cannot leave stale markers behind.

## Performance boundary

- no inventory hierarchy scan and no `Update()` polling;
- no per-item `GameObject`, component or graphical asset;
- template ids are resolved only at lifecycle/hover events;
- presentation text is cached and reprojected only when the immutable data snapshot changes;
- the render path runs only for Unity repaint events, iterates only the already registered live views and skips inactive views.

Physical SPT 4.1.2 validation remains required for final placement, supported ItemView coverage and interaction feel.
