# SPT repository instructions

These instructions apply to every automated or interactive development process working in this repository.

## Read before writing

Before making changes, read:

- `CONTRIBUTING.md`
- `docs/development-workflow.md`
- `docs/github-stable-runtime.md`
- `docs/branch-hygiene.md`
- the affected module's README and relevant durable docs.

Do not begin implementation from stale assumptions when newer repository state, Issues, Pull Requests, runtime evidence, or user direction exists.

## Mandatory isolation

Treat every SPT mod/workstream as independent by default.

- Work in a dedicated short-lived branch.
- Keep changes scoped to the affected module.
- Use that module's validation workflow.
- Do not share concurrency groups with unrelated modules.
- Do not cancel, supersede, retarget, force-update, or block another active workstream.
- Do not rewrite another module's runtime branch.
- Do not invoke repository-wide publication merely to validate development.
- Do not push ordinary feature/fix work directly to `main`.

Parallel development of multiple modules is normal and must remain safe.

## Required lifecycle

`Issue → branch → implementation/diagnostics → PR → module CI → runtime validation if required → merge → delete temporary branch → remove obsolete temporary material → update current-state docs`

If the work is too small to justify its own Issue, it may be included in an existing coherent Issue/PR, but it still follows branch/PR isolation.

## CI and publication

Module CI proves the affected module. Keep triggers/path filters narrow.

GitHub Actions minutes and runner capacity are finite repository resources even when account quota remains. Every development process must use them deliberately:

- inspect code, repository state, and existing logs before triggering CI;
- prefer module/path-scoped checks over repository-wide validation;
- order validation fail-fast: cheap deterministic checks first, expensive builds/package work later;
- after a failure, diagnose the first failed boundary before triggering another run;
- rerun only the smallest necessary failed job/check when GitHub supports it;
- never rerun an unchanged full workflow merely to see whether the same failure disappears;
- do not duplicate validation already proven by another current check;
- do not spend Windows-hosted runner time on documentation-only or otherwise irrelevant changes;
- treat repeated failed-job minutes, redundant setup/download work, unnecessary full-suite runs, and broad triggers as CI hygiene defects to fix rather than normal operating cost.

A larger Actions quota is capacity for useful work, not permission to waste runner time.

`Publish SPT Mod Suite` is a manual release/publication controller. It may promote `stable` and rewrite install-only runtime channels. Treat it as a higher-level release operation, not a development check.

## Repository hygiene

Generated binaries, build/test logs, CI metadata, one-off trigger/evidence files, dependency caches, temporary diagnostics, obsolete package copies, and dead branches do not belong in the long-term source tree.

Clean up safely as part of completing the work. Never remove active working data or unique unmerged changes merely for cosmetic cleanliness.

## Priority

Active development/runtime validation > module-specific PR CI > deliberate publication > housekeeping.
