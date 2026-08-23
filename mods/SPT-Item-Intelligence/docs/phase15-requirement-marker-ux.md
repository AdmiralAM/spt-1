# Phase 15 — requirement marker UX and F12 controls

Phase 15 aligns the Item Intelligence presentation with the approved Discussion UX contract.

## Marker contract

- every visible marker uses the same programmatically drawn `ⓘ` glyph;
- color represents requirement priority only: Quest Now → Quest Later → Hideout → generic Keep → Neutral;
- Value, Safe to Sell and surplus never affect marker color;
- Loading and Unavailable retain diagnostic colors;
- markers remain attached to their registered item cards and the tooltip opens only over the marker of the currently hovered item.

## Tooltip modes

- `Minimal` — Value, Keep ×N;
- `Normal` — Value, Quest Now, Quest Later, Hideout, Keep ×N;
- `Detailed` — Normal plus per-slot value and Owned ×N;
- `Full` — Detailed plus the legacy decision/surplus line and template id.

Empty facts are omitted. When no visible value or requirement fact exists, the tooltip reports `No active requirements`. Transport diagnostics ignore the selected mode and remain explicit.

## F12 configuration

The BepInEx configuration surface exposes:

- tooltip mode;
- marker size and opacity;
- marker X/Y offsets;
- separate `#RRGGBB` colors for Neutral, Quest Now, Quest Later, Hideout, Keep, Loading and Unavailable.

Settings are read directly during the existing marker render path, so F12 changes apply without restarting or adding an update loop.

## Performance boundary

- no polling, item scans or per-item GameObjects;
- immutable marker classifications are reused;
- formatted tooltip strings remain cached with the immutable presentation snapshot;
- F12 reads are constant-time and restricted to the marker draw path.
