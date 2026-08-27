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
