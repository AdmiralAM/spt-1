# SPT repository instructions

These instructions apply to every automated or interactive development process working in this repository.

## Read before writing

Before making changes, read:

- `AGENTS.md`
- `CONTRIBUTING.md`
- `docs/development-workflow.md`
- `docs/github-stable-runtime.md`
- `docs/branch-hygiene.md`
- the affected module's README and relevant durable docs.

Do not begin implementation from stale assumptions when newer repository state, Issues, Pull Requests, runtime evidence, or user direction exists.

## Source-of-truth check

Before writing code, changing CI, publishing artifacts, or telling a user which build to test, establish the current authority for the affected module:

- check `main` for integrated source state;
- check active Issues for objective, allowed scope, non-goals, stop condition, and acceptance criteria;
- check active Pull Requests for in-flight implementation, draft/merge status, head branch, head SHA, runtime gate, and validation status;
- check the relevant `runtime-*` branch only as an install/publication channel, not as development source;
- check recent Actions artifacts only when a PR/build explicitly points to the matching commit SHA.

Never assume work is merged because a PR exists, a branch exists, or a chat says a build was made. Name the PR number, branch, commit SHA, and artifact/run being used when giving runtime-test instructions.

If repo evidence conflicts with chat memory, the repository state and latest explicit user direction win. Stop and report the conflict instead of guessing.

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

`Issue -> branch -> implementation/diagnostics -> PR -> module CI -> runtime validation if required -> merge -> delete temporary branch -> remove obsolete temporary material -> update current-state docs`

If the work is too small to justify its own Issue, it may be included in an existing coherent Issue/PR, but it still follows branch/PR isolation.

A failed runtime gate does not authorize feature expansion or redesign. Diagnose the first proven failing boundary, make the smallest corrective change, rerun the minimum necessary validation, and keep unrelated work out of the PR.

## Runtime gates and test artifacts

SPT/EFT runtime behavior is proven only by the required physical/user runtime evidence for that module. CI is necessary but not a substitute when an Issue or PR defines a runtime gate.

Any PR or chat handoff that asks for user testing must provide:

- module name and affected SPT version;
- PR number, branch name, and exact commit SHA;
- successful workflow/run that produced the exact test build;
- artifact name and whether it is transient Actions output or a maintained `runtime-*` package;
- exact install layout, including `BepInEx/plugins` and/or `SPT_Runtime/user/mods` paths;
- focused test checklist;
- exact logs/screenshots/results to return, including `BepInEx/LogOutput.log` when client runtime evidence is needed;
- explicit pass/fail decision rule.

Do not ask the user to perform physical/runtime testing from source code, a PR diff, a branch name, or a CI success alone. A runtime-test request is valid only after a downloadable artifact or deliberate `runtime-*` package exists for the exact commit being tested. If the workflow passed but produced no artifact, stop and fix the packaging/handoff workflow first.

Test candidates belong in GitHub Actions artifacts while they are under review. Do not update `main`, `stable`, or a `runtime-*` publication branch with a candidate until the required validation gate has passed and the promotion is deliberate.

## Issues and Pull Requests

Issues define durable scope: objective, evidence/current state, allowed work, non-goals, stop condition, runtime checklist when needed, and acceptance criteria.

Pull Requests are integration gates and durable change records. A PR must state affected module, linked Issue/objective, changes, explicit non-goals, validation, runtime gate status, and post-merge cleanup. Keep a PR draft while its required runtime evidence is absent.

Do not use source files, generated commits, or permanent branches as substitutes for Issue/PR comments, checklists, Actions checks, or Actions artifacts.

## CI and publication

Module CI proves the affected module. Keep triggers/path filters narrow.

Green checks with annotations or warnings are not automatically clean. Review every annotation before requesting runtime testing or merging. A warned build may be used for runtime testing only after the warnings are classified as non-blocking for that exact artifact. Before merge, warnings must be fixed or explicitly documented as accepted follow-up debt with the reason they do not affect the gate being evaluated.

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
