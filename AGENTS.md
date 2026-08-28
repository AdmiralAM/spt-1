# SPT automated work charter

This is the single repository-wide execution policy for automated workers.

## Canonical authority

Before every work session, fetch `origin/main` and read these exact files from that ref:

1. `origin/main:AGENTS.md` — immutable worker policy;
2. `origin/main:.github/workstreams.json` — current workstream state;
3. the technical Issues recorded in its phase plan and the module's live GitHub PR/evidence;
4. the affected module README and relevant technical docs.

Policy copied into a feature branch, old PR body, chat memory, artifact, or historical Issue is not authority. Do not merge `main` merely to read control state.

## Roles

`GitHub Work SPT` is the sole controller. It owns product goals, recorded phase plans, frozen identity/version/IDs, stable acceptance, runtime-test activation, blocked/parked state, and release decisions.

Module workers (Belt, Trader, Economy, HUD, and future module chats) are executors. They may inspect, implement, test, repair CI, package, and attach evidence. They must not:

- edit this charter or `.github/workstreams.json`;
- rewrite their recorded phase plan or stable acceptance;
- create a new product scope or mark an unfinished product `parked`;
- change frozen names, versions, persistent IDs, routes, or cross-module ownership;
- treat a branch, commit, PR, CI run, document, validator, or artifact as the product finish line;
- ask for `next step` when the registry already records the continuation;
- perform repository governance or modify another module.

Control files may change only through a `governance/*` PR owned by `GitHub Work SPT`. CODEOWNERS and the control guard enforce this boundary.

## Worker execution loop

The complete ordered `phasePlan` is pre-authorized. At the start of every run, inspect `main`, the recorded Issues, and the module's live PR evidence; resume at the first phase whose acceptance is not already proven. Continue within every available run:

`ACTIVE -> IMPLEMENT -> VALIDATE -> FIX UNTIL GREEN -> CONTINUE NEXT RECORDED PACKAGE -> RELEASE CANDIDATE -> ONE BATCHED RUNTIME TEST -> FAIL: REMEDIATE / PASS: STABLE RELEASE`

- Complete each phase's technical work without requiring user or controller approval between internal steps.
- Fix scoped CI failures and continue; do not end a run merely because CI started or passed.
- When a phase completes, record evidence in its technical Issue/PR and immediately continue to the next `phasePlan` entry.
- Ordinary recorded phase transitions never require a registry edit, controller acknowledgement, or a new user message.
- Create at most one implementation PR for the module, and only when coherent implementation exists.
- Discover the module's single live implementation PR from GitHub; PR numbers and temporary branches are deliberately not stored as control pointers.
- New branch/PR mechanics for an already recorded phase are not a new product decision.
- Do not expand beyond the registry and linked Issue/PR.

## Registry update boundaries

Do not update the registry merely because a phase, commit, CI run, PR, merge, artifact, or recorded successor completed. The evidence itself determines the resume point.

A controller-owned registry update is required only to add/remove/reorder product scope, change a phase contract or frozen identity, activate a user runtime gate, record a real blocked/parked state, or make a stable/publication decision.

## Valid stop conditions

A worker may stop only at the exact boundary of:

1. a coherent physical SPT/EFT runtime gate that cannot be resolved from source, references, logs, artifacts, or automated validation;
2. missing permission/access or a proven external dependency after all unblocked work is exhausted;
3. an explicit product decision absent from the registered roadmap;
4. completed stable/release acceptance.

PR creation, branch synchronization, commits, documentation, CI, packaging, and an internal artifact are never stop conditions.

## Runtime-test budget and handoff

- The repository normally permits one active user runtime gate across all workstreams.
- A queued gate does not stop implementation. Only the controller may activate it, normally at the combined release-candidate boundary.
- Batch related checks into one release-candidate session; do not use the user as a per-patch debugger.
- Ask only after all feasible source inspection, automated tests, builds, packaging, and CI repair are complete.
- Provide the exact GitHub Actions/release URL, PR, branch, commit SHA, artifact name/ID, digest, install layout, and a short numbered table of action / PASS / FAIL / minimal evidence.
- Chat attachments, local files, source ZIPs, CI success without an artifact, and vague `test everything` requests are invalid handoffs.
- On FAIL, consume the evidence and resume remediation automatically. On PASS, follow the next recorded phase or release transition.

Detailed handoff mechanics live in `docs/runtime-artifact-gate.md`; that document cannot override this charter or the registry.

## Safety and isolation

- Work in the registered module branch/PR and change only that module plus narrowly required shared infrastructure.
- `main` is integration-only; `runtime-*` and `stable` are deliberate publication channels, not development workspaces.
- Never weaken tests, force-update another workstream, share unrelated concurrency groups, or invoke suite publication for ordinary validation.
- Persistent profile identities require an immutable manifest covering current and retired distributed IDs, backup-first ownership-scoped recovery, and deterministic regression coverage.
- Never rename, reuse, or silently drop a distributed persistent ID.
- A profile-load/save incident freezes feature expansion for that module until recovery and prevention are proven.
- Performance-sensitive code must avoid permanent polling, scene-wide scans, hot-path reflection/allocations, and global UI mutation unless explicitly proven necessary and bounded.

## Communication

Use only:

1. one short start acknowledgement;
2. a material root cause or plan-changing CI failure;
3. a genuine blocker/runtime gate;
4. one coherent package/RC/stable result.

Do not narrate file edits, branch creation, commits, CI polling, documentation, or every internal gate. Intermediate updates are non-terminal and require no user response.

## Repository lifecycle

`Issue -> short-lived branch -> implementation -> module CI -> PR -> runtime gate when required -> merge -> verify main -> close/update Issue -> delete temporary branch`

Detailed repository mechanics live in `CONTRIBUTING.md`, `docs/development-workflow.md`, `docs/github-stable-runtime.md`, and `docs/branch-hygiene.md`. If any text conflicts, this charter and `.github/workstreams.json` win.
