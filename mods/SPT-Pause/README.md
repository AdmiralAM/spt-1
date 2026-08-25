# Pause Admiral

Standalone client-only offline-raid pause for SPT 4.1.x. Current stable version: **v1.0.0**.

Press **P** during an offline raid to pause or resume. The keybind and supported options can be changed in the BepInEx F12 configuration manager.

## Behavior

- freezes the relevant world ticks so AI, player simulation, ballistics, and world-driven health/resource updates stop;
- sets Unity time scale to zero so physics and scaled gameplay stop;
- blocks local gameplay/camera input while paused and clears pending Unity input so actions entered during pause do not execute after resume;
- preserves the real raid deadline and displayed raid timer across the paused interval;
- preserves time of day by shifting the relevant game-time anchor on resume;
- can display `PAUSED` in place of the raid timer while paused;
- keeps the pause toggle available so Resume always remains possible;
- rejects hideout and network sessions;
- restores player input ownership, time scale, audio state, and adjusted clocks on resume, plugin teardown, or scene change;
- still allows Resume if `Enabled` is switched off in F12 during an active pause;
- performs reflection/object discovery on pause interaction rather than through background world scans.

Optional audio pause is available in F12 and defaults to off.

## Installation

The install layout is:

`BepInEx/plugins/Pause Admiral/Pause Admiral.dll`

The maintained install-only publication channel is [`runtime-pause`](https://github.com/AdmiralAM/spt-1/tree/runtime-pause). Copy its contents into the SPT root.

## Validation status

**Pause Admiral v1.0.0 is runtime validated.** Automated state/input/clock regression tests and the SPT 4.1.x client build passed, followed by an offline-raid runtime gate confirming:

- world, AI and raid timer stop while paused;
- no camera drift/free-camera behavior remains;
- movement/fire/reload/inventory/action inputs do not accumulate during pause;
- no queued paused actions execute after resume;
- normal controls restore after resume;
- no Pause runtime exceptions were observed.

See [runtime contract](docs/phase1-runtime-contract.md) for the compatibility mapping and [raid validation matrix](docs/phase1-raid-validation.md) for the physical acceptance sequence. Historical references to specific SPT 4.1.x patch levels describe the environments used during archaeology/validation, not a repository-wide compatibility guarantee.
