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
- clickable GitHub Actions run URL;
- artifact name;
- artifact ID and direct GitHub-hosted artifact URL, or an explicit maintained GitHub runtime/release URL;
- artifact digest or package checksum when available;
- exact install layout, including `SPT_Runtime/user/mods` and/or `BepInEx/plugins`;
- focused Gate A checklist;
- evidence requested from the user;
- explicit pass/fail rule.

Do not ask for runtime testing from a branch, PR diff, source tree, or CI success alone.

## GitHub-only candidate delivery

The user must download the candidate from GitHub. Accepted handoff sources are:

- the exact GitHub Actions artifact produced by the named successful run;
- a deliberate maintained `runtime-*` branch package on GitHub;
- a deliberate GitHub Release asset.

A chat attachment, automatic chat download, local/sandbox path, pasted binary, generic repository ZIP, or source checkout is not an accepted runtime-test handoff. Provide a normal clickable GitHub URL that the user can open independently from the chat. For an Actions candidate, link both the run page and the specific artifact ID. If the artifact is expired or unavailable, the gate remains queued until a replacement GitHub artifact is produced.

## Point-by-point test contract

The user must know exactly what the candidate is intended to prove. Present the physical checklist in this form:

| # | Exact action | Expected PASS | Explicit FAIL | Evidence to return |
| --- | --- | --- | --- | --- |
| 1 | One concrete user action | One observable result | One observable contrary result | Smallest useful log line, screenshot, or `PASS`/`FAIL` |

Every row must affect the gate decision. Number the rows, keep their order stable, and state the overall rule (for example, `PASS only when items 1-4 all pass`). Do not combine unrelated product areas, ask the user to explore freely, or request a full log when numbered PASS/FAIL results or a narrow failure excerpt are sufficient.

## Manual-test budget and scheduling

Manual runtime testing is the final physical gate for a milestone candidate, not a per-turn development loop.

1. Each workstream may request one batched user runtime session only when it reaches its recorded `requiresUserRuntime` phase and all feasible automated work is complete.
2. No controller, coordination chat, registry edit, or permission from another worker activates that handoff. The module worker asks the user directly.
3. A workstream with red CI, unresolved warnings, incomplete implementation, or no exact downloadable artifact cannot request user action.
4. Related runtime checks must be batched into one short session, normally 10-15 minutes or less.
5. If several candidates are ready, the user chooses when and in which order to run them; one workstream never blocks another workstream's non-physical work.
6. A new test request after a previous run requires a material change to the same runtime boundary and fresh green automated evidence.
7. Validators and evidence scripts must consume normal runtime output when possible. They must not create repeated user chores whose only purpose is proving the validator itself.

Runtime readiness and PASS/FAIL evidence belong in the module PR/Issue, not in mutable registry gate fields.

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

Actions artifacts are the normal place for test candidates. Do not promote to `main`, `stable`, or a `runtime-*` publication branch until the required runtime gate has passed. After PASS, continue the recorded stable/publication phase automatically under the user's standing authorization unless the user explicitly placed the release on hold.
