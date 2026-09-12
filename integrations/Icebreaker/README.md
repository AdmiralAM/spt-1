# Icebreaker integration

Owned integration layer for Manimal's Icebreaker Backport and paced RUAF / Black Division events.

This repository does not redistribute upstream binaries, bundles, map assets or databases. Install the exact upstream packages, then keep only hashes, compatibility findings and owned configuration here.

## What Icebreaker provides

Icebreaker Backport is a complete location/content backport. It registers the Icebreaker location and its map access/progression, extracts, loot and crates, quests, environmental and interaction systems, Wedge encounter, sealed/keypad doors, ladders, tripwires, gas, weather/fog/snow presentation and related runtime repairs.

Black Division is a required dependency/content component, not the entire purpose of Icebreaker.

## Resume order

1. Resolve exact official packages and SHA-256 values in `upstream-lock.json`.
2. Inspect the user's active SPT paths and remove duplicate active copies only through recoverable backups.
3. Start SPT 4.1.5, capture baseline evidence, then stop the server and verify the task-owned PID/port is gone.
4. Characterize actual default RUAF/Black Division behavior.
5. Apply `rotation-normal.json` through upstream configuration where possible.
6. Create code only for controls that upstream configuration cannot express safely.

Authority: #353. ORBIT is explicitly outside this workstream.
