# SPT development workflow

## Purpose

All SPT modules follow one lifecycle so active work remains isolated, reviewable, parallel-safe, and cheap to clean up.

Default flow:

`Issue → short-lived work branch → branch commits/pushes → Pull Request → module-specific CI → runtime validation when required → deliberate merge → cleanup → delete branch`

`main` is the repository integration point. It is not a workspace, progress store, diagnostic scratchpad, or CI trigger mechanism.

## Start-of-work checklist

Before changing code or repository infrastructure:

1. read `AGENTS.md`, `CONTRIBUTING.md`, this document, and the affected module README/docs;
2. inspect existing Issues/PRs/branches so the work does not duplicate or collide with an active workstream;
3. create or reuse an Issue when the work is meaningful enough to need durable scope/acceptance criteria;
4. create a dedicated short-lived branch;
5. identify the module-specific validation path before changing shared CI.

## Module identity and versioning

Every maintained module has one explicit **official product name** and one authoritative semantic version (`MAJOR.MINOR.PATCH`) for each independently shipped component.

- The module README must state the official product name and current version near the top.
- Project/package metadata (`<Version>`, assembly/file version, server metadata where applicable) is the authoritative machine-readable version and must agree with the README.
- Client/server components that are one release unit use the same module version. If components intentionally have different version lines, as with Tactical HUD client/server, the README and root module index must state both explicitly.
- A leading `v` is presentation syntax for release/tag labels (`v1.0.0`); it is not part of the semantic version stored in project/package metadata.
- Product names do not contain a version unless a compatibility-constrained artifact filename deliberately does so. If an assembly/artifact name embeds a version, that embedded value must match project metadata in the same commit.
- Directory names, GUIDs, namespaces, endpoints, runtime branch names, upstream names, and other established technical identifiers may remain unchanged when renaming them would create migration/compatibility risk. Such retained identifiers must be documented as compatibility/provenance identifiers and must not be presented as the current product name.
- Any PR that changes a product name or version must update the module README, root module index, affected manifests/build metadata, workflow/package naming, and durable current-state docs in the same integration slice.

Do not invent a new version merely to make documentation look current. Version changes represent actual release/version decisions owned by the module workstream.

## Issues

Use an Issue for a meaningful bug, feature, compatibility problem, validation gap, maintenance backlog, or research target. Record the objective, evidence/current state, scope/non-goals, acceptance or stop criteria, and links to resulting PRs/follow-ups.

Stable modules may keep one low-priority maintenance/polish Issue instead of accumulating loose branches and scattered TODO files.

Do not create Issues for trivial edits that are naturally part of an existing workstream/PR.

## Work branches

Every non-trivial implementation or diagnostic task uses its own short-lived branch. Push intermediate progress there whenever it needs preservation, sharing, CI, or review.

Recommended prefixes:

- `feature/` — new behavior;
- `fix/` — corrective work;
- `diagnostic/` — temporary evidence/instrumentation;
- `perf/` — measured performance work;
- `research/` — bounded research/prototype work that may preserve unique evidence but is still temporary;
- `chore/` — repository/CI/documentation maintenance;
- `archive/` — intentional documented historical reserve only.

Do not use ordinary branches as permanent archives. Delete them once useful work is merged or explicitly superseded and no unique material remains.

## Synchronizing with `main`

Do not merge/rebase/pull `main` into a work branch continuously without purpose. Synchronize when needed for:

- a real shared dependency;
- conflict resolution;
- final integration validation;
- required uptake of an upstream repository/CI contract.

Unnecessary synchronization creates churn and conflict risk without improving the work.

## Pull Requests

A Pull Request is the normal integration gate into `main` and the durable record of a change. It should state the linked Issue/objective, affected module, changes, non-goals, automated validation, runtime/user validation status, and post-merge cleanup.

CI attached to a PR is scoped to the affected module whenever practical. Documentation-only/repository-hygiene PRs must not trigger unrelated full-suite builds.

A PR can remain open while work/validation continues. Its existence does not imply the change is ready for `main`.

## Validation and deliberate integration

Automated tests/builds prove only what they execute. SPT/EFT runtime behavior is not considered proven when runtime evidence is required but absent.

### Milestone batches

Work is authorized and reported at milestone/gate granularity. A gate may require multiple commits and CI iterations. Agents execute those internal steps autonomously and do not require the user to repeat `continue` after each one.

The normal batch is:

`inspect current authority -> implement complete scoped slice -> validate -> diagnose/fix scoped failures -> package/update PR -> milestone handoff`

Intermediate commits and checks preserve evidence; they are not separate user handoffs. End the batch only at acceptance, a real external decision/blocker, or a required physical runtime boundary. Status updates remain non-terminal and low-noise while the batch continues.

Merge into `main` only when there is a concrete integration need and the task has reached its required acceptance/validation state. Do not merge merely to preserve progress, obtain a build artifact, expose work to another process, or clean up a branch.

After merge:

1. confirm the intended result exists in `main`;
2. close/update the linked Issue;
3. remove temporary diagnostics, trigger/evidence files, generated logs, and obsolete artifacts;
4. update current-state documentation when behavior/status changed;
5. delete the temporary branch after verifying no unique work remains.

## GitHub-native state management

Use GitHub's built-in objects rather than encoding process state in source history:

- Issues for durable work/backlog state;
- branches for isolated work/progress;
- PRs for review/integration state;
- Actions/checks for automated validation;
- Actions artifacts for transient binaries/test outputs;
- runtime branches for deliberate installable publication;
- labels/milestones for organization when useful;
- comments/checklists for evidence and review notes.

A custom source file, generated commit, or permanent branch needs a technical reason to exist when GitHub already has a native representation for that state.

## Module isolation and parallelism

Each independent mod owns its folder under `mods/`, including source, client/server components as applicable, tests, tools, and durable documentation.

Two to five module workstreams may operate concurrently. They must not require coordination unless a genuine shared dependency exists. Do not share concurrency groups across unrelated module workflows, publish another module's runtime branch, or mix unrelated module work in one PR.

## Publication

Module CI validates development. Repository publication is a different operation.

`Publish SPT Mod Suite` is a deliberate manual release/promotion controller. It must not be used as ordinary feature-branch CI or triggered simply because `main` changed.

- `main` — authoritative integrated source;
- `stable` — deliberately validated/promoted source commit;
- `runtime-*` — install-only generated module packages;
- work branches — temporary development/diagnostic space.

## Repository priorities

When work genuinely competes for repository/CI attention:

1. active module development and runtime validation;
2. module-specific PR CI;
3. deliberate publication;
4. repository housekeeping/cosmetic polish.

Housekeeping yields to active development and must not cancel, supersede, force-update, or block it.

## Clean-as-you-go

Repository hygiene is part of task completion:

`implement → validate → integrate when needed → remove superseded material → update Issue/docs → delete temporary branch`

Do not leave experimental build branches, stale trigger/evidence files, generated outputs, duplicate packages, or superseded current-state documentation for a later repository-wide cleanup when they can be safely removed with the work that made them obsolete.

See [source/stable/runtime governance](github-stable-runtime.md) and [branch hygiene](branch-hygiene.md) for retention rules.
