# Second Life compatibility boundary — SPT 4.1.5

This is source and exact-assembly evidence for the pre-runtime compatibility
gate. It does not replace the batched physical acceptance session.

## Terminal settlement

The first `InitiateGameStopping()` call is suppressed before `Stop`,
`GameEnd`, `Player.OnGameSessionEnd` and `IEftSession.LocalRaidEnded`. The dead
player is removed from `GameWorld` and replaced in the local game's player
dictionary. Native `BaseLocalGame.Spawn()` then subscribes the replacement
health controller to the ordinary terminal-death handler. Consequently the
later extraction or second death reaches the single native `GameEnd` path with
the replacement profile inventory and health state; the first death cannot
submit a second quest/profile/insurance settlement.

The recovery does not replay `OnPlayerDead`, quest events, raid-start events or
backend reward endpoints. It constructs only the replacement local player,
owner, camera and native `Spawn()` binding. Source review found no Second Life
patch on quest or reward methods.

## Health and payment

The offer uses EFT's `ItemUiContext.ShowMessageWindow`. Its price mirrors the
installed `HealthTreatmentServiceView` and `HealthFactorObserver` IL:

- missing real-body-part HP multiplied by `HealthPointPrice`;
- Therapist loyalty `HealPriceCoef`, clamped to `0.05..10` after division by
  100;
- native fast-heal trial exemption;
- the same Charisma healing discount and final `Math.Ceiling` behavior.

Ruble stacks are selected deterministically from the existing stash tree.
Counts and health are changed only inside recovery execution; failure before
commit restores every original count. Fully consumed stacks use the native
`ItemManipulator.Remove` path after the replacement is attached. The later
native raid settlement serializes the resulting inventory and restored health
once. Decline, insufficient funds or any missing runtime contract resumes the
ordinary death path without profile mutation.

## Module boundaries

| Module/path | Source-resolvable result |
| --- | --- |
| B&A&HB (`mods/SPT-Belt-Armband-Inventory`) | No client patch shares `CreateCorpse` or `InitiateGameStopping`. Its pseudo-slots derive from the same equipment template used for the new empty root. No protected item is copied into recovery; the original tree stays corpse-owned and the server remains the authority for kept/lost/insurance policy. Physical insurance/protection settlement remains in the batched gate. |
| Economy Admiral (`mods/Economy-Admiral`) | Server template/economy enforcement only; it does not own player death, healing or local inventory reconstruction. Second Life uses the native Therapist price and creates no second economy engine. |
| Admiral Trader (`mods/Admiral-Trader`) | Server trader/quest content only. Second Life calls no accept/complete/reward endpoint, so it cannot duplicate authored rewards by itself. |
| Item Intelligence / Item Valuation | Presentation and prepared item-data consumers only; persistent item IDs are retained for the moved pistol tree and spare magazine. |
| Pause Admiral | Patches bounded world/timer updates, not death, player construction or settlement. A configured recovery delay is real time and introduces no polling. |
| Tactical HUD | UI/world observation only. Disposal of the old player and native creation/binding of the new player provide normal lifecycle events; visual rebinding remains physical evidence. |
| Icebreaker/common map mods | Selection consumes the current map's native spawn collection and side/category masks. No map ID, privileged point or copied spawn table exists. |
| Fika/network | Explicitly unsupported: activation requires the exact solo `EFT.LocalGame` type. Any other game type fails closed before mutation. |

## Bounded runtime

- spawn enumeration: at most 512 points;
- stash armament traversal: at most 4096 items;
- no recurring scan, scene-wide polling or per-frame reflection;
- configurable offer delay is a single bounded `Task.Delay` (`0..60s`);
- corpse and live-player distances are bounded F12 values (`0..500m`);
- duplicate death/finalization callbacks remain state-machine suppressed.

Custom spawn systems may legitimately leave `Player.SpawnPoint` unset. This is
not a failure: the selector falls back to the full side/category mask while
still enforcing distance from the corpse, last aggressor and every other live
player. The absent original point cannot be selected by identity, but the
corpse-distance rule guarantees a different recovery location.

## Remaining physical evidence

Camera/input/HUD rebinding, corpse retrieval, protected-item/insurance outcome,
quest progress, extraction, second death, profile restart and zero-stack
cleanup require the one direct-deployment runtime session recorded by Issue
#352. They are not claimed by source inspection or green CI.
