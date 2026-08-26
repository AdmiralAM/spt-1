# Admiral Trader

Official curated successor workstream for the legacy Andrudis/QuestManiac ecosystem.

## Product identity

- Mod name: **Admiral Trader**
- Trader name: **Admiral / Адмирал**
- Official trader portrait: **final and ingested** — the exact user-approved `1182x1330` bust portrait from the `Генерация иконок адмирала` workstream showing Admiral in a white naval tunic.
- Approved source SHA-256: `48508c7370bd0c98ed368049ff89a161282279a0ffa40a705e73f23d83a28aff`.
- Runtime portrait: technical-only `512x576` JPEG encoding, Git blob `0cd9db6776b246c08eb9ae0f1ac3e79c2a486966`, SHA-256 `701a79cb4e88053b0bb26492cf9c88b2d9276fa317975f0e1768ea43af5f8889`; substitution, regeneration and placeholder fallback are forbidden. The maintained contract is [`manifests/identity-assets.json`](manifests/identity-assets.json).
- Legacy Andrudis/QuestManiac names are provenance/source references only and are not the target product identity.

## Gameplay Alpha direction

Admiral is a **full specialist trader with an authored campaign and capability-broker functions**, not an empty quest-unlock terminal and not a preserved QuestManiac content dump.

His target role is **expeditionary procurement and specialist field logistics**. The player-facing loop is:

`trade -> build relationship -> take authored contracts -> earn milestones/capabilities -> expand specialist access`

The current 31 quests are a runtime/gameplay backbone, not a final quest-count target. Quest count has no artificial ceiling: expansion is quality-gated by authored purpose, objective variety, progression value and meaningful payoff.

## Current stock model

Gameplay Alpha uses three stock layers:

1. **Baseline/Core** — visible from first contact, finite and not quest-gated.
2. **Relationship** — specialist stock may use player level plus Admiral standing/loyalty where appropriate.
3. **Milestone/Capability** — high-value privileges may be explicitly quest-gated and remain finite/buy-limited.

The current materialized assort contains **11 offers**:

- **4 baseline offers** available without a quest gate;
- **7 milestone offers**: Laboratory access plus six controlled ammunition capability offers.

Baseline candidates are audited against pinned vanilla SPT 4.1.3, Scorpion and Artem references. Current pinned overlap validation reports zero direct baseline duplicates with Scorpion or Artem.

Special Weapons remains sample-only: its current Munitions milestone awards one green RSP-30 signal cartridge and does not create renewable heavy/explosive ammunition supply.

## Current campaign backbone

The current authored runtime set contains **31 quests**:

- 10 **Access Protocol** quests;
- 21 **Arsenal Protocol** quests across seven weapon families;
- current Arsenal backbone: Qualification -> Fieldwork -> Munitions;
- Qualification is a non-FIR, non-consumptive readiness/possession proof;
- Fieldwork is family-specific combat use;
- six normal Munitions tracks add capability-caliber field proof and a controlled ammunition milestone;
- Special Weapons ends in a one-unit RSP-30 sample and no permanent ammo offer.

This structure is explicitly **not** a future campaign cap. Strong Andrudis concepts, unique objectives, authored chains, presets and milestone rewards may add many more quests after passing the current quest-admission rules.

## Player-facing quest contract

Every quest must make **Why / What / Context / Payoff** understandable in EN/RU. Raw TPLs, condition IDs and opaque technical objective text are prohibited as final UI.

Gameplay Alpha now maintains:

- dedicated EN/RU locale text for every current finish-condition ID;
- quest-level Gameplay Alpha prose overrides where the original skeleton text did not match actual mechanics;
- explicit communication of capability unlocks and sample rewards;
- runtime-generated Admiral standing context derived from actual `TraderStanding` reward records;
- semantic regression checks linking objective mechanics, objective locales, item/standing rewards and `questassort` unlocks.

## Loyalty / standing

Standing represents relationship/status. It does **not** replace explicit milestone proof and sales-sum grind is forbidden.

