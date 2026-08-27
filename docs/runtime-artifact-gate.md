# Runtime artifact gate

This policy applies to every SPT mod in this repository.

## Rule

When a task asks for a runtime candidate, gameplay alpha, installable package, user-test build, or artifact-only continuation, the next meaningful delivery is a CI-green installable artifact for the exact commit being evaluated.

Static validation is useful, but it is not runtime delivery. Tests, validators, manifests, docs, provenance files, and guard scripts only count as progress when they accompany or directly repair the artifact-producing path.

## Required artifact handoff

Every runtime-test handoff must name:

- module name;
- target SPT version;
- PR number, branch, and exact commit SHA;
- successful workflow run;
- artifact name;
- artifact digest or package checksum when available;
- exact install layout, including `SPT_Runtime/user/mods` and/or `BepInEx/plugins`;
- focused Gate A checklist;
- evidence requested from the user;
- explicit pass/fail rule.

Do not ask for runtime testing from a branch, PR diff, source tree, or CI success alone.

## Manual-test budget and scheduling

Manual runtime testing is the final physical gate for a milestone candidate, not a per-turn development loop.

1. There may be only one outstanding active user runtime gate repository-wide unless the user explicitly authorizes parallel tests.
2. A workstream with red CI, unresolved warnings, incomplete implementation, or no exact downloadable artifact cannot request user action.
3. Related runtime checks must be batched into one short session, normally 10-15 minutes or less.
4. Other candidates remain `queued`; their agents continue every task that can be completed without physical evidence.
5. A new test request after a previous run requires a material change to the same runtime boundary and fresh green automated evidence.
6. Validators and evidence scripts must consume normal runtime output when possible. They must not create repeated user chores whose only purpose is proving the validator itself.

Every PR that has a physical gate records one state: `none`, `queued`, `active`, `passed`, or `failed`. Moving a gate from `queued` to `active` requires confirming that no other active PR already owns the user test window.

## Profile-affecting candidates

Before distributing a candidate that can persist custom item, slot, trader, quest, assort, build, insurance, mail, or inventory identity:

- list all current and historically distributed persistent IDs in a machine-readable manifest;
- keep those IDs immutable and never reuse them;
- provide a backup-first, ownership-scoped recovery/uninstall tool in the artifact;
- prove recovery is idempotent and preserves unrelated data;
- state the exact profile impact and recovery readiness in the handoff.

Generic SPT options that remove invalid traders/items or repair inventory structure do not replace module-owned recovery. If a candidate can leave the profile unloadable after disable, removal, downgrade, or failed registration, it is not eligible for runtime handoff.

Any reported profile-load/save incident immediately suspends feature development and new runtime-test requests for that workstream. Recovery of the affected profile and prevention of recurrence become the sole gate.

## Static-only stop condition

After an artifact-only/runtime-candidate instruction, a follow-up that only adds documentation, validators, guards, tests, or manifests is incomplete unless it also produces or fixes the artifact workflow/package.

If the artifact cannot be produced, stop and report the exact missing boundary:

- missing source file or asset;
- missing SPT reference;
- broken package layout;
- failed build step;
- missing workflow artifact upload;
- unavailable credentials or runner capability;
- any other concrete blocker.

Do not replace a blocked artifact with another static check.

## PR status

Keep the PR draft while required runtime evidence is absent. A PR can be considered ready only when its required artifact exists and its runtime gate is either passed or explicitly documented as the remaining user validation.

## Publication

Actions artifacts are the normal place for test candidates. Do not promote to `main`, `stable`, or a `runtime-*` publication branch until the required runtime gate has passed and the promotion is deliberate.
