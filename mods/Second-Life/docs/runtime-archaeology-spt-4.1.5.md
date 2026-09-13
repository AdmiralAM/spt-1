# SPT 4.1.5 runtime archaeology

## Exact baseline

- installation: `C:\Games\SPT`;
- SPT client assembly: `spt-singleplayer.dll`, assembly version `4.1.5.0`;
- EFT assembly: `EscapeFromTarkov_Data\Managed\Assembly-CSharp.dll`;
- EFT SHA-256: `EE25CEE1259777B38ED8B3E7841FDC2DB3C98540B1469FA539B1FF183476E436`;
- EFT module MVID: `cc2d80b0-6d5b-4cb1-a581-6d2cc901d4c7`.

Evidence below comes from metadata and IL read directly from those installed
assemblies. No game or server process was started.

## Proven death and raid-finalization order

`EFT.Player.Init(...)` subscribes virtual
`Player.OnDead(EFT.EDamageType)` to `IHealthController.DiedEvent` while the
player is being constructed. Later, `EFT.BaseLocalGame<T>.Spawn()` subscribes
its own `CG_Spawn(EFT.EDamageType)` handler to the same event. Consequently the
player death handler runs before the local-game handler on the normal local
raid construction path.

The virtual dispatch for the local protagonist is:

1. `EFT.LocalPlayer.OnDead(EFT.EDamageType)` formats the death packet, sets
   dog-tag data, then calls `EFT.Player.OnDead(EFT.EDamageType)`.
2. `EFT.Player.OnDead(...)` publishes the player-dead events, disables player
   systems, removes first-level items from the player inventory, calls
   `EFT.Player.CreateCorpse()`, applies the corpse impulse, and starts
   `EFT.Player.OnDeadCoroutine()`.
3. `EFT.BaseLocalGame<T>.CG_Spawn(EFT.EDamageType)` disables the current
   `PlayerOwner`, unsubscribes itself from `DiedEvent`, then calls
   `EFT.BaseLocalGame<T>.InitiateGameStopping()`.
4. `InitiateGameStopping()` shows the death panel and calls
   `Stop(profileId, exitStatus, exitName, delay)`.
5. `Stop(...)` changes game status, stops the raid timer/exfiltration/bot and
   environment systems, shows the black screen, then schedules `GameEnd(...)`.
6. `CG_GameEnd.MoveNext()` calls `Player.OnGameSessionEnd(...)`, disposes the
   players, stores health and visual state, builds `ProfileDescriptor` and
   `SessionResult`, obtains lost insured/transfer items, and finally awaits
   `IEftSession.LocalRaidEnded(...)`.

This proves that `LocalRaidEnded` and profile settlement have not started when
`InitiateGameStopping()` is entered after the first death. The original corpse
has already been created at that point.

## Corpse ownership

`Player.OnDead(...)` removes the player's first-level inventory items before
calling `CreateCorpse()`. Corpse construction receives the existing
`InventoryEquipment` tree. `Corpse.Init(...)` constructs a new
`CorpseItemController` over that exact equipment root and initializes the loot
item from it. This is the native single-owner transfer seam; the
recovery implementation must not clone that tree or repopulate it from the
pre-death equipment. Emergency armament must instead move existing persistent
item IDs from their stash addresses, with rollback before any partial transfer
is committed.

The recovered profile therefore requires a different, newly created empty
`InventoryEquipment` root. `EFT.ItemFactory.CreateItem(id, templateId, diff)`
and the public `InventoryEquipment(string id, InventoryEquipmentTemplate)`
constructor are available, but the replacement root must be installed before
constructing the recovered `SinglePlayerInventoryController`. Reusing the
corpse root is a hard preflight failure even when all slots appear empty.

Metadata-only inspection additionally proves that `EFT.Profile.Inventory` is
a public replaceable `Inventory` field, while `Inventory.Equipment` is a public
readonly field. The public `Inventory` constructor takes the new equipment root
first and the existing stash, quest-raid, quest-stash, sorting-table, hideout,
discard-limit and deserialization state thereafter (12 parameters total).
Recovery must therefore construct a replacement inventory atomically around
the preserved non-equipment roots; mutating the readonly equipment field or
sharing the corpse-owned root is forbidden.

## Proven recovery construction APIs

The normal local-player constructor is:

```text
EFT.LocalPlayer.Create(
  EFT.GameWorld, int, Vector3, Quaternion, string, string, EPointOfView,
  EFT.Profile, bool, EUpdateQueue, Player.EUpdateMode, Player.EUpdateMode,
  CharacterControllerSpawner.Mode, Func<float>, Func<float>,
  EFT.IStatisticsManager, EFT.ICustomizationFilter, EFT.IEftSession,
  EFT.ELocalMode, bool, bool) -> Task<EFT.LocalPlayer>
```

Its async body constructs `SinglePlayerInventoryController`,
`PlayerHealthController`, `QuestControllerClientLocalGame`, achievements,
prestige and dialog controllers, calls `Player.Init(...)`, validates magazines,
spawns empty hands and initializes local culling.

The normal owner/camera binding path is:

1. `EFT.EftGamePlayerOwner.Create(...)` / `GamePlayerOwner.smethod_2(...)`;
2. `PlayerOwner.Create(...)` binds the player and input tree;
3. owner construction binds health, inventory, UI, input, audio and hands
   events, then calls `EftGamePlayerOwner.Init()`;
4. `EFT.CameraControl.PlayerCameraController.Create(EFT.Player)` binds the
   first-person camera;
5. `BaseLocalGame<T>.Spawn()` binds the final-death handler and enables owner
   input.

`BaseLocalGame<T>` exposes the `LocalPlayer` and `Profile` setters needed by
the normal construction path. Re-entry must use the same factory/owner/camera
sequence; teleporting or reviving the dead object is rejected.

## Spawn API

`EFT.Game.Spawning.ISpawnSystem.SelectSpawnPoint(...)` returns an
`ISpawnPoint`. The interface exposes stable ID, position, rotation, side and
category masks, infiltration, bot zone, sniper-zone flag and blocked state.
The module can therefore apply a bounded second-stage safety filter for a
different point, corpse/killer/combat/extract distances and map-generic
eligibility. No Icebreaker-specific spawn table is required.

## Go decision and rollback seam

M0 is **GO for a guarded solo-only vertical slice**. A prefix on the exact
`BaseLocalGame<T>.InitiateGameStopping()` boundary can suppress native
settlement only after all recovery preflight checks succeed. Any unsupported
state or construction failure resumes that same native method behind a
re-entry guard, preserving the original death flow. No profile endpoint has
been called before this seam.

Fika/network games remain unsupported. The installed tree contains a
`LootNetFika.dll` compatibility assembly but no Fika runtime plugin; this is
not evidence of co-op safety. Runtime activation must require the exact local
game type and fail closed otherwise.

No suitable public Extra Lives source repository or verifiable source license
was found in the GitHub search performed for this milestone, so no third-party
code or assets were used.

## Remaining physical proof

Metadata and IL prove the available seam and ordering, not that a reconstructed
player survives a live frame or saves correctly. Camera/input/UI rebinding,
corpse looting, insurance, extraction and final death remain part of the
batched physical runtime gate.
