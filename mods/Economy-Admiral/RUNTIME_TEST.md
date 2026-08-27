# Economy Admiral runtime testing

Physical SPT 4.1.3 tests are reserved for product gates that cannot be adequately proven in CI. Do not repeat runtime tests for documentation-only changes, source cleanup, parity already physically accepted, or ordinary transaction/planner regressions covered by automated smoke tests.

## Accepted Alpha evidence

The narrow Experience/TraderStanding Enforce Alpha is already physically accepted on SPT 4.1.3.

Accepted evidence includes:

- Audit / Normal: read-only DB fingerprint, concrete policy preview, `planned=88`, `applied=0`;
- Enforce / Normal: real Experience/TraderStanding mutations, `applied=88`, fingerprint changed, exact targets, pristine protection;
- Off: pipeline disabled;
- production transaction smoke: commit, rollback and same-state idempotence;
- primary audit: typed final DB + pristine startup parity physically proven (`final=5362`, `pristine=558`, `compared=5362`, `questRewardEdges=38176`).

The accepted parity proof is historical acceptance evidence. Its shadow service and standalone parity validator have been retired from the active runtime after the direct typed/pristine path replaced the old correction chain.

## Standard packaged validators

From the installed `Economy Admiral` folder:

```powershell
.\Validate-Runtime.ps1
.\Validate-Enforce.ps1
```

`Validate-Runtime.ps1` is for `mode=Audit`, requires runtime-evidence schema 5, all seven core reports, pristine provenance consistency, an unchanged DB fingerprint, and no committed mutations.

`Validate-Enforce.ps1` is for `mode=Enforce`. It recognizes:

- enforcement-plan schema 5 / mutation policy 3: accepted Alpha (`Experience`, `TraderStanding` only);
- enforcement-plan schema 6 / mutation policy 4: opt-in bounded single-stack item normalization in addition to numeric Alpha dimensions.

For schema 6 the validator is a strict product gate: at least one `ItemRewardStackCount` mutation must actually be applied. A schema-6 run with zero item-stack mutations is FAIL, even if XP/standing mutations succeed. On PASS it prints every applied item mutation as `QuestId | QuestName | Provenance | Before -> After`.

## Bounded item-stack runtime candidate

This is the only active post-Alpha physical gate.

Source/default configuration remains conservative (`Audit`, item-stack disabled). For the one physical candidate run, set exactly:

```json
"mode": "Enforce",
"preset": "Normal",
"enableItemRewardStackNormalization": true
```

No manual quest overrides are required.

The expected mutation class is intentionally narrow:

- existing `Success` Item reward stack only;
- quest provenance must be `ModAdded`, or `PristineModified` with `SuccessItemHandbookValue` proven changed;
- the automatically mutable reward shape must be unambiguous;
- known positive handbook price;
- finite integral current stack count > 1;
- whole Success-item bundle must exceed the Normal item budget after immutable reward value is reserved;
- target is an integer >= 1 and lower than current stack count;
- `_tpl`, reward records and structural quest fields are never replaced, added or removed;
- `Reward.Value` and `Upd.StackObjectsCount` change together and are transactionally rolled back together on failure.

The user's last physical SPT dataset contained 295 item-reward outlier flags, so the runtime environment has real item-reward candidates to evaluate. The physical gate itself decides which of those also satisfy provenance, reward-shape, price and reducibility requirements.

### One physical test

1. Install the single candidate artifact over `user/mods/Economy Admiral`.
2. Set the three configuration values above.
3. Start the SPT server once and let startup complete.
4. From the installed mod folder run exactly:

```powershell
.\Validate-Enforce.ps1
```

PASS requires all of the following in that same run:

- runtime evidence gate passed;
- DB fingerprint changed;
- transaction committed without rollback/error;
- pristine/unknown provenance protection held;
- exact mutation targets verified;
- **at least one real `ItemRewardStackCount` mutation applied**;
- validator prints the concrete changed quests and stack values.

The only result needed back is the final validator output. Do not perform a second Audit run, parity run, or unrelated economy test for this gate.

## Stop condition

Do not begin flea, world-loot, trader-price, craft, insurance, or other economy subsystems until the physical schema-6 run above passes with at least one concrete item reward stack change.
