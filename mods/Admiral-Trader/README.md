# Admiral Trader

Official curated successor workstream for the legacy Andrudis/QuestManiac ecosystem.

## Product identity

- Mod name: **Admiral Trader**
- Trader name: **Admiral / Адмирал**
- Official trader portrait: **final and ingested** — the exact user-approved `1365x1536` source portrait supplied in the current Admiral Trader chat, showing Admiral in a white naval tunic.
- Approved source SHA-256: `2387fb3d6bc9b8a0ec677789959d7007f108744e72d7cf809ed945d459428cda`.
- Runtime portrait: technical-only `512x576` JPEG encoding, Git blob `63e158fbd96b595a609560dfef452451b4783144`; substitution, regeneration and placeholder fallback are forbidden. The maintained contract is [`manifests/identity-assets.json`](manifests/identity-assets.json).
- Legacy Andrudis/QuestManiac names are provenance/source references only and are not the target product identity.

## Gameplay direction

Admiral is a **full specialist trader with an authored campaign and capability-broker functions**, not an empty quest-unlock terminal and not a preserved QuestManiac content dump.

His target role is **expeditionary procurement and specialist field logistics**. The player-facing loop is:

`trade -> build relationship -> take authored contracts -> earn milestones/capabilities -> expand specialist access`

The current frozen package contains 31 quests. Later campaign expansion is outside this active package and requires an explicit product decision.

## Current stock model

The current package uses three stock layers:

1. **Baseline/Core** — visible from first contact, finite and not quest-gated.
2. **Relationship** — reserved design space; not materialized in the current frozen 11-offer package.
3. **Milestone/Capability** — high-value privileges explicitly quest-gated and finite/buy-limited.

The current materialized assort contains **11 offers**:

- **4 baseline offers** available without a quest gate;
- **7 milestone offers**: Laboratory access plus six controlled ammunition capability offers.

Baseline candidates are audited against pinned vanilla SPT 4.1.3, Scorpion and Artem references. Current pinned overlap validation reports zero direct baseline duplicates with Scorpion or Artem.

Special Weapons remains sample-only: its current Munitions milestone awards one green RSP-30 signal cartridge and does not create renewable heavy/explosive ammunition supply.

## Current campaign backbone

The current authored runtime set contains **31 quests**:

- 10 **Access Protocol** quests;
- 21 **Arsenal Protocol** quests across seven weapon families;
- Arsenal backbone: Qualification -> Fieldwork -> Munitions;
- all seven Qualification quests use a native non-consumptive `CounterCreator/Kills` proof requiring one elimination with an approved family weapon;
- Fieldwork requires sustained family-specific combat use;
- six normal Munitions tracks add capability-caliber field proof and a controlled ammunition milestone;
- Special Weapons ends in a one-unit RSP-30 sample and no permanent ammo offer.

Qualification no longer uses acquisition-based `FindItem`; compiler output, committed runtime JSON and objective localization are kept in parity by CI.

## Player-facing quest contract

Every quest must make **Why / What / Context / Payoff** understandable in EN/RU. Raw TPLs, condition IDs and opaque technical objective text are prohibited as final UI.

The maintained runtime provides:

- dedicated EN/RU locale text for every current finish-condition ID;
- quest-level prose overrides where skeleton text did not match actual mechanics;
- explicit communication of capability unlocks and sample rewards;
- runtime-generated Admiral standing context derived from actual `TraderStanding` reward records;
- concrete installed-locale item names for Access objectives when available;
- truthful fallback text for native `FindItem`: acquisition occurs after acceptance, pre-existing stash copies do not count, FIR is not required and items are not consumed;
- semantic regression checks linking objective mechanics, objective locales, item/standing rewards and `questassort` unlocks.

Observed runtime evidence has already shown why text cannot be treated as proof of mechanics: the first access quest required the canonical stash-aware handover fix before it became completable. Remaining reward/mail and delayed UI-refresh behavior belongs to the final batched physical runtime proof rather than speculative JSON changes.

## Loyalty / standing

Standing represents relationship/status. It does **not** replace explicit milestone proof and sales-sum grind is forbidden.

Current loyalty thresholds are level + standing based, with no sales-sum requirement. New-profile template registration pins Admiral initial standing to `0`. Existing contaminated test profiles are handled only through the packaged backup-first, ownership-scoped recovery tool.

Insurance is not part of the current `0.1.0` release. The runtime validates that contract before registration and reasserts it after normal mod loading so broad service mutators cannot expose an incomplete Admiral insurer in the pre-raid screen. A future Admiral insurance feature remains a separate product phase; it is not cancelled or partially exposed by this candidate.

## Economy Admiral boundary

Admiral Trader owns authored quest/progression semantics, stock classification, quest/standing gates, finite stock/buy ceilings, sample/permanent capability semantics and ownership/provenance declarations. **Economy Admiral** owns global source-pressure analysis, reward/price benchmarking and normalization, provenance/health checks and approved economy enforcement.

## Andrudis curation

QuestManiac/Andrudis is source material, not a runtime dependency of the target product. Preserve strong authored concepts and unusual objectives; remove literal vanilla duplicates, empty count-escalation ladders, excessive FIR/handover busywork, purposeless hideout chores and reward faucets.

## SPT 4.1.3 runtime safety

Admiral Trader is server-side and targets **SPT 4.1.3**. Source registration remains fail-closed through `runtime-manifest.json`.

The validation layer checks frozen trader identity, native lowercase `questassort` states (`started / success / fail`), quest/objective shapes, referenced item TPLs, assort contracts, locale coverage, persistent identity ownership and backup-first profile recovery. Exact-runtime packaging compiles against the real SPT 4.1.3 assemblies supplied to the maintained build path and records source/runtime provenance; published API CI is necessary but is not physical runtime evidence.

Connector-authored commits have an independent exact-head validation path: a push dispatcher launches default-branch registered Trader workflows, while feature-only profile/preflight gates run directly on the pushed feature head. This avoids relying exclusively on `pull_request/synchronize`.

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

Active work is tracked by Issue #192 / Draft PR #193. `origin/main:AGENTS.md` and `origin/main:.github/workstreams.json` own phase progression and runtime-gate authority.
