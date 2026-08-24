# Branch hygiene

This repository keeps branches only when they have a clear current role.

## Permanent branches

- `main` — active source of truth.
- `stable` — CI-green source snapshot.
- `runtime` — Tactical HUD install channel.
- `runtime-item-intelligence` — Item Intelligence install channel.
- `runtime-pause` — Pause install channel.
- `runtime-belt-armband` — Belt/Armband install channel.
- `archive/v1.13.0` — intentional frozen Tactical HUD reserve.

## Active development branches

Do not delete branches that are part of current workstreams or still carry unique changes under review. Current examples include:

- `artem-revival-spt-4.1.3`
- `belt-runtime-candidate-poc`
- current Item Intelligence R35-R38 work lines while they remain unmerged
- any newly created Quest Planner, Belt/Armband, Item Intelligence, Pause, or Artem branch that is still being actively developed or validated

## Safe-to-delete rule

A temporary branch is safe to delete when comparison against `main` shows `ahead_by=0`: the branch is entirely contained in `main` and has no unique commits left.

Confirmed safe-to-delete Belt archaeology branches at the time of this audit:

- `belt-armband-packnstrap-r10`
- `belt-armband-packnstrap-r11`
- `belt-armband-packnstrap-r12`
- `belt-armband-packnstrap-r13`
- `belt-armband-packnstrap-r14`
- `belt-armband-packnstrap-r15`
- `belt-armband-packnstrap-r16`
- `belt-armband-packnstrap-r17`
- `belt-armband-packnstrap-r18`
- `belt-armband-packnstrap-r19`
- `belt-armband-packnstrap-r20`
- `belt-armband-packnstrap-r21`
- `belt-armband-packnstrap-r22`

## Needs review before deletion

Do not delete merely because a branch is old. Branches with `ahead_by>0` still carry unique commits and require content review or explicit supersession evidence first.

Known examples:

- `belt-armband-packnstrap-r1`
- `belt-armband-packnstrap-r22-rebased`
- `belt-armband-packnstrap-r23`
- historical Item Intelligence `fix/*`, `diagnostic/*`, `work/*`, `r35/*` through `r38/*` branches that still compare as diverged
- `fix/pause-phase1-validation-hardening`
- `feature/belt-armband-phase1`

## Working policy

1. Prefer short-lived feature/fix/diagnostic branches.
2. Merge or otherwise absorb validated work into `main`.
3. Delete the temporary branch once `ahead_by=0` or once its unique work is explicitly superseded.
4. Do not use branches as indefinite archives; intentional historical reserves must use the `archive/` namespace and be documented.
5. Generated build branches are not development history. Runtime channels are rebuilt from validated source and may be force-updated by CI.
6. Never delete an active workstream branch during repository cleanup.
