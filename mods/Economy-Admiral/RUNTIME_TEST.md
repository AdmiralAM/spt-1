# Economy Admiral — Enforce Alpha runtime gate

Physical acceptance target: **SPT 4.1.3**, exact `economy-admiral-candidate` artifact built from PR #207 exact head.

The packaged default remains `mode=Audit`. Do not edit the DLL or mix reports between runs.

## Install

Install the candidate at the same SPT 4.1.3 mod location already used by the target stack:

`SPT_Runtime/user/mods/Economy Admiral/`

Before every test run, remove/archive the previous `reports/` directory.

## Run A — Audit / Normal

1. Keep `mode=Audit`, `preset=Normal`.
2. Start SPT and let all PostLoad callbacks finish.
3. Run `Validate-Runtime.ps1` from the Economy Admiral folder.
4. Run `Validate-PrimaryParity.ps1` from the same folder.

Acceptance:

- SPT starts successfully;
- `Validate-Runtime.ps1` exits `0`;
- Audit before/after DB fingerprints are identical;
- `MutationCount = 0`;
- a concrete `SelectedPolicy` exists;
- eligible XP / TraderStanding proposals may be present, but every proposal has `Applied=false` and `After=Before`;
- `PristineUnchanged` has no proposed mutation;
- primary typed/pristine parity remains PASS.

## Run B — Enforce / Normal

Only after Run A passes:

1. Stop SPT.
2. Set `mode=Enforce`, keep `preset=Normal`.
3. Remove/archive Run A reports.
4. Start SPT again and let PostLoad finish.
5. Run `Validate-Enforce.ps1`.

Acceptance for the first Alpha physical proof:

- SPT starts successfully;
- a concrete `SelectedPolicy = PresetNumericQuestRewardCapV1/Normal` is recorded;
- `ApplyMutations=true`;
- `PlannedMutationCount >= MutationCount`;
- **`MutationCount > 0`**;
- transaction committed and did not roll back;
- DB fingerprint changed;
- every applied dimension is only `Experience` or `TraderStanding`;
- every applied record contains exact `Before`, `Current`, `Target`, `After`;
- `Before = Current` and `After = Target` within dimension tolerance;
- an automatic policy never increases reward magnitude;
- `PristineUnchanged` remains untouched;
- a `PristineModified` field may change only when that exact dimension is present in `ChangedDimensions`;
- unknown provenance is never mutated;
- declared mutation count equals the number of applied records.

If the current target stack happens to contain no automatically eligible numeric outlier, use a **known ModAdded quest** from the Audit plan and add an exact `questRewardOverrides` target for an existing XP or TraderStanding reward. A manual exact target does not bypass provenance protection: pristine/unknown quests remain blocked.

Example shape only — use an actual ModAdded quest id and a value appropriate for the observed Audit record:

```json
"questRewardOverrides": {
  "ACTUAL_MOD_ADDED_QUEST_ID": {
    "allowAutomaticMutation": true,
    "experienceTarget": 3000,
    "traderStandingTarget": null,
    "note": "Alpha physical mutation proof"
  }
}
```

## Idempotence / rollback evidence

CI executes the exact production transaction core used by Enforce and proves:

- committed XP normalization reaches target;
- once current value equals the automatic target, `NeedsMutation=false` on a second pass over the same DB state;
- a synthetic failure after an earlier mutation rolls back the entire batch;
- all original slot values are verified after rollback.

The physical first-Alpha run additionally proves the typed SPT `Reward.Value` mutation path against the real SPT 4.1.3 DB.

## Run C — Off

1. Stop SPT.
2. Set `mode=Off`.
3. Remove/archive existing reports.
4. Start SPT.
5. Economy Admiral must generate no new reports and must perform no analysis or mutation pipeline work.

## Reports/evidence to return

Return together from the same physical run:

- complete `reports/` directory;
- same-run SPT server log;
- installed `BUILD_INFO.json`;
- validator console output:
  - Run A: `Validate-Runtime.ps1` + `Validate-PrimaryParity.ps1`;
  - Run B: `Validate-Enforce.ps1`.

Core reports remain:

1. `economy-admiral-audit.json`
2. `economy-admiral-reward-utility.json`
3. `economy-admiral-progression-graph.json`
4. `economy-admiral-quest-constraints.json`
5. `economy-admiral-quest-analysis.json`
6. `economy-admiral-provenance-delta.json`
7. `economy-admiral-composite-candidates.json`
8. `economy-admiral-target-proposals.json`
9. `economy-admiral-enforcement-plan.json`
10. `economy-admiral-runtime-evidence.json`
11. `economy-admiral-primary-parity.json`

Admiral Trader adapter/source-pressure reports are supplemental only and do not gate the XP/standing Alpha.

Do not mix reports from different runs or artifacts.
