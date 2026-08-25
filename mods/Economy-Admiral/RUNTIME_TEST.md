# Economy Admiral runtime gate

Physical acceptance target: **SPT 4.1.3**, current target mod stack, exact `economy-admiral-candidate` from PR #121.

## Audit / Normal gate

Install the Actions candidate under `SPT_Runtime/user/mods/Economy Admiral/`, leave `mode=Audit` / `preset=Normal`, start SPT and allow all startup/PostLoad callbacks to finish. Then run the packaged `Validate-Runtime.ps1`.

PASS requires:

- runtime evidence schema v3;
- exact packaged build identity;
- pristine baseline captured at priority `1` before normal mod callbacks;
- positive pristine quest count and consistent final/mod-added counts;
- **9/9 working reports** plus runtime manifest;
- `PristineStartupSnapshot` benchmark source in primary, utility, progression and constraint reports;
- provenance delta consistent with manifest counts;
- provenance-aware enforcement review plan;
- identical before/after final-DB fingerprints;
- `DatabaseUnchangedAcrossPipeline=true`;
- `RuntimeGatePassed=true`;
- zero mutations;
- no selected composite policy;
- no automatic mutation candidate.

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

`economy-admiral-runtime-evidence.json` is the manifest, giving **10 JSON files total**.

## Off gate

After Audit passes: stop SPT, set `mode=Off`, remove/move existing reports, start SPT again, and confirm Economy Admiral generates no new reports.

## Evidence

Retain the complete 10-JSON `reports` directory, the same-run SPT server log and installed `BUILD_INFO.json`. This evidence is the gate before any composite policy selection or mutation transaction design.
