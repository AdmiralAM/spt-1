# CI workstream map

Development validation is module-specific. A pull request should trigger only the validation needed for the module it changes, plus narrowly related repository checks.

| Module | Pull-request workflow | Publication channel |
| --- | --- | --- |
| Tactical HUD | `Tactical HUD Validate` | `runtime` |
| Item Intelligence | `Item Intelligence Validate` | `runtime-item-intelligence` |
| Pause | `Pause Validate` | `runtime-pause` |
| Belt/Armband Inventory | `Belt Armband Inventory Validate` | `runtime-belt-armband` |
| Quest Planner | `Quest Planner Validate` | CI artifacts until a dedicated runtime channel is deliberately introduced |
| Artem Revival | its dedicated workstream validation while the revival PR remains active | no publication channel until its own acceptance gates are satisfied |

## Rules

- Each unrelated module workflow uses its own concurrency group.
- `cancel-in-progress: true` is allowed only inside one module/PR scope so a newer commit can replace an older run of the same workstream.
- Documentation-only and repository-hygiene work must not invoke the repository-wide publisher.
- `Publish SPT Mod Suite` is manual release/promotion control, not normal development CI.
- Actions artifacts are transient build/test output. They are not committed to `main`.
- A runtime branch is install-only output and must never be used as a development branch.

See `CONTRIBUTING.md` and `docs/development-workflow.md` for the complete repository lifecycle.