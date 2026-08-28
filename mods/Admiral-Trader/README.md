# Admiral Trader

Official curated successor workstream for the legacy Andrudis/QuestManiac ecosystem.

## Product identity

- Mod name: **Admiral Trader**
- Trader name: **Admiral / Адмирал**
- Official trader portrait: **final and ingested** — the exact user-approved `1365x1536` source portrait supplied in the current Admiral Trader chat, showing Admiral in a white naval tunic.
- Approved source SHA-256: `2387fb3d6bc9b8a0ec677789959d7007f108744e72d7cf809ed945d459428cda`.
- Runtime portrait: technical-only `512x576` JPEG encoding, Git blob `63e158fbd96b595a609560dfef452451b4783144`; substitution, regeneration and placeholder fallback are forbidden. The maintained contract is [`manifests/identity-assets.json`](manifests/identity-assets.json).
- Legacy Andrudis/QuestManiac names are provenance/source references only and are not the target product identity.

## Gameplay Alpha direction

Admiral is a **full specialist trader with an authored campaign and capability-broker functions**, not an empty quest-unlock terminal and not a preserved QuestManiac content dump.

His target role is **expeditionary procurement and specialist field logistics**. The player-facing loop is:

`trade -> build relationship -> take authored contracts -> earn milestones/capabilities -> expand specialist access`

The current 31 quests are a runtime/gameplay backbone, not a final quest-count target. Quest count has no artificial ceiling outside the currently frozen 31-quest package: later expansion requires a separate controller decision.

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
- current committed Qualification templates still use non-FIR `FindItem` readiness proof and are a known Beta-stabilization defect because pre-existing stash/equipped weapons do not satisfy that semantic reliably for the intended player-facing contract;
- Fieldwork is family-specific combat use;
- six normal Munitions tracks add capability-caliber field proof and a controlled ammunition milestone;
- Special Weapons ends in a one-unit RSP-30 sample and no permanent ammo offer.

The qualification defect is explicitly not accepted as final behavior. It must be replaced by a native, deterministic, non-consumptive objective whose UI text exactly describes what the game counts before release-candidate activation.

## Player-facing quest contract

Every quest must make **Why / What / Context / Payoff** understandable in EN/RU. Raw TPLs, condition IDs and opaque technical objective text are prohibited as final UI.

Gameplay Alpha maintains:

- dedicated EN/RU locale text for every current finish-condition ID;
- quest-level Gameplay Alpha prose overrides where the original skeleton text did not match actual mechanics;
- explicit communication of capability unlocks and sample rewards;
- runtime-generated Admiral standing context derived from actual `TraderStanding` reward records;
- semantic regression checks linking objective mechanics, objective locales, item/standing rewards and `questassort` unlocks.

Observed runtime evidence has already shown why text cannot be treated as proof of mechanics: the first access quest required a canonical stash-aware handover fix before it became completable. Weapon Qualification remains under the same rule: mechanics first, text second.

## Loyalty / standing

Standing represents relationship/status. It does **not** replace explicit milestone proof and sales-sum grind is forbidden.

Current loyalty thresholds are level + standing based, with no sales-sum requirement. New-profile template registration pins Admiral initial standing to `0`. Existing contaminated test profiles are handled only through the packaged backup-first, ownership-scoped recovery tool.

## Economy Admiral boundary

Admiral Trader owns authored quest/progression semantics, stock classification, quest/standing gates, finite stock/buy ceilings, sample/permanent capability semantics and ownership/provenance declarations. **Economy Admiral** owns global source-pressure analysis, reward/price benchmarking and normalization, provenance/health checks and approved economy enforcement.

## Andrudis curation

QuestManiac/Andrudis is source material, not a runtime dependency of the target product. Preserve strong authored concepts and unusual objectives; remove literal vanilla duplicates, empty count-escalation ladders, excessive FIR/handover busywork, purposeless hideout chores and reward faucets.

## SPT 4.1.3 runtime safety

Admiral Trader is server-side and targets **SPT 4.1.3**. Source registration remains fail-closed through `runtime-manifest.json`.

The validation layer checks frozen trader identity, native lowercase `questassort` states (`started / success / fail`), quest/objective shapes, referenced item TPLs, assort contracts, locale coverage, persistent identity ownership and backup-first profile recovery. Exact-runtime packaging compiles against the user's real SPT 4.1.3 assemblies and records source/runtime provenance; published API CI is necessary but is not physical runtime evidence.

Connector-authored commits now have an independent exact-head validation path: a push dispatcher launches all default-branch registered Trader workflows, while feature-only profile/preflight gates run directly on the pushed feature head. This avoids relying exclusively on `pull_request/synchronize`.

## Key maintained contracts

- [`docs/gameplay-doctrine.md`](docs/gameplay-doctrine.md)
- [`manifests/gameplay-policy.json`](manifests/gameplay-policy.json)
- [`manifests/identity-assets.json`](manifests/identity-assets.json)
- [`manifests/persistent-identities.json`](manifests/persistent-identities.json)
- [`manifests/baseline-stock.json`](manifests/baseline-stock.json)
- [`manifests/reward-policy.json`](manifests/reward-policy.json)
- [`docs/source-baseline.md`](docs/source-baseline.md)
- [`docs/migration-contract.md`](docs/migration-contract.md)
- [`docs/spt413-test-candidate.md`](docs/spt413-test-candidate.md)

## Validation

```bash
python -m unittest discover -s mods/Admiral-Trader/tests -p 'test_*.py'
```

Active work is tracked by Issue #192 / Draft PR #193. The registry, not this README, owns phase progression and runtime-gate activation.
