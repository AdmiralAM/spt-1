# Phase 15 — requirement marker UX and F12 controls

Phase 15 aligns the Item Intelligence presentation with the approved Discussion UX contract.

## Marker contract

- every visible marker uses the same programmatically drawn `ⓘ` glyph;
- color represents unmet requirement priority only: Quest Now → Hideout → Quest Later → Default;
- Value, Safe to Sell, surplus, generic Keep and fulfilled requirements never affect marker color;
- diagnostics retain the Default marker color;
- markers remain attached to their registered item cards and the tooltip opens only over the marker of the currently hovered item.

## Tooltip modes

- `Minimal` — Value, Keep ×N;
- `Normal` — Value, Quest Now, Hideout, Quest Later and Keep ×N with owned/required progress;
- `Detailed` — Normal plus per-slot value and Owned ×N;
- `Full` — Detailed with every concrete target; internal ids, Safe to Sell and surplus are never user-facing.

Empty facts are omitted. When no visible value or requirement fact exists, the tooltip reports `No active requirements`. Transport diagnostics ignore the selected mode and remain explicit.

## F12 configuration

The BepInEx configuration surface exposes:

- tooltip mode;
- marker size and opacity;
- marker X/Y offsets;
- native color selectors for Default, Quest Now, Hideout and Quest Later.

Setting-change events invalidate the cached marker visuals once, so F12 changes apply without restarting or adding an update loop.

## Performance boundary

- no polling, item scans or global per-frame marker loop; each lifecycle-managed cell owns one lightweight child marker;
- immutable marker classifications are reused;
- formatted tooltip strings remain cached with the immutable presentation snapshot;
- F12 changes trigger one bounded refresh of registered marker visuals.
