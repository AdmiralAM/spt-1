# Phase 1 runtime contract

## Archaeology result

The installed `netYnum.Pause 1.4.0` enables its world patches but fails on SPT 4.1.2 because it references the removed public type name `GameTimerClass` directly.

Two earlier public implementations established the intended behavior:

- [`epidemicz/Pause`](https://github.com/epidemicz/Pause) — `P` toggle, world tick freeze, raid/display timer preservation and hideout exclusion;
- [`kobrakon/TakeABreak`](https://github.com/kobrakon/TakeABreak) — the older world-freeze concept.

Phase 1 is an independent implementation. It does not copy or compile either legacy source. The current SPT 4.x assembly shape was mapped from the public decompilation in [`Luna-Salamanca/assemblycsharptarkovspt4`](https://github.com/Luna-Salamanca/assemblycsharptarkovspt4):

| Legacy dependency | SPT 4.x equivalent | Phase 1 strategy |
| --- | --- | --- |
| `GameTimerClass` | obfuscated sealed timer exposed as `AbstractGame.GameTimer` | Resolve the object through the semantic `GameTimer` property; no hard timer type reference |
| private timer field names | `StartDateTime` / `EscapeDateTime` semantic properties over obfuscated fields | Match backing fields by property type and value, then shift both anchors by the paused duration |
| `GameWorld.DoWorldTick` | same semantic method, `float dt` | Harmony prefix installed once at startup |
| `GameWorld.DoOtherWorldTick` | same semantic method, `float dt` | Harmony prefix installed once at startup |
| `TimerPanel.UpdateTimer` | same semantic protected method | Freeze updates and write `PAUSED` through reflected `_timerText` |
| `GameDateTime._realtimeSinceStartup` | unchanged semantic anchor | Shift by the paused duration on resume |

## Acceptance checklist

- [x] Separate plugin identity, version and package.
- [x] Offline raid guard; hideout/network rejected.
- [x] Default `P` toggle with live F12 configuration.
- [x] AI/player/world ticks frozen.
- [x] Unity physics/scaled systems frozen.
- [x] Raid deadline, visible timer and time of day preserved.
- [x] Transactional restore on duplicate input, teardown and scene change.
- [x] No `GameTimerClass` compile-time dependency.
- [x] No periodic reflection/world polling.
- [x] Automated state and clock-anchor tests.
- [ ] Physical SPT 4.1.2 raid validation.
