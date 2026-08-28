# Economy Admiral runtime testing

Physical SPT 4.1.3 testing is reserved for the single combined release-candidate gate after automated work is exhausted. Earlier Alpha/item-stack runtime proofs remain accepted historical evidence and are not repeated as separate user tasks.

## Already accepted physical evidence

Economy Admiral has already physically proven on SPT 4.1.3:

- Audit is read-only with concrete policy preview and unchanged DB fingerprint;
- Enforce performs real `Experience` / `TraderStanding` mutations with exact targets and pristine protection;
- bounded opt-in `ItemRewardStackCount` mutation updates `Reward.Value` and `Upd.StackObjectsCount` transactionally;
- grouped reward handling is safe and may be `NOT APPLICABLE` when the installed modset exposes no reducible grouped candidate;
- transaction commit, rollback, rollback verification and second-pass idempotence;
- Off disables the pipeline.

These proofs are retained by deterministic CI regressions. Do not request separate Audit/parity/item-stack micro-tests again.

## Economy Beta release-candidate gate

The final physical gate is one server startup and one command. It simultaneously checks the already accepted transactional Enforce path plus the new Economy Beta observation/compatibility boundaries.

The release-candidate environment must include the maintained **Admiral Trader** package because stable Economy acceptance includes explicit Trader compatibility. Economy Admiral never infers Trader ownership from names/IDs alone and never creates a second Trader economy engine.

For the RC run use:

```json
"mode": "Enforce",
"preset": "Normal",
"enableItemRewardStackNormalization": true
```

No manual quest reward overrides are required.

### One batched runtime test

1. Install the exact GitHub artifact over `user/mods/Economy Admiral`.
2. Install/use the maintained Admiral Trader Gameplay Alpha package for the same SPT 4.1.3 runtime session.
3. Set the three Economy Admiral configuration values above.
4. Start the SPT server once and allow startup to finish completely.
5. Close the server.
6. From `user/mods/Economy Admiral` run exactly:

```powershell
.\Validate-Beta.ps1
```

Return only the final PowerShell output.

## PASS contract

`Validate-Beta.ps1` first executes the established strict Enforce validator and then requires all Economy Beta evidence from that same startup:

- real committed transactional reward mutations with exact before/current/target/after evidence;
- pristine/unknown provenance protection, rollback-safe transaction semantics and bounded item-stack proof;
- source-pressure schema 2 with final-DB evidence plus the loaded explicit Admiral Trader adapter;
- world loot remains explicit `UnknownNoMaintainedAdapter` rather than fabricated zero supply;
- health schema 1 remains separately inspectable, selects no opaque composite score and does not independently authorize mutation;
- Admiral Trader adapter schema 3 resolves exact product name, modGuid and frozen trader ID through Gameplay Alpha schema v4;
- every maintained permanent Trader offer is explicitly classified `Baseline` / `Relationship` / `Milestone`, remains bounded and retains `ExplicitAdapter` provenance;
- milestone offers preserve authored effective quest gates;
- Special Weapons remain sample-only and are not converted into permanent offers;
- exact Economy Admiral build SHA/workflow identity is present.

Any missing/incompatible Trader contract, inferred/unclassified offer, lost quest gate, unbounded offer, attribution drift, health mutation authorization or source-pressure boundary regression is FAIL.

## After the gate

On PASS, the exact tested candidate is eligible for the recorded `stable-release` transition. On FAIL, use the returned validator output to remediate the same workstream; do not create unrelated economy scope or ask the user for additional exploratory tests.
