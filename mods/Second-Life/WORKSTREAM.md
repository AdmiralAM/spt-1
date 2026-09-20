# Second Life Admiral workstream

## Published channel

`0.1.1` is the authorized Stable Beta: the implemented solo recovery path is suitable for ordinary play and is published as a prerelease. Stable Beta does not claim final M6/M7 acceptance. Remaining edge-case physical checks, balance and polish continue in this same workstream and do not change the frozen ownership, one-life, payment or anti-duplication contracts.

Authority for continuation lives in this file, Issue #352, the live PR body and the live PR exact head. The live implementation PR is the only development line. A future worker starts at the first milestone without acceptance evidence and continues automatically.

## Frozen product contract

- one additional appearance at most per raid;
- different safe spawn on the same map;
- emergency armament moves one randomly selected owned pistol, its installed
  magazine and exactly one compatible spare magazine from the stash;
- no complete eligible stash set means an unarmed recovery;
- recovery is offered only with the ordinary Therapist health price; accepting
  atomically debits owned stash rubles and restores health, while declining or
  lacking funds continues native death;
- the eligible pistol pool is configurable, while selection is bounded and
  seedable for deterministic validation;
- first corpse and original equipment remain recoverable in-world;
- no automatic full-kit restore and no item duplication;
- unsupported states fail closed to native death;
- disabled mode is behaviorally inert;
- compatibility target SPT 4.1.x; exact validation baseline SPT 4.1.5.

## Milestones

### M0 — Runtime archaeology

Prove exact local-player death, raid-finalization, profile-save, corpse ownership and local-player construction hooks from SPT 4.1.5 assemblies/source.

Acceptance: exact signatures and ordering are recorded; a rollback-safe seam is proven; unsupported Fika/network paths are identified. Guessed Harmony patches are forbidden.

Evidence: `docs/runtime-archaeology-spt-4.1.5.md` records the installed
assembly hash/MVID, exact signatures and IL ordering. Result: guarded
solo-only vertical slice GO; Fika/network paths remain fail-closed.

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

After spawn acceptance, move one owned pistol selected at random from eligible
stash items, its existing installed magazine, and exactly one compatible spare
magazine from the stash into the recovered inventory. Preserve every item ID
and remove each root item from its former stash address before attaching it to
the recovered player. The installed magazine remains attached to the pistol
through the pistol-tree move. Do not source anything from the first corpse. If the stash
does not contain a complete eligible set, recovery proceeds unarmed.

Acceptance: seeded selection is deterministic; unseeded selection can vary;
the selected pistol and both magazines retain their persistent IDs and have
exactly one owner/address at every committed step; both magazines are
compatible; capacity/slot rules are valid; exactly one spare is moved; no
original corpse gear is restored; no complete stash set produces an unarmed
recovery; and a partial transfer rolls back to the original stash addresses or
cleanly resumes native death.

### M5 — Cross-module hardening

Validate quests, health, insurance, B&A&HB, Economy Admiral, Icebreaker, save/restart and supported solo lifecycle.

Acceptance: the native EFT confirmation window shows the native Therapist HP,
loyalty, trial and Charisma-adjusted price; accepting debits only owned stash
rubles and restores the recovered player; any reconstruction failure rolls the
debit back; declining, insufficient funds or an unavailable native contract
continues ordinary death; no duplicate rewards/events/items, stale state or
permanent scans/polling.

### M6 — Batched physical gate

Directly deploy one exact candidate and test first death, recovery, corpse retrieval, extraction, final death and profile restart.

Acceptance: exactly one recovery; original equipment exists once; recovered inventory saves; final death remains native; logs/profile are clean.

### M7 — Stable release

Remediate failures, package and deliberately promote only the exact physically accepted head.

Acceptance: install/rollback are verified; main and runtime identity agree; merge, Issue closure and branch deletion occur only after explicit user confirmation.

The Stable Beta publication is an intermediate prerelease within M7. Final stable promotion remains pending the remaining M6 edge-case matrix and explicit final-stable confirmation.

### M8 — Post-stable pre-raid emergency preset

After the first stable release, add a pre-raid selector that lets the player
assign an owned stash pistol, its installed magazine and one compatible spare
magazine as the emergency set. Capture only persistent item IDs at raid entry;
do not clone, reserve or remove the items at selection time. An empty preset
uses bounded seeded random selection across the stash root and nested stash
containers. If an explicitly selected set is missing/invalid at raid entry, or
if random selection finds no complete eligible set, recovery proceeds unarmed.

Acceptance: the selector is available before raid launch; selected IDs resolve
to the same owned items at raid entry; nested-container traversal is bounded
and cycle-safe; explicit, random and unarmed fallback paths are deterministic;
no item is duplicated, removed merely by selecting it, or sourced from the
first corpse; and the post-stable candidate passes its own focused physical
gate before publication.

## Worker execution

`M0 -> M1 -> M2 -> M3 -> M4 -> M5 -> M6 physical gate -> M7 stable -> M8 post-stable preset`

Use one PR. Do not stop merely at CI green. Do not request user testing before exact build, direct deployment or a verified install-ready artifact. Any worker that starts local SPT processes must stop its own processes and verify cleanup before ending.
