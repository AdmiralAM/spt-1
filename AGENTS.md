# SPT repository instructions

These instructions apply to every automated or interactive development process working in this repository.

## Read before writing

Before making changes, read:

- `AGENTS.md`
- `CONTRIBUTING.md`
- `docs/development-workflow.md`
- `docs/runtime-artifact-gate.md`
- `docs/github-stable-runtime.md`
- `docs/branch-hygiene.md`
- the affected module's README and relevant durable docs.

Do not begin implementation from stale assumptions when newer repository state, Issues, Pull Requests, runtime evidence, or user direction exists.

When using an old mod, external repository, previous implementation, old branch, or other reference as a baseline, perform a baseline viability check before implementation. Identify the reference source and version, the current target version, affected runtime/API boundaries, known incompatible dependencies, whether the reference is product inspiration, data-model source, code port, or runtime behavior proof, and the minimal proof that the baseline boundary still exists on the target version. If that foundation cannot be proven, stop and report instead of spending implementation effort porting an unvalidated reference.

## Source-of-truth check

Before writing code, changing CI, publishing artifacts, or telling a user which build to test, establish the current authority for the affected module:

- check `main` for integrated source state;
- check active Issues for objective, allowed scope, non-goals, stop condition, and acceptance criteria;
- check active Pull Requests for in-flight implementation, draft/merge status, head branch, head SHA, runtime gate, and validation status;
- check the relevant `runtime-*` branch only as an install/publication channel, not as development source;
- check recent Actions artifacts only when a PR/build explicitly points to the matching commit SHA.

Never assume work is merged because a PR exists, a branch exists, or a chat says a build was made. Name the PR number, branch, commit SHA, and artifact/run being used when giving runtime-test instructions.

If repo evidence conflicts with chat memory, the repository state and latest explicit user direction win. Stop and report the conflict instead of guessing.

## Product identity and version contract

Every maintained module has one official product name and authoritative version metadata. Treat those values as repository contracts, not cosmetic prose.

- Before changing a module name or version, identify its current README and machine-readable package/project metadata authority.
- Current product names and versions must agree across the root module index, module README, affected project/package metadata, maintained runtime/publication manifest, workflow/package display naming, and durable current-state documentation.
- Runtime README/display identity must use the official product name.
- Historical upstream names, directory names, GUIDs, namespaces, endpoints, binary names, and runtime branch names may remain only when they are required for compatibility/provenance and that retained role is documented explicitly.
- Do not silently treat a compatibility identifier as the current product name.
- Do not invent a version bump merely to normalize documentation. Version changes are release decisions owned by the affected module workstream.
- A rename/version PR must update all affected identity surfaces in the same integration slice or explicitly document a compatibility exception.

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

## Autonomous work-package execution

Active development is authorized as a work package: one bounded product objective containing an ordered sequence of gates and one final acceptance condition. A gate is an internal checkpoint inside the package, not a user conversation boundary.

The active Issue or PR must define before implementation:

- one work-package objective;
- the ordered gates included in the package;
- explicit non-goals/frozen contracts;
- the final package acceptance condition;
- the package state: `active`, `blocked`, `runtime-gate`, or `complete`.

Once the user authorizes the active package with wording such as `continue`, `work`, or `next step`, execute the whole package autonomously.

- Do not redefine, shrink, or replace the package with a smaller self-selected `next gate`.
- Do not turn discovery, policy reading, one file, one commit, one quest, one validator, one CI run, documentation reconciliation, or PR cleanup into a terminal milestone unless the user explicitly defined that exact task as the whole package.
- Complete gates in the recorded order. When one gate passes, update its state and immediately start the next recorded gate without asking for permission.
- Continue through source inspection, implementation, tests, commits, CI diagnosis, corrective commits, packaging, PR updates, merge and cleanup when those steps are included in the package.
- `One gate at a time` controls execution order and prevents scope expansion. It never means `one gate per response`.
- A terminal response is valid only when every package gate and the final acceptance condition are complete, or when a genuine blocker changes package state to `blocked`/`runtime-gate`.
- If blocked, name the exact external boundary and preserve the remaining ordered gates. Do not invent a documentation/validator gate merely to end the turn.

Progress communication must be low-noise and non-terminal. Give a start acknowledgement, then report only a proven root cause, a material multi-file implementation slice, CI failure that changes the plan, a genuine blocker, or final package completion. Do not issue `next step`, `head before/after`, `changed files`, artifact, or completion-style summaries after internal gates. When an intermediate update is necessary, state `continuing automatically; no user response required` and continue working.

