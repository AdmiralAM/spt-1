# Contributing to the SPT Mod Suite

Before starting any development, diagnostic, CI, documentation, or maintenance work in this repository, read:

1. [`docs/development-workflow.md`](docs/development-workflow.md)
2. [`docs/github-stable-runtime.md`](docs/github-stable-runtime.md)
3. [`docs/branch-hygiene.md`](docs/branch-hygiene.md)
4. the README and durable docs for the affected module.

These documents define the repository operating model and are mandatory for every workstream.

## Required working model

The default lifecycle is:

`Issue → dedicated short-lived branch → commits → Pull Request → module-specific CI → runtime/user validation when required → merge → delete temporary branch → cleanup/update docs`

### Isolation is the default

Multiple SPT modules are expected to be developed concurrently in the same repository. Each workstream must therefore remain independent unless an explicitly approved cross-module change requires otherwise.

A workstream must:

- use its own branch;
- change only its own module plus narrowly required shared infrastructure;
- use module-specific CI/validation;
- avoid shared concurrency groups with unrelated modules;
- never cancel, supersede, force-update, or block another active workstream;
- never publish or rewrite another module's runtime channel;
- avoid direct writes to `main` for normal development;
- avoid unrelated refactors, packaging changes, or repository-wide churn.

Two to five independent module workstreams may run in parallel and must be able to do so without coordination unless a real shared dependency exists.

## Issues

Create or reuse an Issue for a meaningful bug, feature, validation gap, maintenance backlog, compatibility problem, or research target. Keep the Issue current enough that another developer can understand the objective, evidence, scope, non-goals, and stop/acceptance criteria.

Do not create an Issue for every trivial edit. Small cleanup belongs in the relevant PR.

## Branches and Pull Requests

Use one coherent short-lived branch per task. Prefer `feature/`, `fix/`, `diagnostic/`, `perf/`, or `chore/` prefixes.

Open a Pull Request before normal integration into `main`. The PR is the merge gate and should record:

- linked Issue/objective;
- affected module(s);
- what changed and why;
- explicit non-goals;
- automated validation performed;
- runtime/user validation still required, if any;
- cleanup required after merge.

When the PR is complete, merge it, close/update the Issue, and delete the temporary branch after verifying that no unique work remains.

## CI and publication

Development validation is module-specific and independent. A module workflow must not use a concurrency group shared with unrelated modules.

The repository-wide **Publish SPT Mod Suite** workflow is a controlled publication operation, not ordinary development CI. It is manual-only unless the repository policy is deliberately changed. Do not invoke, modify, or depend on it merely to validate a feature branch.

Runtime branches are install-only generated channels. They are not development branches.

## Clean-as-you-go

Completing a task includes removing material that became obsolete because of the task: temporary diagnostics, trigger files, generated evidence, dead branches, duplicate package copies, and superseded current-state documentation.

Do not leave cleanup for a future repository-wide sweep when it can be safely completed with the work that created the obsolete material.

## Priority rule

When work competes for repository or CI attention:

1. active module development and runtime validation;
2. module-specific PR CI;
3. deliberate publication;
4. repository housekeeping and cosmetic polish.

Housekeeping must yield to active development, never the reverse.
