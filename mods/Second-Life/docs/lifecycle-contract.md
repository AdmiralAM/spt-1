# Lifecycle integration contract

## Current proof

The state machine and finalization gate establish one-use and terminal-state
semantics. The guarded client executor now uses the proven local-game factories
and a replacement inventory/equipment root, but remains insufficient to claim
runtime support until the complete vertical slice passes the physical gate.

Exact SPT 4.1.5 metadata/IL evidence and the guarded solo go decision are
recorded in `runtime-archaeology-spt-4.1.5.md`.

## M0 questions that must be answered from exact SPT 4.1.5 assemblies/source

1. Which local-player death callback runs before raid shutdown and inventory serialization?
2. Can native raid finalization be suspended and resumed without completing death/profile settlement?
3. Which supported factory constructs a new local player and binds camera, input, health, inventory and quest controllers?
4. How is the first corpse inventory detached so it stays in-world and is not also persisted to the profile?
5. Which spawn-point API exposes valid side/category constraints on every supported map, including Icebreaker?
6. Which callbacks settle extraction, final death, insurance and quest events exactly once?
7. Which lifecycle is safe in solo only, and what must be explicitly disabled under Fika?

## Go/no-go boundary

Do not add Harmony hooks merely because a method name appears plausible. M0 passes only when exact method signatures, ownership and ordering are evidenced and a rollback-safe seam exists. If native raid finalization cannot be safely suspended, the feature must fail closed rather than reconstruct the profile.

## Emergency armament contract

The recovered player receives no generated equipment. The module selects one
owned pistol at random from eligible stash items and moves that exact item, its
installed magazine and exactly one compatible spare stash magazine into the
recovered inventory. Persistent item IDs are preserved and each item must have
one owner/address throughout the committed transfer. Selection supports a fixed
seed for deterministic tests. If no complete set exists, recovery continues
unarmed. An invalid tree or partial move rolls back to the captured stash
addresses or resumes native death; equipment from the first corpse is never a
substitute.

The pistol and installed magazine form one ownership tree and therefore use
one physical move. The installed magazine must retain its original ID and
remain in the pistol magazine slot before and after that move. The spare
magazine is the second physical move. Treating the installed magazine as an
independent move is forbidden because it creates a transient invalid weapon
tree. The first stable selector considers only items placed directly in the
stash root; nested-case traversal belongs to the post-stable preset milestone.

The recovered equipment root receives a newly constructed intrinsic pockets
item using the native pockets template so the owned spare magazine has a legal
address. This infrastructure is not selected armament and never reuses the
corpse-owned pockets instance.

The first stable release uses automatic selection only. A post-stable pre-raid
preset may record the three selected persistent item IDs when entering the
raid. Selection itself never moves or reserves items. Resolution order is:

1. a complete explicitly selected set present in the stash tree;
2. when the preset is empty, a complete seeded-random set from the stash root
   or nested stash containers;
3. unarmed recovery when the applicable set cannot be resolved.

An invalid explicit preset does not silently choose different equipment.
Traversal of nested containers must be bounded and cycle-safe.

## Process cleanup

Any local server/client/helper started for archaeology or smoke testing must be stopped by the same worker. Record PIDs before launch, prefer graceful shutdown, and verify no owned process or listening port remains.
