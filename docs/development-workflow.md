# Development workflow

This document describes repository mechanics. The only execution authority is the current `origin/main:AGENTS.md` plus `origin/main:.github/workstreams.json`.

## Start and continue

1. Fetch `origin/main`.
2. Read the charter and registry from that ref.
3. Inspect the recorded phase-plan Issues, the module's single live PR and affected module documentation.
4. Resume at the first phase without acceptance evidence and continue through later recorded phases until a valid stop boundary.

Do not use policy copied into a long-lived feature branch. Reading current control state does not require merging `main` into implementation work.

## Branches and pull requests

- Use one short-lived implementation branch and at most one active implementation PR per module.
- Target `main` unless a real code dependency requires a temporary stack.
- Do not open empty, planning-only, checkpoint, successor, or artifact-only PRs.
- Keep implementation/completion evidence in module Issues/PRs; keep only durable product authorization in the registry.
- Control-plane changes use one `governance/*` branch and are owned by `GitHub Work SPT`.

## Integration lifecycle

`Issue -> branch -> implementation -> module CI -> PR -> runtime gate if required -> merge -> verify main -> update/close Issue -> delete temporary branch`

CI, a commit, merge, or packaged artifact is evidence, not a reason to stop. A completed recorded phase automatically advances to the next phase-plan entry without a registry edit or new instruction.

After merge, remove obsolete temporary branches and close superseded PRs. Preserve historical PRs and commits; do not revive an old stack as an active roadmap.

## Runtime and publication

Use `docs/runtime-artifact-gate.md` for exact physical-test handoff fields. Runtime publication channels and promotion mechanics are described by `docs/github-stable-runtime.md`; branch cleanup mechanics are described by `docs/branch-hygiene.md`.

Those documents support the control plane and cannot override it.
