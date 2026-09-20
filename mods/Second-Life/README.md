# Second Life Admiral

Second Life Admiral `0.1.1` is a Stable Beta bounded one-time same-raid recovery mod for SPT 4.1.x. The current build and load baseline is SPT 4.1.6; the original lifecycle archaeology baseline remains SPT 4.1.5.

Stable Beta means the implemented recovery path is published for ordinary solo play while remaining edge-case physical checks, balance tuning and interface polish continue. It is not the final stable release.

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
- the original corpse and its non-protected equipment remain in the raid;
- secure container, the three special slots, armband, B&A&HB belt and headband keep their native
  protected ownership; other original gear is never cloned or restored;
- unsupported states fail closed to native raid death;
- disabled mode is behaviorally inert.

## Current implementation

The client patches the proven corpse/finalization boundary and contains the
first guarded solo recovery executor. It captures the corpse-owned equipment
root and constructs a distinct recovery object using the profile's persistent
equipment identity, reuses the native
local-game player and owner factories, replaces the dead player registration,
and resumes native death if preflight or reconstruction fails. The executor is
enabled by default for new Stable Beta installations and remains fail-closed
when a supported runtime contract is unavailable. The
server reserves a bounded random complete pistol set from the authoritative
stash, including nested containers. The client reconstructs the exact pistol
tree, installed magazine and one same-template spare magazine with their
persistent IDs and item state, then commits only after native inventory
transactions accept both roots. A newly created intrinsic pockets item gives
the spare magazine a legal recovery address without reusing the corpse-owned
pockets instance. The paid-healing gate mirrors EFT's native
HP price, Therapist loyalty coefficient, free-heal trial and Charisma discount.
The local SPT server reserves rubles from the authoritative stash, including
nested wallets, then commits after recovery or refunds on decline and failure.
The camera handoff retires the old PerfectCulling sampler, resets the retired
FPS-camera entries, registers the replacement camera and awaits its new sampler. When
Dynamic Maps is installed, its main-player marker is refreshed after handoff.
When LootNet is installed, a successfully completed recovery clears only its
provisional first-death result; LootNet's accumulated loot and kill counters
remain intact. The compatibility path is inert when LootNet is absent and
fails safely if a future LootNet version changes that optional contract.
After a recovered raid reaches its final death or extraction boundary, Second
Life shows a read-only two-section report for the first and second lives. It
separates kill details and live session experience without changing EFT's
native counters, profile settlement or ordinary post-raid screens.

The exact pre-runtime settlement and neighboring-module audit is recorded in
`docs/compatibility-spt-4.1.5.md`.

## Development installation

Install the matching client and server outputs:

```text
BepInEx/plugins/Second Life Admiral/Second Life Admiral.dll
BepInEx/plugins/Second Life Admiral/SecondLife.Core.dll
SPT_Runtime/user/mods/Second Life Admiral/Second Life Admiral Server.dll
```

All files must come from the same exact source commit. A load warning that
`Second Life Admiral.dll` references a missing `SecondLife.Core` means the
installation is incomplete and the recovery module is inert.

Existing `BepInEx/config/com.admiralam.secondlife.cfg` settings are preserved on upgrade. If an older installation explicitly saved `Enabled = false`, enable it manually to use recovery.

## Stable Beta boundary

The Stable Beta includes one paid recovery, safe alternate spawning, retained corpse ownership, nested-stash pistol selection, protected-slot handling, Dynamic Maps and optional LootNet compatibility, plus the separate first-life/second-life report.

Remaining beta work covers the complete extraction/second-death matrix on SPT 4.1.6, quick-slot and protected-slot regression checks, insurance/FIR/profile restart auditing, balance of healing/spawn distances and presentation polish. Fika remains unsupported. The pre-raid emergency preset, native-styled death/healing sequence and merged two-life EFT result are future extensions.

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
