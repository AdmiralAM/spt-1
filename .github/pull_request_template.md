## Objective / linked Issue

<!-- Link the Issue or state the narrowly scoped objective. -->

## Scope

- Affected module(s):
- Explicit non-goals:
- Persistent profile impact (`none` or exact IDs/state written):
- Work-package objective:
- Package state (`active` / `blocked` / `runtime-gate` / `complete`):
- Final package acceptance condition:
- Product roadmap / authoritative Issue:
- Current product phase:
- Artifact class (`none` / `diagnostic` / `runtime-candidate` / `release-candidate` / `release`):
- Final stable/release acceptance criteria:
- Successor package after PASS:
- FAIL transition / remediation path:

## Ordered package gates

| # | Gate | Acceptance | State |
| --- | --- | --- | --- |
| 1 |  |  | `pending` |
| 2 |  |  | `pending` |

## Changes

<!-- What changed and why? -->

## Validation

- [ ] Module-specific automated tests/builds passed where applicable.
- [ ] CI annotations/warnings were reviewed and are fixed, or explicitly classified as non-blocking/follow-up debt.
- [ ] Runtime/user validation is complete, or the remaining runtime gate is explicitly documented.
- [ ] If this PR is a runtime candidate/gameplay alpha/installable package, the exact artifact exists; static checks/docs/validators are not being used as a substitute for delivery.
- [ ] Existing logs/results were inspected before triggering reruns.
- [ ] If this PR ports or uses an old reference/mod/branch, baseline viability was checked and documented before implementation.
- [ ] Any failure was diagnosed at the first failed boundary before rerunning.
- [ ] No unnecessary repository-wide/full-suite validation was used.
- [ ] No unrelated module behavior was changed.
- [ ] No shared CI concurrency group was introduced with unrelated workstreams.
- [ ] This PR does not invoke or depend on `Publish SPT Mod Suite` merely for development validation.
- [ ] The complete work package was executed autonomously; internal gates were not turned into repeated user handoffs.
- [ ] Passing an internal gate automatically advanced to the next recorded gate.
- [ ] No terminal update was issued before all package gates/final acceptance completed or a genuine blocker was recorded.
- [ ] The agent did not shrink the package into a self-selected discovery/docs/one-file/one-commit gate.
- [ ] This package is linked to a roadmap through stable/release; its artifact is not being mislabeled as the final product.
- [ ] PASS automatically activates the recorded successor package unless the product's stable/release criteria are satisfied.
- [ ] FAIL returns this package to evidence-driven remediation without requiring the user to restate the task.
- [ ] A branch/PR boundary is not being used as a reason to stop when the next roadmap phase is already authorized.

## Runtime test handoff

<!-- Required when asking the user to test an Actions artifact or runtime package. Delete or mark N/A for docs-only/no-runtime PRs. -->

- PR / branch / commit SHA:
- Successful workflow run and clickable GitHub URL:
- Artifact name / artifact ID:
- Direct GitHub artifact URL or deliberate GitHub runtime/release URL:
- Artifact digest / package checksum:
- User gate state (`none` / `queued` / `active` / `passed` / `failed`):
- Single runtime gate being tested:
- Install layout:
- Point-by-point checklist:

| # | Exact action | Expected PASS | Explicit FAIL | Evidence to return |
| --- | --- | --- | --- | --- |
| 1 |  |  |  |  |

- Overall gate decision rule:
- Required returned evidence:
- [ ] I am not asking for runtime testing from CI success alone; the named artifact/package exists for the exact commit above.
- [ ] I did not stop at docs/tests/validators after an artifact-only or runtime-candidate instruction.
- [ ] This handoff answers one clear physical question and is not an internal micro-patch/debug loop.
- [ ] Unknown runtime/API boundaries were resolved from references, source, logs, artifacts, or narrow diagnostics before requesting physical testing.
- [ ] Load safety was considered first; this artifact is not expected to block profile/game loading or startup.
- [ ] No other PR currently owns the single active user runtime-test window, or the user explicitly authorized parallel tests.
- [ ] Related checks were batched into one short milestone session instead of per-commit requests.
- [ ] CI is green and warnings are classified; red/incomplete work is not being handed to the user.
- [ ] The candidate is linked from GitHub; no chat attachment, automatic chat download, local file, or source ZIP is being substituted.
- [ ] The GitHub link identifies the exact artifact ID for the exact commit and is currently downloadable.
- [ ] Every checklist row has a concrete action, PASS result, FAIL result, and minimal evidence request.

## Persistent profile safety

<!-- Required when the PR can serialize mod-owned IDs/state. Mark N/A only when it truly has no profile impact. -->

- Persistent identity manifest:
- Historically distributed IDs covered:
- Backup-first recovery/uninstall path:
- Recovery regression / evidence:
- [ ] No distributed persistent ID was renamed, reused, or omitted from recovery ownership.
- [ ] Disabling/removing/downgrading this candidate is not expected to strand the user's profile.
- [ ] No unresolved profile-load/save incident exists for this workstream.

## Repository hygiene

- [ ] No generated build/test logs, CI metadata, temporary trigger/evidence files, dependency caches, or package copies were added to source history.
- [ ] Temporary diagnostics are removed or explicitly justified as still required.
- [ ] Current-state README/docs were updated if behavior/status changed.
- [ ] The work branch can be deleted after merge; GitHub automatic head-branch deletion is expected to handle normal merged PRs.

## Publication

<!-- Leave blank for ordinary development. State explicitly if a deliberate runtime/stable publication is required after merge. -->
