# SPT Pause

Standalone client-only raid pause for SPT 4.1.x. Current version: **0.1.1**.

Press **P** during an offline raid to pause or resume. The keybind and supported options can be changed in the BepInEx F12 configuration manager.

## Behavior

- freezes the relevant world ticks so AI, player simulation, ballistics, and world-driven health/resource updates stop;
- sets Unity time scale to zero so physics and scaled gameplay stop;
- preserves the real raid deadline and displayed raid timer across the paused interval;
- preserves time of day by shifting the relevant game-time anchor on resume;
- can display `PAUSED` in place of the raid timer while paused;
- keeps UI/inventory interaction available;
- rejects hideout and network sessions;
- restores time scale, audio state, and adjusted clocks on resume, plugin teardown, or scene change;
- still allows Resume if `Enabled` is switched off in F12 during an active pause;
- performs reflection/object discovery on pause interaction rather than through background world scans.

Optional audio pause is available in F12 and defaults to off.

## Installation

Use the install-only [`runtime-pause`](https://github.com/AdmiralAM/spt-1/tree/runtime-pause) channel. Copy its contents into the SPT root.

## Validation

Automated state/clock transaction tests and the SPT 4.1.x client build are covered by CI. Physical in-raid validation remains a separate runtime acceptance step.

See [runtime contract](docs/phase1-runtime-contract.md) for the compatibility mapping and [raid validation matrix](docs/phase1-raid-validation.md) for the physical test sequence. Historical references to a specific 4.1.x patch level in those documents describe the environment used during archaeology/validation, not a repository-wide compatibility guarantee.
