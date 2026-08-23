# SPT Pause

Standalone client-only raid pause for **SPT 4.1.x**. Current version: **0.1.0 (Phase 1)**.

Press **P** during an offline raid to pause or resume. The key and options can be changed live in the BepInEx F12 configuration manager.

## Phase 1 contract

- freezes `GameWorld.DoWorldTick` and `DoOtherWorldTick`, stopping AI, player simulation, ballistics and health/resource world updates;
- sets Unity time scale to zero so physics and scaled gameplay stop;
- preserves the real raid deadline and the displayed raid timer by shifting their clock anchors on resume;
- preserves time of day by shifting `GameDateTime._realtimeSinceStartup` on resume;
- shows `PAUSED` on the raid timer while paused;
- keeps UI/inventory interaction available;
- never activates in hideout or network sessions;
- restores time scale, audio state and clocks on resume, plugin teardown or scene change;
- performs reflection/object discovery only when the user presses the pause key; there is no background world scan.

Optional audio pause is available in F12 and defaults to off, matching the legacy behavior.

## Installation

Use the install-only [`runtime-pause`](https://github.com/AdmiralAM/spt-1/tree/runtime-pause) branch or its [ZIP download](https://github.com/AdmiralAM/spt-1/archive/refs/heads/runtime-pause.zip). Copy the branch contents into the SPT root.

## Validation status

The pure state/clock transaction tests and the SPT 4.1.x client build run in CI. Physical in-raid validation remains the final milestone and does not block software work.

See [Phase 1 runtime contract](docs/phase1-runtime-contract.md) for the compatibility mapping and acceptance checklist.
