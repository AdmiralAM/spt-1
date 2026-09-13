# Second Life Admiral

Second Life Admiral is a bounded one-time same-raid recovery workstream for SPT 4.1.x.

## Product contract

- at most one recovery after the first player death;
- recovery uses a different safe spawn;
- emergency armament moves one randomly selected owned pistol, its installed
  magazine and exactly one compatible spare magazine from the stash;
- if the stash has no complete eligible set, recovery starts unarmed;
- the pistol pool remains configurable; random selection is bounded and can be
  seeded for deterministic validation;
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
4. Add owned random-pistol emergency loadout with one compatible spare magazine.
5. Validate cross-module semantics.
6. Produce one batched physical runtime candidate.

Do not create a second PR for this workstream. Continue from the live PR exact head.

## Post-stable follow-up

After the first stable release, add a pre-raid emergency preset selector. At
raid entry it records the persistent item IDs of an owned stash pistol, its
installed magazine and one compatible spare magazine. An empty preset requests
bounded random selection across the stash and nested stash containers. If the
chosen set is unavailable or no complete random set exists, recovery is
unarmed. This follow-up must not delay or silently expand the first stable gate.
