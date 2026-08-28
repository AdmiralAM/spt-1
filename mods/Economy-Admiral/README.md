# Economy Admiral

Economy Admiral is an SPT 4.1.3 server-side economy governor. It combines a physically proven, provenance-safe quest-reward transaction path with deterministic source-pressure/health observation and an explicit Admiral Trader compatibility boundary.

Version is **0.1.0**. Default configuration remains conservative:

```json
{
  "mode": "Audit",
  "preset": "Normal",
  "enableItemRewardStackNormalization": false
}
```

`Audit` analyzes and previews without mutating the final SPT database. `Enforce` applies only supported, provenance-eligible bounded reward mutations. `Off` skips the Economy Admiral pipeline.

## Accepted reward enforcement

The production mutation path supports:

- `Experience`;
- `TraderStanding`;
- opt-in bounded `ItemRewardStackCount` for one structurally unambiguous existing Success reward stack.

The XP/standing Alpha and bounded item-stack slice are physically proven on SPT 4.1.3. Grouped item handling is deterministic and fail-closed; a grouped mutation may legitimately be `NOT APPLICABLE` when the installed quest set exposes no safely reducible grouped candidate.

### Provenance safety

- `PristineUnchanged`: never mutate.
- `ModAdded`: only flagged/manual supported dimensions may mutate.
- `PristineModified`: a dimension may mutate only when the pristine delta proves that exact dimension changed.
- unknown provenance: block.
- manual exact targets do not bypass provenance or dimension safety.

For item-stack normalization on `PristineModified`, `ItemRewardStackCount` requires `SuccessItemHandbookValue` to be proven changed.

### Bounded item-stack rules

When `enableItemRewardStackNormalization=true`, automatic item normalization requires an existing Success Item reward whose selected stack is uniquely safe, finite, integral, greater than one and handbook-priced. The complete Success item bundle is budgeted, immutable sibling reward value is reserved, and the policy only lowers the selected existing stack to an integer of at least one.

Economy Admiral never replaces `_tpl`, creates/deletes reward records, removes the last item to satisfy a budget, or rewrites structural quest fields. `Reward.Value` and `Upd.StackObjectsCount` are updated and rolled back together.

### Transaction contract

Every active reward mutation shares `NumericRewardTransactionCore`:

1. deterministic plan;
2. journal all original values before the first write;
3. apply;
4. verify exact targets;
5. rollback the entire batch on any error;
6. verify rollback.

CI covers commit, rollback, rollback restoration, idempotence, grouped quantity synchronization and mixed XP/standing/item transactions.

## Source-pressure observation

Economy Admiral observes acquisition pressure once at startup/PostLoad and does no raid/frame polling.

Maintained observation includes:

- final-DB trader currency purchases and barters;
- normal and repeatable quest item rewards;
- hideout crafts;
- explicitly registered external adapter evidence;
- a bounded effective-acquisition graph with memoization, cycle detection and explicit depth/path caps.

Flea remains **reference-only**; Economy Admiral does not become a flea simulator. World loot remains explicit `UnknownNoMaintainedAdapter` until a maintained ownership-safe adapter exists; missing evidence is never converted into zero supply.

The source-pressure report keeps per-item/channel source diversity, renewable-path evidence, progression coverage, concentration, bounded supply coverage, provenance classes, effective-acquisition evidence and measured startup cost separately inspectable.

## Health invariants

Health evidence is policy-free and fail-closed. There is no opaque single health score.

The invariant model can represent `Pass`, `Fail` or `Unknown` for:

- protected pristine content;
- proof that the targeted dimension was actually changed;
- renewable-path continuity;
- progression-access regression;
- source-concentration regression;
- attribution confidence/conflict;
- bounded intervention magnitude.

Missing evidence remains `Unknown` and blocks future automatic action. The runtime health report explicitly keeps `CompositeScoreSelected=false` and `MutationAuthorized=false`; observation does not silently create a new mutation domain.

## Admiral Trader compatibility

Economy Admiral consumes Admiral Trader only through the maintained explicit adapter. It does **not** infer ownership from display names, loyalty levels or ID patterns and does not implement a second Trader economy engine.

For the maintained Gameplay Alpha v4 contract it validates:

