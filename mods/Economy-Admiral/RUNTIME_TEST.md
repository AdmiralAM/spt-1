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

The final physical gate is one server startup and one command. It simultaneously checks the already accepted transactional Enforce path plus the Economy Beta observation/compatibility boundaries.

**Admiral Trader is an optional dependency.** Economy Admiral must run and validate standalone when Admiral Trader is absent. When Admiral Trader is installed, Economy Admiral must load the maintained explicit adapter and validate the Trader contract strictly and fail-closed on identity/schema/offer-class drift. Economy Admiral never infers Trader ownership from names/IDs alone and never creates a second Trader economy engine.

The playable RC artifact is already preconfigured for:

```json
"mode": "Enforce",
"preset": "Normal",
"enableItemRewardStackNormalization": true,
"enableTraderPurchasePressure": true,
"enableFleaPurchasePressure": true,
"enableLootPressure": true
```

No manual config editing or quest reward overrides are required for the RC artifact.

### One batched runtime test

1. Install the exact GitHub RC artifact over `user/mods/Economy Admiral`.
2. Admiral Trader may be present or absent. If present, use the maintained supported package.
3. Start the SPT server once and allow startup to finish completely.
4. Close the server.
5. From `user/mods/Economy Admiral` run exactly:

```powershell
.\Validate-Beta.ps1
```

Return only the final PowerShell output.

## PASS contract

`Validate-Beta.ps1` first executes the established strict Enforce validator and then requires all Economy Beta evidence from that same startup:

- real committed transactional reward mutations with exact before/current/target/after evidence;
- pristine/unknown provenance protection, rollback-safe transaction semantics and bounded item-stack proof;
- source-pressure schema 2 with final-DB evidence;
- world loot remains explicit `UnknownNoMaintainedAdapter` rather than fabricated zero supply;
- health schema 1 remains separately inspectable, selects no opaque composite score and does not independently authorize mutation;
- when Admiral Trader is absent, adapter state is exactly `NotInstalled` and no Trader adapter is falsely claimed by source-pressure evidence;
- when Admiral Trader is installed, adapter schema 3 resolves exact product name, modGuid and frozen trader ID through Gameplay Alpha schema v4;
- when Admiral Trader is installed, every maintained permanent Trader offer is explicitly classified `Baseline` / `Relationship` / `Milestone`, remains bounded and retains `ExplicitAdapter` provenance;
- when Admiral Trader is installed, milestone offers preserve authored effective quest gates and Special Weapons remain sample-only;
- exact Economy Admiral build SHA/workflow identity is present.

Absent Admiral Trader is valid. An installed but missing/incompatible Trader contract, inferred/unclassified offer, lost quest gate, unbounded offer, attribution drift, health mutation authorization or source-pressure boundary regression is FAIL.

## After the gate

On PASS, the exact tested candidate is eligible for the recorded `stable-release` transition. On FAIL, use the returned validator output to remediate the same workstream; do not create unrelated economy scope or ask the user for additional exploratory tests.
