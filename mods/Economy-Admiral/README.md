# Economy Admiral

Economy Admiral is an SPT 4.1.3 server-side economy normalization mod. The physically accepted Alpha mutates only numeric quest rewards (`Experience` and `TraderStanding`) under strict pristine/provenance and transaction safety rules.

## Current product state

### Physically accepted Enforce Alpha

The accepted Alpha is the narrow Experience/TraderStanding slice. It has been physically exercised on SPT 4.1.3 with Audit read-only proof, real Enforce DB mutations, pristine protection, exact target verification, rollback/idempotence smoke coverage, and Off-mode proof.

Default configuration remains conservative:

```json
{
  "mode": "Audit",
  "preset": "Normal",
  "enableItemRewardStackNormalization": false
}
```

`Audit` previews policy decisions without mutating the final SPT DB. `Enforce` activates only dimensions allowed by the selected product contract and provenance gates. `Off` skips the Economy Admiral pipeline.

### Post-Alpha bounded item-stack slice

A new opt-in slice can reduce the quantity of a **single existing stackable Success item reward** when it is a policy outlier. It is deliberately disabled by default:

```json
"enableItemRewardStackNormalization": false
```

When explicitly enabled, Economy Admiral may reduce `ItemRewardStackCount` only when all of the following are true:

- the quest is `ModAdded`, or it is `PristineModified` with `SuccessItemHandbookValue` proven changed;
- the quest is flagged by the item reward budget policy;
- the Success reward contains exactly one Item reward item;
- that item has a known positive handbook price;
- the current stack count is a finite integer greater than one;
- `Reward.Value` and `Upd.StackObjectsCount` are both present/finite and equal before mutation;
- the calculated budget target can be reached by lowering the existing stack count to an integer of at least one.

The transaction writes `Reward.Value` and `Upd.StackObjectsCount` together, verifies them through a synchronized read, and restores both on rollback. If the original fields disagree, the candidate is blocked/fails preflight instead of being repaired implicitly.

This slice does **not** replace `_tpl` item templates, add/remove reward records, delete the last item to satisfy a budget, mutate structural quest fields, bypass provenance protection, or enable generic item replacement logic.

## Provenance safety

- `PristineUnchanged`: never mutate.
- `ModAdded`: only policy-flagged supported dimensions may mutate.
- `PristineModified`: a dimension may mutate only when the corresponding pristine delta proves that dimension changed.
- unknown provenance: block.
- manual overrides do not bypass provenance safety.

For the opt-in item-stack slice, `ItemRewardStackCount` on `PristineModified` maps to the proven delta dimension `SuccessItemHandbookValue`.

## Numeric policy

Easy / Normal / Hard / Custom resolve concrete thresholds and target caps. Automatic normalization only reduces outliers; it never raises a normal reward. Manual quest reward overrides remain available for exact Experience/TraderStanding targets:

```json
"questRewardOverrides": {
  "QUEST_ID": {
    "allowAutomaticMutation": true,
    "experienceTarget": 3000,
    "traderStandingTarget": 0.03,
    "note": "optional"
  }
}
```

## Transaction contract

All active reward mutations share the production `NumericRewardTransactionCore`:

1. deterministic requests;
2. journal original values before the first write;
3. apply;
4. verify exact target;
5. rollback the whole batch on any failure;
6. verify rollback.

Experience, TraderStanding and the opt-in single-stack item quantity therefore participate in the same all-or-nothing batch. CI smoke tests include successful commits, same-state idempotence, synthetic failures, full rollback of earlier numeric mutations, synchronized item-stack commit, and mixed XP/standing/item rollback restoring both item quantity representations.

## SPT boundary and lean runtime path

Compile boundary: `SPTarkov.Server.Core 4.1.2` / .NET 10. Physical runtime target: **SPT 4.1.3**. Packaged candidates carry exact head/workflow identity in `BUILD_INFO.json`.

Load order remains:

1. priority `OnLoadOrder.Watermark + 1` — immutable pristine startup baseline;
2. normal SPT/mod callbacks;
3. `PostLoad + 1000` — final modded DB analysis and optional enforcement.

Primary audit, reward utility, progression, constraints and unified quest analysis now consume typed final DB state directly against the pristine snapshot. The old report correction/reparse/rewrite chain, primary parity shadow scan, composite candidate pass and target-envelope pass are no longer on the runtime path.

## Runtime outputs and validators

Runtime evidence schema **5** requires exactly seven core reports under the mod-local `reports/` directory:

1. `economy-admiral-audit.json`
2. `economy-admiral-reward-utility.json`
3. `economy-admiral-progression-graph.json`
4. `economy-admiral-quest-constraints.json`
5. `economy-admiral-quest-analysis.json`
6. `economy-admiral-provenance-delta.json`
7. `economy-admiral-enforcement-plan.json`

`economy-admiral-runtime-evidence.json` is the manifest over those seven reports.

Packaged candidates include only active runtime validators:

- `Validate-Runtime.ps1` — Audit/read-only contract;
- `Validate-Enforce.ps1` — Enforce mutation contract; recognizes Alpha schema 5/policy 3 and opt-in item-stack schema 6/policy 4.

The old primary parity shadow verifier and validator were retired after physical SPT 4.1.3 parity was proven. Their accepted evidence remains recorded in project history; they are not repeated on every server start or shipped as a misleading runtime command.

Physical runtime validation is reserved for meaningful SPT gates; ordinary code/contract changes should be proven through CI/smoke tests first.

## Scope boundaries

Economy Admiral does not currently expand into PBS, world loot, flea, crafts, insurance, Scorpion, Artem, Andrudis, generic attribution/replacement graphs, or Admiral Trader stock architecture. Those domains are separate future decisions and are not prerequisites for the current quest reward normalization line.

Development lifecycle: `single active branch/PR -> CI -> physical runtime gate only when genuinely required -> stabilize -> continue product work`.
