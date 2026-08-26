# Economy Admiral runtime gate

Physical acceptance target: **SPT 4.1.3**, current target mod stack, exact `economy-admiral-spt413-dropin` artifact from the packaging-fix PR / its exact-head CI run.

## INSTALL — exact SPT layout

The Actions artifact is a **SPT-root drop-in package**. Its archive root must contain exactly:

```text
user/
└── mods/
    └── Economy Admiral/
        ├── Economy-Admiral.dll
        ├── BUILD_INFO.json
        ├── README.md
        ├── RUNTIME_TEST.md
        ├── Validate-Runtime.ps1
        ├── Validate-PrimaryParity.ps1
        └── config/
            └── config.json
```

Installation:

1. Stop SPT.
2. Delete or move the previous `SPT_Runtime/user/mods/Economy Admiral` folder so stale DLLs/files cannot survive the test.
3. Extract the **contents of the artifact archive directly into `SPT_Runtime`**. The archive's top-level `user` directory merges with the existing `SPT_Runtime/user` directory.
4. Verify before startup that this exact file exists:
   `SPT_Runtime/user/mods/Economy Admiral/Economy-Admiral.dll`.
5. Do **not** create another nested `Economy Admiral` directory and do not copy only the inner files by guessing.

If SPT reports `No Assemblies found in path: ...\user\mods\Economy Admiral`, the package/install layout is invalid and the runtime gate is not a valid Economy Admiral test.

## Audit / Normal gate

1. Keep `mode=Audit` and `preset=Normal`.
2. Keep the current target mod stack enabled, including Admiral Trader when testing the explicit adapter path.
3. Remove or archive old Economy Admiral reports so evidence cannot be mixed across runs.
4. Start SPT and allow all startup/PostLoad callbacks to finish.
5. Run the packaged `Validate-Runtime.ps1` from the Economy Admiral mod folder.
6. Run the packaged `Validate-PrimaryParity.ps1` from the same folder.

Both validators must exit with code `0` on the **same SPT run**.

## Gate A — provenance / zero mutation

`Validate-Runtime.ps1` PASS requires:

- runtime evidence schema v3;
- exact packaged build identity matching installed `BUILD_INFO.json`;
- pristine baseline captured at priority `1` before normal mod callbacks;
- positive pristine quest count;
- exact provenance partition with non-negative `added / modified / unchanged / removed` counts;
- `modified + unchanged + removed = pristine`;
- `added + modified + unchanged = final`;
- all 9 legacy/core working reports plus runtime manifest present;
- `PristineStartupSnapshot` benchmark source in primary, utility, progression and constraint reports;
- provenance delta exactly consistent with manifest counts;
- enforcement plan schema v4 / mutation-eligibility policy v2;
- `PristineUnchanged` candidates protected;
- `PristineModified` reward eligibility limited to reward dimensions actually proven changed versus pristine;
- unknown provenance blocked;
- every `AutomaticMutationAllowed=false` and every `ProposedMutation=null`;
- identical before/after final-DB fingerprints;
- quest fingerprint coverage includes trader identity, restartability, full conditions and full rewards;
- fingerprint also covers item identities, handbook prices and trader assort items/barter/loyalty mappings;
- `DatabaseUnchangedAcrossPipeline=true`;
- `RuntimeGatePassed=true`;
- zero declared mutations;
- no selected composite policy.

## Gate B — source-correct primary parity

`Validate-PrimaryParity.ps1` validates `reports/economy-admiral-primary-parity.json` independently of Gate A.

PASS requires:

- `SchemaVersion = 1`;
- `ExpectedSource = TypedFinalDbPlusPristineStartupSnapshot`;
- positive final and pristine quest counts;
- `ComparedQuestRows = FinalQuestCount`;
- `ExpectedQuestRewardSourceEdges = ReportedQuestRewardSourceEdges`;
- `QuestRowsMatch = true`;
- `AcquisitionMatches = true`;
- `BenchmarkMatches = true`;
- `AllMatched = true`;
- `Mismatches = []`.

Gate B is the physical evidence required before #139 may remove the legacy JSON-recursive reward extraction, hardcoded vanilla-trader membership, or correction-overlay chain. A clean DB fingerprint does **not** substitute for parity, and parity does **not** substitute for the zero-mutation fingerprint.

## Admiral Trader integration evidence

When Admiral Trader is installed, retain the same-run adapter/source-pressure reports as supplemental evidence. They must show:

- Admiral Trader discovered through its maintained stable `modGuid` contract;
- explicit-adapter attribution rather than heuristic attribution;
- finite/bounded supply evidence preserved for its maintained offers;
- effective progression derived from authored quest gates;
- LL1 remains assort metadata and is not used as the effective progression fallback;
- no adapter contract drift/fail-open behavior.

## Core reports

1. `economy-admiral-audit.json`
2. `economy-admiral-reward-utility.json`
3. `economy-admiral-progression-graph.json`
4. `economy-admiral-quest-constraints.json`
5. `economy-admiral-quest-analysis.json`
6. `economy-admiral-provenance-delta.json`
7. `economy-admiral-composite-candidates.json`
8. `economy-admiral-target-proposals.json`
9. `economy-admiral-enforcement-plan.json`
10. `economy-admiral-runtime-evidence.json` — runtime manifest
11. `economy-admiral-primary-parity.json` — independent source-correct parity evidence

Adapter/source-pressure JSON files are supplemental and should be retained when generated; they are not counted inside the 11-file core gate above.

## Off gate

Only after both Audit gates pass:

1. stop SPT;
2. set `mode=Off`;
3. remove or move existing Economy Admiral reports;
4. start SPT again;
5. confirm Economy Admiral generates no new reports and performs no audit pipeline work.

## RETURN — evidence to send back

Retain and return together:

- the complete same-run `SPT_Runtime/user/mods/Economy Admiral/reports/` directory;
- same-run SPT server log;
- installed `SPT_Runtime/user/mods/Economy Admiral/BUILD_INFO.json`;
- console output (or transcript) from `Validate-Runtime.ps1`;
- console output (or transcript) from `Validate-PrimaryParity.ps1`.

Do not mix reports from different SPT runs or from a different Economy Admiral artifact. This evidence is the acceptance gate before removing #139 correction overlays or advancing toward any active mutation transaction.
