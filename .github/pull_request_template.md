## Standing user authorization

- Workstream key:
- Active Issue:
- Phase-plan entry / acceptance:
- Resume evidence checked:

## Scope

- Changed behavior:
- Intentionally unchanged:
- Profile/performance risk:

## Validation

- Automated checks:
- CI run:
- Exact head SHA:

## Runtime handoff

- Required now: no / yes (why automation cannot decide):
- Primary clickable GitHub download URL:
- Download exactly (artifact/asset/ZIP name):
- Candidate (module/version/SPT/PR/exact head):
- Exact install/replace paths:
- Numbered action / PASS / FAIL checklist:
- Minimum evidence to return:
- Overall PASS rule:
- Link and exact candidate availability verified: no / yes

A worker must not ask the user to test while any required `yes` handoff field is blank. A PR, source branch, workflow page or CI result is provenance, not a substitute for the primary installable download.

## Integration

- Target branch:
- Issue/branch cleanup after merge:

Worker PRs execute the complete recorded plan in `origin/main:.github/workstreams.json` without permission from another chat. They do not redefine policy, roadmap, scope, or identity unless faithfully implementing an explicit user instruction.
