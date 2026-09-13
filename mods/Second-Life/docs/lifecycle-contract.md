# Lifecycle integration contract

## Current proof

The pure state machine establishes one-use and terminal-state semantics without touching EFT or profile data. It is deliberately insufficient to claim runtime support.

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

## Process cleanup

Any local server/client/helper started for archaeology or smoke testing must be stopped by the same worker. Record PIDs before launch, prefer graceful shutdown, and verify no owned process or listening port remains.