## Product roadmap and package transitions

A work package is not automatically the product finish line. Every actively developed module must have a durable product roadmap in its authoritative Issue, PR, or module current-state documentation. The roadmap must define:

- the final product/release objective and explicit stable acceptance criteria;
- the ordered product phases from the current state to that objective;
- which phase and work package are active now;
- the successor package that becomes active after a PASS;
- the FAIL path that resumes diagnosis/remediation;
- dependencies or product decisions that can legitimately block automatic transition.

An artifact is a delivery vehicle, not proof that the module is finished. Every artifact must be classified as `diagnostic`, `runtime-candidate`, `release-candidate`, or `release`. Do not describe a diagnostic/runtime candidate as a final version. A CI-green candidate may complete a repository implementation gate, but it does not complete the product unless the roadmap's stable/release acceptance criteria are also satisfied.

At a package boundary:

- if no physical gate is required, close the package and automatically open/activate the recorded successor package when the roadmap already authorizes it;
- if a runtime gate is required, set the package to `runtime-gate` and request the one batched test defined by the PR;
- after a runtime PASS, record the evidence and automatically continue into the recorded successor package without asking `what next` or waiting for another authorization;
- after a runtime FAIL, consume the evidence, return the package to `active`, diagnose and fix the failed boundary, rerun automated validation, and produce the next coherent candidate without asking the user to restate the task;
- after inconclusive evidence, exhaust repository/source/log diagnosis first and request only the smallest missing external fact when genuinely necessary;
- mark the product `complete` only when the roadmap's stable/release criteria are satisfied, not merely because a package, PR, CI run, or artifact exists.

A new branch or PR required by the recorded successor phase is repository mechanics, not a new user decision. Create it automatically when the roadmap already authorizes that phase. Do not expand beyond the recorded product roadmap or invent new mechanics merely to keep working.

## Runtime gates and test artifacts

SPT/EFT runtime behavior is proven only by the required physical/user runtime evidence for that module. CI is necessary but not a substitute when an Issue or PR defines a runtime gate.

When the active user direction, Issue, PR, or handoff asks for a runtime candidate, gameplay alpha, installable package, or artifact-only continuation, the accepted unit of progress is a CI-green installable artifact for the exact commit under review. Static tests, validators, documentation, manifests, and guard scripts may support that artifact, but they are not delivery by themselves.

After an artifact-only/runtime-candidate instruction, do not stop on static-only commits. Do not add another documentation, validator, guard, manifest, or refactor commit unless the same work produces or directly fixes the installable artifact packaging path. If packaging cannot be produced, stop with the exact missing boundary instead of substituting additional static proof.

Any PR or chat handoff that asks for user testing must provide:

- module name and affected SPT version;
- PR number, branch name, and exact commit SHA;
- successful workflow/run that produced the exact test build;
- a clickable GitHub Actions run URL and a direct GitHub-hosted artifact URL for the exact artifact ID, or a deliberate maintained GitHub runtime/release URL;
- artifact name and whether it is transient Actions output or a maintained `runtime-*` package;
- artifact digest or package checksum when available;
- exact install layout, including `BepInEx/plugins` and/or `SPT_Runtime/user/mods` paths;
- focused test checklist;
- exact logs/screenshots/results to return, including `BepInEx/LogOutput.log` when client runtime evidence is needed;
- explicit pass/fail decision rule.

The GitHub URL is mandatory. Do not hand off a chat attachment, automatic chat download, ephemeral local/sandbox file, source archive, or binary pasted outside GitHub as the test candidate. If the Actions artifact expired, is missing, or cannot be downloaded from the named run, regenerate or deliberately republish it on GitHub before requesting user action.

The runtime checklist must be numbered and decision-oriented. For every item state: the exact user action, expected PASS behavior, explicit FAIL condition, and evidence to return. Do not use vague requests such as `test everything`, `see whether it works`, or a bare request for the full log. Ask for the complete log only when it is genuinely required; otherwise request results by checklist number and the smallest failure evidence.

Do not ask the user to perform physical/runtime testing from source code, a PR diff, a branch name, or a CI success alone. A runtime-test request is valid only after a downloadable artifact or deliberate `runtime-*` package exists for the exact commit being tested. If the workflow passed but produced no artifact, stop and fix the packaging/handoff workflow first.

