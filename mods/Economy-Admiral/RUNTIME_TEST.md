# Economy Admiral runtime gate

This document covers physical SPT 4.1.3 runtime acceptance for the current read-only Economy Admiral foundation.

## Candidate contract

Use the exact `economy-admiral-candidate` GitHub Actions artifact produced from the current PR head.

Install its contents under:

`SPT_Runtime/user/mods/Economy Admiral/`

The installed module directory must contain at least:

- `Economy-Admiral.dll`;
- `BUILD_INFO.json`;
- `config/config.json`;
- `README.md`;
- `RUNTIME_TEST.md`;
- `Validate-Runtime.ps1`.

`BUILD_INFO.json` binds the package to the exact PR head SHA and workflow run. The runtime validator rejects evidence without this build identity.

## Test A — Audit / Normal

1. Confirm `mode` is `Audit` and `preset` is `Normal`.
2. Start SPT 4.1.3 normally with the target mod stack enabled.
3. Allow all startup/PostLoad callbacks to finish.
4. Confirm there is no Economy Admiral startup exception or fatal server error.
5. Run:

```powershell
& '.\SPT_Runtime\user\mods\Economy Admiral\Validate-Runtime.ps1'
```

Expected result: exit code `0` and a green Economy Admiral PASS line.

The validator now requires:

- runtime evidence schema v3;
- exact packaged `BuildIdentity` for Economy Admiral / SPT 4.1.3;
- a pristine startup baseline captured at priority `1` before normal mod callbacks;
- positive `PristineQuestCount` and consistent `FinalQuestCount` / `ModAddedQuestCount`;
- all **9** analysis/planning reports;
- `PristineStartupSnapshot` benchmark provenance in primary audit, reward utility, progression graph and quest constraints;
- a provenance delta report classifying final quests as `PristineUnchanged`, `PristineModified` or `ModAdded`, plus removed pristine quest IDs;
- valid JSON in every report;
- identical before/after final-DB SHA-256 fingerprints;
- `DatabaseUnchangedAcrossPipeline = true`;
- `RuntimeGatePassed = true`;
- `ApplyMutations = false`;
- zero declared mutations;
- no selected composite candidate;
- no automatic target/enforcement mutation candidate.

The 9 working reports are:

1. `economy-admiral-audit.json`
2. `economy-admiral-reward-utility.json`
3. `economy-admiral-progression-graph.json`
4. `economy-admiral-quest-constraints.json`
5. `economy-admiral-quest-analysis.json`
6. `economy-admiral-provenance-delta.json`
7. `economy-admiral-composite-candidates.json`
8. `economy-admiral-target-proposals.json`
9. `economy-admiral-enforcement-plan.json`

`economy-admiral-runtime-evidence.json` is the manifest over those reports and is not included in the working-report count.

## Test B — Off mode

Run only after Test A succeeds.

1. Stop the SPT server.
2. Set `mode` to `Off` in `config/config.json`.
3. Delete or move the existing `reports` directory so stale files cannot satisfy the test.
4. Start SPT again and allow startup to finish.
5. Confirm Economy Admiral exits before pristine capture/final analysis output and does not recreate its report set.

`Off` is accepted only if no Economy Admiral analysis/planning/runtime-evidence reports are newly generated during that startup.

## Evidence to retain

For Audit-mode acceptance retain:

- the complete `reports` directory (**10 JSON files total: 9 working reports + runtime manifest**);
- the SPT server log from the same startup;
- `BUILD_INFO.json` from the installed module directory.

For Off-mode acceptance retain the server log and confirm no reports were regenerated.

The pristine-provenance runtime evidence is the gate for choosing/rejecting composite policy candidates and designing the first real enforcement transaction. No mutation path should be enabled before it is reviewed.
