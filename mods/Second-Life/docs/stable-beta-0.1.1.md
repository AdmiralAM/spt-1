# Second Life Admiral 0.1.1 — Stable Beta

Second Life Admiral now has a published Stable Beta for ordinary solo play on SPT 4.1.x, built and load-validated against SPT 4.1.6. This is a prerelease rather than the final stable channel.

## Included

- one paid recovery after the first death, at most once per raid;
- a bounded alternate spawn on the same map;
- the original corpse and non-protected equipment remain recoverable in-world;
- one owned stash pistol, its installed magazine and one compatible spare magazine, including bounded nested-container search;
- unarmed recovery when no complete owned set exists;
- native Therapist-price calculation and server-authoritative stash-ruble reservation;
- secure container, special slots, armband, B&A&HB belt/headband ownership preservation;
- camera, input, culling and Dynamic Maps player-marker handoff;
- optional LootNet provisional-death reconciliation;
- separate first-life and second-life kill/experience report;
- fail-closed native death fallback for unsupported or failed recovery states.

## Install

Exit SPT, extract the archive into the SPT installation root and allow these files to be replaced:

```text
BepInEx/plugins/Second Life Admiral/Second Life Admiral.dll
BepInEx/plugins/Second Life Admiral/SecondLife.Core.dll
SPT_Runtime/user/mods/Second Life Admiral/Second Life Admiral Server.dll
```

Existing configuration and profiles are not included in the archive and are not replaced. Older configurations that explicitly contain `Enabled = false` remain disabled until changed by the player.

## Stable Beta limits and future work

- complete the SPT 4.1.6 extraction and second-death edge-case matrix;
- recheck quick slots, protected slots, insurance, FIR state and profile restart across representative mod combinations;
- tune paid-healing and safe-spawn balance;
- polish the two-life report and recovery presentation;
- add the post-stable pre-raid emergency pistol preset;
- investigate native-styled death and Therapist screens;
- defer merged native EFT statistics until its own lifecycle-safe design is proven;
- keep Fika/network recovery disabled until separately proven.

The Stable Beta preserves the existing persistent item IDs, single-owner inventory transfers, one-recovery limit and duplicate-event/reward protections.