Current loyalty thresholds are level + standing based, with no sales-sum requirement. Capability gates remain authored quest gates where declared; relationship stock may use loyalty in future slices.

## Economy Admiral boundary

Admiral Trader owns:

- authored quest/progression semantics;
- Baseline / Relationship / Milestone stock classification;
- quest and standing gates;
- finite stock/buy ceilings;
- sample/permanent capability semantics;
- ownership/provenance declarations.

**Economy Admiral** owns global source-pressure analysis, reward/price benchmarking and normalization, provenance/health checks and any future approved economy enforcement. Admiral Trader therefore keeps its data native-shaped and publishes explicit integration contracts instead of implementing a second global economy engine.

Gameplay Alpha changes that add baseline/relationship stock must remain compatible with Economy Admiral Issue #197; Economy Admiral must not infer that every Admiral offer is quest-gated or that renewable offer count is permanently seven.

## Andrudis curation

QuestManiac/Andrudis is source material, not a runtime dependency of the target product. The intended integration mode allows the legacy mod to be disabled while one Admiral replaces its useful authored value.

Preserve or re-author:

- strong authored concepts and narrative chains;
- unusual objectives;
- unique weapon/preset ideas;
- meaningful milestone rewards and specialist capabilities.

Remove or rewrite:

- literal and low-value vanilla duplicates;
- empty count-escalation ladders;
- excessive FIR/handover busywork;
- purposeless hideout chores;
- reward faucets.

Curation optimizes **quality density**, not minimum quest count.

## SPT 4.1.3 runtime safety

Admiral Trader is currently server-side and targets **SPT 4.1.3**. Source registration remains fail-closed through `runtime-manifest.json`.

The server/runtime validation layer checks trader identity, native lowercase `questassort` states (`started / success / fail`), quest/objective shapes, referenced item TPLs, assort contracts and locale coverage. The earlier physical PascalCase `questassort` startup failure is retained as a regression boundary.

The exact-runtime builder compiles against the user's real SPT 4.1.3 runtime assemblies and records source/runtime provenance before physical testing. CI is necessary but does not substitute for physical SPT runtime evidence.

The official portrait source has been physically recovered and ingested. Source metadata locks the approved `1182x1330` image by SHA-256, while the runtime package locks the technical `512x576` JPEG by Git blob identity and records its exact package SHA-256 in candidate provenance. `base.json` and runtime metadata use only `/files/trader/avatar/d5c27bb3169f8dfbc13f6b69.jpg`; placeholder/substitute portrait paths are not accepted.

## Key maintained contracts

- [`docs/gameplay-doctrine.md`](docs/gameplay-doctrine.md) — current gameplay purpose, stock layers, quest admission test and anti-goals.
- [`manifests/gameplay-policy.json`](manifests/gameplay-policy.json) — machine-readable gameplay invariants.
- [`manifests/identity-assets.json`](manifests/identity-assets.json) — official Admiral identity/portrait selection contract.
- [`manifests/baseline-stock.json`](manifests/baseline-stock.json) — baseline stock classification and overlap rationale.
- [`manifests/reward-policy.json`](manifests/reward-policy.json) — vanilla-benchmarked reward envelopes.
- [`docs/source-baseline.md`](docs/source-baseline.md) — pinned external reference authority.
- [`docs/migration-contract.md`](docs/migration-contract.md) — legacy migration/suppression safety boundary.
- [`docs/spt413-test-candidate.md`](docs/spt413-test-candidate.md) — exact physical runtime handoff/evidence contract.

## Validation

```bash
python -m unittest discover -s mods/Admiral-Trader/tests -p 'test_*.py'
```

Module CI additionally validates the pinned legacy corpus/graph, vanilla reward benchmark, current Access/Arsenal materialization, weapon/ammo pools, native SPT quest/assort contracts, Gameplay Alpha quest semantics, baseline stock overlap against pinned references, .NET 10 SPT 4.1.3 server compilation, package layout/provenance and fail-closed runtime boundaries.

Active Gameplay Alpha work is tracked by Issue #192 / Draft PR #193. Economy Admiral stock-class compatibility is tracked separately by Issue #197.
