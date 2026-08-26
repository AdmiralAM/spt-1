# Contributing to the SPT Mod Suite

Before starting any development, diagnostic, CI, documentation, or maintenance work in this repository, read:

1. [`docs/development-workflow.md`](docs/development-workflow.md)
2. [`docs/github-stable-runtime.md`](docs/github-stable-runtime.md)
3. [`docs/branch-hygiene.md`](docs/branch-hygiene.md)
4. the README and durable docs for the affected module.

These documents define the repository operating model and are mandatory for every workstream.

## Required working model

The default lifecycle is:

`Issue → dedicated short-lived branch → commits/pushes to that branch → Pull Request → module-specific CI → runtime/user validation when required → deliberate merge → delete temporary branch → cleanup/update docs`

### Product identity is a repository contract

Every maintained module has one official product name and authoritative version metadata. Before changing either, establish the current README and machine-readable project/package authority.

Current product name/version must agree across the root module index, module README, affected project/package metadata, maintained runtime/publication manifest, workflow/package display naming, and durable current-state documentation. Runtime README/display identity follows the official product name.

Established technical identifiers such as directory names, GUIDs, namespaces, endpoints, binary names, upstream names, and runtime branch names may remain when changing them would create compatibility or migration risk. Their compatibility/provenance role must be documented explicitly; they must not be presented as the current product name.

Do not invent version bumps for documentation cleanup. A rename/version PR must update all affected identity surfaces in the same coherent integration slice or document the exact compatibility exception.

### `main` is an integration point, not a workspace

Normal development, diagnostics, archaeology, experiments, temporary validation, and progress preservation belong on the workstream branch. Do not push intermediate work to `main` merely to save it, expose it to CI, or make it visible to another process.

Merge into `main` only when there is a concrete integration need and the work has reached the appropriate acceptance/validation state. Likewise, do not repeatedly pull/rebase/merge `main` into a work branch without a reason. Synchronize when integration, conflict resolution, dependency uptake, or final validation actually requires it.

Direct writes to `main` are exceptional repository/bootstrap/recovery operations, not the normal development path.

### Use GitHub's native lifecycle tools

Prefer the GitHub mechanism designed for the job instead of storing workflow state in source files or permanent branches:

- **Issues** — meaningful bugs, features, research targets, validation gaps, and maintenance backlogs;
- **short-lived branches** — isolated implementation/diagnostic work and progress preservation;
- **Pull Requests** — review/integration gate and durable change record;
- **Actions/checks** — automated module-specific validation;
- **Actions artifacts** — transient compiled/test outputs;
- **runtime branches** — deliberate install-only publication channels;
- **labels/milestones** — backlog/release organization when they add useful structure;
- **comments/checklists** — task evidence and review state that does not belong in source files.

Do not create trigger/evidence files, generated-status commits, archive branches, or custom source-tree state when a native GitHub primitive already represents that state cleanly.

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

Stable modules may keep a low-priority polish/maintenance Issue rather than accumulating loose branches and scattered TODO notes.

Do not create an Issue for every trivial edit. Small cleanup belongs in the relevant PR.

## Branches and Pull Requests

Use one coherent short-lived branch per task. Prefer `feature/`, `fix/`, `diagnostic/`, `perf/`, or `chore/` prefixes.

Push freely to the work branch when progress needs to be preserved, shared, or validated. That is what the branch is for.

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

Runtime branches are install-only generated channels. They are not development branches. Their maintained manifests and human-readable runtime identity must use the official product name; compatibility branch/binary identifiers may remain only under the documented identity rules above.

## Clean-as-you-go

Completing a task includes removing material that became obsolete because of the task: temporary diagnostics, trigger files, generated evidence, dead branches, duplicate package copies, and superseded current-state documentation.

Do not leave cleanup for a future repository-wide sweep when it can be safely completed with the work that created the obsolete material.

For branch cleanup, classify every candidate before deletion:

- `delete-now` — no unique useful work remains and the branch is merged, expired, superseded, or deliberately discarded;
- `retain-active` — active work, review, or required runtime validation still depends on it;
- `retain-evidence` — unique useful evidence/recovery state remains and the retention reason is documented;
- `manual-action` — deletion/cleanup is justified but the available interface cannot perform it safely.

When classification is ambiguous, retain and investigate. One-time cleanup queues/evidence belong in Issues, PR comments, or explicit manual reports rather than durable source files.

The completion rule is:

`implement → validate → integrate when needed → remove superseded material → update Issue/docs → delete temporary branch`

## Priority rule

When work competes for repository or CI attention:

1. active module development and runtime validation;
2. module-specific PR CI;
3. deliberate publication;
4. repository housekeeping and cosmetic polish.

Housekeeping must yield to active development, never the reverse.