- product name `Admiral Trader`;
- modGuid `com.admiralam.spt.admiraltrader`;
- trader ID `d5c27bb3169f8dfbc13f6b69`;
- runtime trader identity/avatar route;
- explicit `Baseline` / `Relationship` / `Milestone` stock classes;
- authored milestone quest gates;
- finite permanent stock/buy limits;
- `ExplicitAdapter` provenance;
- Special Weapons remain sample-only rather than permanent offers.

The current Gameplay Alpha has no maintained Relationship offer manifest, so Relationship is supported as a valid explicit class but its current materialized count is zero; Economy Admiral does not fabricate Relationship offers.

Missing Trader contracts produce `ContractUnavailable`; malformed/incompatible maintained contracts produce `ContractUnsupported`. Such evidence is non-authoritative and cannot silently authorize automatic normalization.

## Economy Beta ownership gate

Beta enforcement introduces **no new mutation dimension**. It retains the physically accepted XP/standing/opt-in item-stack transaction core.

Non-Admiral quests continue through the existing provenance/dimension rules. For quests owned by Admiral Trader, automatic reward normalization additionally requires exact Gameplay Alpha v4 identity/class/bounded-supply/`ExplicitAdapter` evidence. If that maintained ownership contract is absent, legacy, incompatible or drifted, only automatic mutation-driving flags for those Trader quests are suppressed. Explicit manual exact targets still remain subject to the existing provenance and dimension gates.

## Runtime reports

The seven core quest/economy reports remain the runtime-evidence manifest contract:

1. `economy-admiral-audit.json`
2. `economy-admiral-reward-utility.json`
3. `economy-admiral-progression-graph.json`
4. `economy-admiral-quest-constraints.json`
5. `economy-admiral-quest-analysis.json`
6. `economy-admiral-provenance-delta.json`
7. `economy-admiral-enforcement-plan.json`

Additional Beta observation evidence is emitted separately:

- `economy-admiral-source-pressure.json`;
- `economy-admiral-health.json`;
- `economy-admiral-admiral-trader-adapter.json`;
- `economy-admiral-grouped-item-evidence.json` when applicable.

`economy-admiral-runtime-evidence.json` carries exact build identity and the core before/after DB fingerprint evidence.

## Packaged validators

- `Validate-Runtime.ps1` — Audit/read-only contract.
- `Validate-Enforce.ps1` — strict transactional Enforce contract for XP/standing and the opt-in bounded item-stack mode.
- `Validate-Beta.ps1` — the single combined Economy Beta release-candidate gate. It runs the strict Enforce validator and then verifies source-pressure, health and explicit Admiral Trader v4 compatibility evidence from the same SPT startup.

See `RUNTIME_TEST.md` for the one batched SPT 4.1.3 release-candidate procedure. Earlier accepted Alpha/item-stack physical tests are not repeated as micro-tests.

## Installation and publication

The maintained install-only channel is **`runtime-economy-admiral`**. Its root is directly copyable into the SPT root and contains:

`SPT_Runtime/user/mods/Economy Admiral/`

The runtime branch also carries `runtime-manifest.json` with product/version/SPT/source identity. Publication is isolated from the suite `stable` branch and from other runtime channels.

The accepted 0.1.0 Beta runtime gate passed on exact RC head `62e46f0a458991f19daa8363db271c8eb8cdd0ec` / workflow `33157209788`, after which the RC was integrated and republished from authoritative `main` through the dedicated Economy runtime publication workflow.

## Runtime boundary and performance

Compile boundary: `SPTarkov.Server.Core 4.1.2` / .NET 10. Physical target: **SPT 4.1.3**. Packaged candidates include exact head/workflow identity in `BUILD_INFO.json`.

Load order:

1. `OnLoadOrder.Watermark + 1` — immutable pristine startup snapshot;
2. normal SPT/mod callbacks;
3. `PostLoad + 1000` — final DB analysis, observation and optional bounded enforcement.

There is no permanent polling, raid/frame scan, repeated report-reparse correction chain, primary parity shadow scan, composite candidate pass or target-envelope pass on the runtime path.

## Explicit non-goals for 0.1.0 Beta

Economy Admiral does not implement a second Trader economy engine, flea simulator, world-loot controller, item-template replacement engine, insurance overhaul or generic cross-mod mutation attribution system. PBS/Scorpion/Artem/Andrudis-specific ownership adapters are not inferred automatically.

Economy Admiral 0.1.0 Beta is physically accepted on SPT 4.1.3 and has a maintained install-only runtime publication channel. Further product expansion requires a new recorded scope rather than silently extending this accepted release.
