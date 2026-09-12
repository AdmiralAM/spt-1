# Second Life Admiral workstream

Authority for continuation lives in this file, Issue #352, the live PR body and the live PR exact head. The live implementation PR is the only development line. A future worker starts at the first milestone without acceptance evidence and continues automatically.

## Frozen product contract

- one additional appearance at most per raid;
- different safe spawn on the same map;
- minimal configurable emergency loadout;
- first corpse and original equipment remain recoverable in-world;
- no automatic full-kit restore and no item duplication;
- unsupported states fail closed to native death;
- disabled mode is behaviorally inert;
- compatibility target SPT 4.1.x; exact validation baseline SPT 4.1.5.

## Milestones

### M0 — Runtime archaeology

Prove exact local-player death, raid-finalization, profile-save, corpse ownership and local-player construction hooks from SPT 4.1.5 assemblies/source.

Acceptance: exact signatures and ordering are recorded; a rollback-safe seam is proven; unsupported Fika/network paths are identified. Guessed Harmony patches are forbidden.

### M1 — Lifecycle integration

Connect the tested state machine to the proven death boundary without profile mutation or early native settlement.

Acceptance: first death arms recovery exactly once; invalid state uses native death; terminal cleanup is deterministic.

### M2 — Corpse and inventory ownership

Detach the original body/inventory tree from the recovering player while keeping it lootable in-world exactly once.

Acceptance: no original item exists in both corpse and profile/recovered player; insurance and B&A&HB protection are not bypassed.

### M3 — Safe alternate spawn

Select a bounded valid point away from the killer, first corpse, active combat and invalid extracts, including a generic Icebreaker path.

Acceptance: deterministic seeded tests pass; no valid point means native death.

### M4 — Emergency loadout

Create only an owned minimal configurable weapon/loadout tree after spawn acceptance.

Acceptance: identity is unique, capacity/slot rules are valid, no original gear is restored and failure rolls back cleanly.

### M5 — Cross-module hardening

Validate quests, health, insurance, B&A&HB, Economy Admiral, Icebreaker, save/restart and supported solo lifecycle.

Acceptance: no duplicate rewards/events/items, no stale state and no permanent scans/polling.

### M6 — Batched physical gate

Directly deploy one exact candidate and test first death, recovery, corpse retrieval, extraction, final death and profile restart.

Acceptance: exactly one recovery; original equipment exists once; recovered inventory saves; final death remains native; logs/profile are clean.

### M7 — Stable release

Remediate failures, package and deliberately promote only the exact physically accepted head.

Acceptance: install/rollback are verified; main and runtime identity agree; merge, Issue closure and branch deletion occur only after explicit user confirmation.

## Worker execution

`M0 -> M1 -> M2 -> M3 -> M4 -> M5 -> M6 physical gate -> M7 stable`

Use one PR. Do not stop merely at CI green. Do not request user testing before exact build, direct deployment or a verified install-ready artifact. Any worker that starts local SPT processes must stop its own processes and verify cleanup before ending.
