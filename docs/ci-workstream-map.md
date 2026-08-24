# CI workstream map

Development validation is module-specific. A pull request should trigger only the validation needed for the module it changes, plus narrowly related repository checks.

| Module | Pull-request workflow | Publication channel |
| --- | --- | --- |
| Tactical HUD | `Tactical HUD Validate` | `runtime` |
| Item Intelligence | `Item Intelligence Validate` | `runtime-item-intelligence` |
| Pause | `Pause Validate` | `runtime-pause` |
| Belt/Armband Inventory | `Belt Armband Inventory Validate` | `runtime-belt-armband` |
| Quest Planner | `Quest Planner Validate` | CI artifacts until a dedicated runtime channel is deliberately introduced |
| Artem Revival | dedicated workstream validation while its revival PR remains active | no publication channel until its own acceptance gates are satisfied |

## Isolation rules

- Each unrelated module workflow uses its own concurrency group.
- `cancel-in-progress: true` is allowed only inside one module/PR scope so a newer commit can replace an older run of the same workstream.
- Documentation-only and repository-hygiene work must not invoke unrelated heavy module builds or the repository-wide publisher.
- `Publish SPT Mod Suite` is manual release/promotion control, not normal development CI.
- Actions artifacts are transient build/test output. They are not committed to `main`.
- A runtime branch is install-only output and must never be used as a development branch.

## Actions resource policy

GitHub-hosted runner time is a finite engineering resource even when monthly quota remains or additional quota can be purchased.

- Inspect the code, diff, existing logs, and prior check result before scheduling another run.
- Prefer the narrowest module/path-specific check that can prove the required condition.
- Order validation fail-fast: cheap deterministic/static checks first, expensive builds and package assembly later.
- On failure, identify the first failed boundary before rerunning anything.
- Rerun only the smallest required failed job/check when supported; do not rerun an unchanged complete workflow as a diagnostic substitute.
- Avoid duplicate builds/tests when a current check already proves the same contract.
- Treat unnecessary Windows-hosted jobs, redundant downloads/setup, excessive failed-job minutes, broad path triggers, and repeated full-suite runs as CI hygiene defects.
- Increasing the Actions quota provides capacity for useful work; it does not relax these efficiency rules.

See `AGENTS.md`, `CONTRIBUTING.md`, and `docs/development-workflow.md` for the complete repository lifecycle.
