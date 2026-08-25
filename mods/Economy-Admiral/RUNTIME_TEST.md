# Economy Admiral runtime gate

This document covers physical SPT 4.1.3 runtime acceptance for the current read-only Economy Admiral foundation.

## Candidate contract

Use the exact `economy-admiral-candidate` GitHub Actions artifact produced from the current PR head and install its contents under:

`SPT_Runtime/user/mods/Economy Admiral/`

The module directory must contain `Economy-Admiral.dll`, `BUILD_INFO.json`, `config/config.json`, `README.md`, `RUNTIME_TEST.md`, and `Validate-Runtime.ps1`.

## Test A — Audit / Normal

1. Confirm `mode=Audit`, `preset=Normal`.
2. Start SPT 4.1.3 normally with the target mod stack.
3. Allow all startup/PostLoad callbacks to finish.
4. Confirm there is no Economy Admiral startup/fatal error.
5. Run `Validate-Runtime.ps1` from the installed Economy Admiral directory or pass its mod path explicitly.

Expected result: exit code `0` and a green Economy Admiral PASS line.

The validator requires:

- runtime evidence schema v3;
- exact packaged build identity;
- pristine baseline capture at priority `1`;
- positive pristine quest count and consistent final/mod-added counts;
- all **9 working reports** plus the runtime manifest;
- pristine benchmark source in primary/utility/progression/constraint reports;
- provenance delta with consistent pristine/final counts;
- provenance-aware enforcement plan;
- identical before/after final-DB fingerprints;
- `DatabaseUnchangedAcrossPipeline=true`;
- `RuntimeGatePassed=true`;
- zero mutations, no selected composite policy and no automatic mutation candidate.

Working reports:

1. `economy-admiral-audit.json`
2. `economy-admiral-reward-utility.json`
3. `economy-admiral-progression-graph.json`
4. `economy-admiral-quest-constraints.json`
5. `economy-admiral-quest-analysis.json`
6. `economy-admiral-provenance-delta.json`
7. `economy-admiral-composite-candidates.json`
8. `economy-admiral-target-proposals.json`
9. `economy-admiral-enforcement-plan.json`

`economy-admiral-runtime-evidence.json` is the manifest and is not included in the 9-report count.

## Test B — Off

Run after Test A succeeds:

1. stop the server;
2. set `mode=Off`;
3. remove/move old `reports`;
4. start the server again;
5. confirm no Economy Admiral reports are regenerated.

## Evidence to retain

Audit acceptance requires the complete `reports` directory (**10 JSON files**), same-run server log and installed `BUILD_INFO.json`.

The evidence must be reviewed before selecting a composite policy or implementing any mutation transaction.
