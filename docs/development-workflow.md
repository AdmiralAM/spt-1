# SPT development workflow

## Purpose

All SPT modules in this repository follow the same development lifecycle so active work remains isolated, reviewable, and easy to clean up.

The default flow is:

`Issue → short-lived work branch → commits → Pull Request → module-specific CI → review/runtime validation → merge → delete temporary branch`

## Issues

Use an Issue for a meaningful unit of work: a bug, feature, compatibility problem, validation gap, maintenance backlog, or research target that benefits from a durable description and acceptance criteria.

An Issue should state:

- the problem or objective;
- current evidence/state;
- scope and explicit non-goals where relevant;
- acceptance/stop criteria;
- links to related PRs or follow-up Issues.

Do not create Issues for trivial one-line housekeeping changes. Small cleanup belongs inside the relevant workstream/PR.

Stable modules may keep one low-priority maintenance/polish Issue instead of accumulating loose branches and scattered notes.

## Work branches

Every non-trivial implementation or diagnostic task should use its own short-lived branch. A branch owns one coherent workstream and should not mix unrelated modules.

Recommended prefixes:

- `feature/` — new behavior;
- `fix/` — bug/corrective work;
- `diagnostic/` — temporary runtime evidence work;
- `perf/` — measured performance work;
- `chore/` — repository/CI/documentation maintenance;
- `archive/` — intentional documented historical reserve only.

Do not use ordinary branches as permanent archives. Once useful work is merged or explicitly superseded, delete the branch.

## Pull Requests

A Pull Request is the merge gate into `main` and the durable review record for the change. It should explain what changed, why, what was deliberately not changed, validation performed, and any remaining runtime/user test requirement.

CI attached to a PR should be scoped to the affected module whenever practical. Documentation-only or repository-hygiene PRs should not trigger unrelated full-module builds.

## Validation and merge

Automated tests/builds prove only what they actually execute. Runtime behavior that requires SPT/EFT must not be declared successful without runtime evidence.

Merge only when the task's acceptance criteria are satisfied or the remaining limitation is explicitly documented and accepted.

After merge:

1. confirm the useful changes exist in `main`;
2. close/update the linked Issue;
3. delete the temporary branch when no unique work remains;
4. remove temporary diagnostics, trigger files, generated logs, and obsolete artifacts;
5. update current-state documentation if behavior/status changed.

## Module isolation

Each independent mod owns its own folder under `mods/`, including source, server/client components as applicable, tests, tools, and durable documentation.

Do not mix unrelated module work in one branch or PR unless there is a proven shared-infrastructure dependency that requires an atomic change.

## Repository priorities

When work overlaps, priority is:

1. active development/runtime validation;
2. module-specific CI and PR validation;
3. publication/runtime channel work;
4. repository housekeeping and cosmetic polish.

Housekeeping must never cancel, supersede, force-update, or block active development work.

## Clean-as-you-go rule

Repository hygiene is part of completing the task, not a separate future project.

For every completed workstream:

`implement → test/validate → merge → remove obsolete temporary material → update documentation/state`

Do not leave behind experimental build branches, stale trigger files, generated evidence, duplicate package copies, or superseded documentation merely because the implementation is finished.

## Source and release roles

- `main` — authoritative development source;
- `stable` — validated/promoted source commit;
- `runtime-*` — install-only generated module packages;
- temporary work branches — implementation/diagnostic space only.

See [source/stable/runtime governance](github-stable-runtime.md) and [branch hygiene](branch-hygiene.md) for repository-level retention rules.
