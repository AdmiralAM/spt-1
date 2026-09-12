# Second Life Admiral

Second Life Admiral is a bounded one-time same-raid recovery workstream for SPT 4.1.x.

## Product contract

- at most one recovery after the first player death;
- recovery uses a different safe spawn;
- only a minimal configurable emergency loadout is granted;
- the original corpse and equipment remain in the raid;
- original gear is never cloned or restored automatically;
- unsupported states fail closed to native raid death;
- disabled mode is behaviorally inert.

## Current foundation

This branch intentionally begins with a pure deterministic lifecycle core. It does not yet patch EFT death or spawn methods and is not an installable gameplay candidate. The first implementation task is M0 runtime archaeology: prove exact SPT 4.1.5 hooks before connecting this state machine to the client.

Authority: Issue #352 and `origin/main:.github/workstreams.json` after the governance registration is integrated.

## Resume order

1. Prove death, raid-end, profile-save and local-player-spawn hooks.
2. Connect the state machine without profile mutation.
3. Add safe alternate-spawn selection.
4. Add minimal emergency-loadout ownership.
5. Validate cross-module semantics.
6. Produce one batched physical runtime candidate.

Do not create a second PR for this workstream. Continue from the live PR exact head.