Test candidates belong in GitHub Actions artifacts while they are under review. Do not update `main`, `stable`, or a `runtime-*` publication branch with a candidate until the required validation gate has passed and the promotion is deliberate.

## Gate-based runtime handoff

Do not use the user as a debugger loop. Agents must not publish a new runtime artifact for every internal hypothesis, blind guess, or micro-patch.

Before requesting user runtime testing, complete all feasible discovery, source/log inspection, static analysis, compile checks, and local validation. If the implementation depends on an unknown runtime/API boundary, resolve that boundary from available references, source, logs, artifacts, or a narrow diagnostic before asking for physical testing. If the boundary cannot be proven, stop and report the blocker instead of handing off a speculative build.

A runtime artifact may be handed off only when it answers one clear physical question. State the single gate being tested, such as load safety, runtime type proof, functional Gate A, or regression confirmation. The handoff must include expected log lines or visible behavior and a pass/fail rule.

Load safety comes first. If a candidate can break profile loading, game loading, taxonomy registration, or startup, it must fail closed before handoff. Do not expose unsafe runtime data or ask the user to confirm a known startup-blocking state.

When a runtime gate fails, do not immediately issue another artifact. First explain the failed boundary, the evidence that proves it, and the next smallest gate. User physical testing is reserved for defined gates, not internal patch iteration.

## User runtime-test budget

Manual SPT/EFT testing is a scarce integration resource, not a routine validation step.

- By default, the repository may have only one outstanding user runtime-test request across all active mod workstreams. A second request requires explicit user authorization.
- Before asking for a runtime test, inspect active PRs and Issues for an existing outstanding user gate. If one exists, continue all work that does not depend on physical evidence and queue the new gate instead of asking the user again.
- Red, incomplete, warning-unclassified, or artifact-less work must never be handed to the user. Restore deterministic CI first.
- Do not request a new physical run after every commit, patch, validator, or artifact. Accumulate related checks into one milestone candidate and one focused test session, normally no longer than 10-15 minutes.
- A user run must answer a product/runtime question that cannot be resolved from source, references, existing logs, automated tests, or artifact inspection. It must not exist merely to validate a validator or generate progress evidence.
- Do not stop feasible implementation, diagnosis, packaging, or documentation work merely because a user gate is queued. Stop only at the exact boundary that genuinely requires physical evidence.
- After a PASS or FAIL, consume the evidence once. Do not ask the user to repeat the same gate unless a material fix changed the tested boundary and all automated evidence is green again.

The active PR must record whether its user gate is `none`, `queued`, `active`, `passed`, or `failed`. Only one PR may normally use `active`; all others remain `queued` without requesting user action.

## Persistent profile safety

Changes that can serialize mod-owned identity into an SPT profile are high-risk. This includes item templates and instances, custom slots, trader IDs, quest IDs/state, assort references, builds, insurance, mail, and nested inventory trees.

- Before distributing such a candidate, maintain an immutable machine-readable identity manifest covering every persistent ID used by all distributed candidates, including retired/experimental IDs.
- The same candidate must provide an ownership-scoped disable/uninstall/recovery path, a backup-first procedure, and deterministic regression coverage. Generic SPT cleanup flags are not a substitute for module-owned recovery.
- Never rename, reuse, or silently drop a persistent ID after an artifact containing it has been distributed.
- A runtime handoff must state profile impact and recovery readiness. If removal or downgrade can strand a profile, the artifact is not ready for user testing.
- When a profile-load or save corruption incident is reported, freeze feature expansion for the responsible workstream. The only allowed work is evidence capture, ownership attribution, recovery, prevention, and CI/package proof until the affected profile is recoverable and the defect cannot recur.
- Do not ask the user to launch another candidate against the affected profile as a diagnostic shortcut. Work from a backup/copy, existing logs, and offline inspection first.

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

For every branch-cleanup pass, classify each candidate explicitly before deletion:

- `delete-now` — useful state is merged, expired, superseded, or deliberately discarded and no unique work remains;
- `retain-active` — active implementation, review, or required runtime validation still depends on it;
- `retain-evidence` — unique useful evidence/recovery material remains and its retention reason is documented;
- `manual-action` — cleanup is justified but the available GitHub interface cannot perform the required ref operation safely.

Ambiguous branches are never `delete-now`. Cleanup queues and one-time evidence belong in Issues, PR comments, or explicit manual reports, not permanent source-tree files.

## Priority

Active development/runtime validation > module-specific PR CI > deliberate publication > housekeeping.
