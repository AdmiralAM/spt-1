# Contributing to the SPT Mod Suite

This file describes repository mechanics. Execution authority comes only from the current `origin/main:AGENTS.md` and `origin/main:.github/workstreams.json`; they win over any copied or historical text.

## Before changing a module

1. Fetch `origin/main` and read the charter and registry from that ref.
2. Read the registered active Issue/PR and the affected module README.
3. Discover the module's single live implementation PR from GitHub, or create a short-lived branch when coherent implementation is ready.
4. Identify the module-specific validation workflow and any persistent profile IDs before editing.

## Identity and persistence

- Product name and version agree across the module README, build/package metadata, runtime manifest and workflow display name.
- Established GUIDs, IDs, routes, namespaces and filenames remain frozen when the registry says so.
- A distributed persistent ID is never renamed, reused or silently omitted from recovery coverage.
- Profile-writing modules maintain an immutable current/retired ID manifest, backup-first ownership-scoped recovery and deterministic recovery tests.
- A profile incident blocks feature expansion in that module until recovery and recurrence prevention are proven.

## Isolation and branches

- `main` is integrated source, not a workbench or CI trigger.
- Use one short-lived branch and at most one active implementation PR per module.
- Keep unrelated modules, workflows and concurrency groups independent.
- Synchronize with `main` only for a real dependency, conflict resolution or final integration.
- Never force-update, retarget, cancel or publish another active workstream.
- `runtime-*` and `stable` are deliberate publication channels, never development branches.

## Pull requests and CI

- Open a PR only after coherent implementation exists; no empty, planning-only or artifact-only PRs.
- Record the registry key, linked Issue, changed behavior, frozen scope, validation and exact head SHA.
- Use the narrowest module workflow. Diagnose the first failed boundary before rerunning.
- CI warnings are fixed or explicitly classified before integration.
- CI success and an artifact are evidence, not completion or a conversational stop.
- Physical testing follows `docs/runtime-artifact-gate.md` when the worker reaches a recorded `requiresUserRuntime` phase with all prior acceptance proven. No other chat activates it.

## Integration and cleanup

After acceptance:

1. merge deliberately into the registered target;
2. verify the intended result on `main`;
3. update or close the linked Issue;
4. remove obsolete temporary diagnostics and branches;
5. update the registry through a `governance/*` PR only when faithfully implementing an explicit user instruction that changes durable product scope or contracts; runtime readiness, blockers and ordinary phase transitions use GitHub evidence and require no registry edit.

Generated binaries, logs, caches, package copies, trigger files and CI metadata do not belong in source history. Preserve unique evidence in Issues, PRs or Actions artifacts instead.

Supporting mechanics: `docs/development-workflow.md`, `docs/github-stable-runtime.md` and `docs/branch-hygiene.md`.
For Windows GitHub CLI setup and the recurring `git executable in PATH` error, see [docs/github-cli-windows.md](docs/github-cli-windows.md).
