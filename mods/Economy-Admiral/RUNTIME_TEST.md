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

`Validate-Enforce.ps1` is for `mode=Enforce`. It also requires the seven-report schema-5 runtime manifest and recognizes:

- enforcement-plan schema 5 / mutation policy 3: accepted Alpha (`Experience`, `TraderStanding` only);
- enforcement-plan schema 6 / mutation policy 4: opt-in bounded single-stack item normalization in addition to numeric Alpha dimensions.

Do not re-request primary parity for unrelated changes. The physical parity gate already served its purpose and is preserved in project history rather than re-run on every server start.

## Opt-in bounded item-stack gate

The config switch is deliberately false by default:

```json
"enableItemRewardStackNormalization": false
```

Before any future physical promotion of this slice, CI must already prove:

- SPT server build with writable `Reward.Value` and `Upd.StackObjectsCount`;
- only one existing Success Item reward item is eligible;
- known positive handbook price;
- finite integral stack count > 1;
- budget target floors to an integer >= 1;
- normal stacks are no-ops;
- cases requiring deletion/template replacement are blocked;
- `Reward.Value == Upd.StackObjectsCount` before item mutation;
- both quantity representations change together;
- transaction verification re-checks synchronization;
- mixed XP/standing/item failure rolls the whole batch back and restores both item quantity representations;
- PristineUnchanged and unknown provenance remain protected;
- PristineModified item mutation requires `SuccessItemHandbookValue` in its changed dimensions;
- `Validate-Enforce.ps1` accepts a valid item-stack fixture and rejects an unproven modified-pristine fixture.

Only after all of that is green does a physical SPT test become meaningful. A physical item-stack test, if eventually required, should be a single candidate run proving a real eligible mod-added stack changes `Before -> Target -> After` while the quest reward remains internally synchronized and all protected content remains untouched.

## Procedure when a physical gate is actually requested

The test request must always provide:

1. exact 40-character candidate SHA;
2. exact workflow run and artifact identity;
3. exact install target;
4. exact config keys to change;
5. exact server/validator commands;
6. minimal output that must be returned.

Do not ask the user to infer what to test.
