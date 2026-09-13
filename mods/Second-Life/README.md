# Second Life Admiral

Second Life Admiral is a bounded one-time same-raid recovery workstream for SPT 4.1.x.

## Product contract

- at most one recovery after the first player death;
- recovery uses a different safe spawn;
- emergency armament moves one randomly selected owned pistol, its installed
  magazine and exactly one compatible spare magazine from the stash;
- if the stash has no complete eligible set, recovery starts unarmed;
- before recovery, EFT's native confirmation window offers full healing at the
  ordinary Therapist price paid from owned stash rubles; declining or lacking
  funds ends the raid through the native death path;
- the pistol pool remains configurable; random selection is bounded and can be
  seeded for deterministic validation;
- the original corpse and equipment remain in the raid;
- original gear is never cloned or restored automatically;
- unsupported states fail closed to native raid death;
- disabled mode is behaviorally inert.

## Current implementation

The client patches the proven corpse/finalization boundary and contains the
first guarded solo recovery executor. It captures the corpse-owned equipment
root, prepares a distinct empty equipment/inventory root, reuses the native
local-game player and owner factories, replaces the dead player registration,
and resumes native death if preflight or reconstruction fails. The executor is
disabled by default and has not yet passed the physical gameplay gate. Its
runtime armament path searches a bounded direct stash-root set, moves the
selected pistol tree and one compatible spare magazine through native inventory
transactions, and rolls both roots back on failure. A newly created intrinsic
pockets item gives the spare magazine a legal recovery address without reusing
the corpse-owned pockets instance. The paid-healing gate mirrors EFT's native
HP price, Therapist loyalty coefficient, free-heal trial and Charisma discount;
its ruble debit is rolled back if recovery reconstruction fails.

The exact pre-runtime settlement and neighboring-module audit is recorded in
`docs/compatibility-spt-4.1.5.md`.

## Development installation

Copy both build outputs into one dedicated plugin directory; the client DLL is
not standalone:

```text
BepInEx/plugins/Second Life Admiral/Second Life Admiral.dll
BepInEx/plugins/Second Life Admiral/SecondLife.Core.dll
```

Both files must come from the same exact source commit. A load warning that
`Second Life Admiral.dll` references a missing `SecondLife.Core` means the
installation is incomplete and the recovery module is inert.

Authority: Issue #352 and `origin/main:.github/workstreams.json` after the governance registration is integrated.

## Resume order

1. Complete and validate the minimal recovery vertical slice.
2. Validate cross-module semantics and terminal profile settlement.
3. Produce one batched physical runtime candidate.

Do not create a second PR for this workstream. Continue from the live PR exact head.

## Post-stable follow-up

After the first stable release, add a pre-raid emergency preset selector. At
raid entry it records the persistent item IDs of an owned stash pistol, its
installed magazine and one compatible spare magazine. An empty preset requests
bounded random selection across the stash and nested stash containers. If the
chosen set is unavailable or no complete random set exists, recovery is
unarmed. This follow-up must not delay or silently expand the first stable gate.
