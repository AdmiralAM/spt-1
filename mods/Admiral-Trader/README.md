# Admiral Trader

Official curated successor to the legacy Andrudis/QuestManiac ecosystem.

## Canonical authority

Admiral Trader has **one active workstream**:

- canonical issue: **#192**;
- active Draft PR: **#328**;
- active branch: `feature/admiral-trader-canonical-milestones`;
- current development/validation baseline: **SPT 4.1.5**;
- runtime metadata compatibility range: **`~4.1.0`**;
- active release-candidate version: **`0.1.0+milestones`**;
- historical frozen `0.1.0`: `053a62ff5f1cb545f13bc89a96bba3acd319a823`, 31 runtime quests / 11 finite offers;
- QuestManiac/Andrudis research archive: **#115**.

PRs #193, #297 and #327 are historical evidence only. Do not resume product work on them or create parallel Trader implementation branches for work that belongs to #328.

## SPT compatibility policy

SPT version matters for development, API compatibility and release evidence, but Admiral Trader is **not hard-pinned to one exact SPT patch through server metadata**.

- Current development/validation baseline: **SPT 4.1.5**.
- Runtime metadata: **`~4.1.0`**, allowing compatible later `4.1.x` patches.
- Exact-version builds/tests are reproducibility evidence, not an automatic runtime refusal policy.
- Campaign sizing and concurrent-load policy: `docs/campaign-portfolio-plan.md`.
- Narrow the supported range only after a demonstrated API/data incompatibility.

## Current milestone — M6 stable-release hardening

M1 lifecycle and the combined M2/M3 fresh-profile runtime pass are accepted. M4 selective absorption and M5 relationship/storefront implementation are closed. M6 freezes the runtime at 43 quests and 15 finite offers while installation, upgrade, removal, package provenance, profile safety and exact-runtime integration are hardened for one final physical release gate.

Expected lifecycle:

`Offered -> explicit Accept -> Started -> progress -> AvailableForFinish -> explicit Complete -> Success -> success dialogue/mail -> reward delivery -> questassort unlock -> persistence`

## Milestone order

- **M1 — Lifecycle correctness**
- **M2 — Existing 31-quest / 11-offer campaign acceptance**
- **M3 — Runtime campaign expansion**
- **M4 — Selective external-content absorption**
- **M5 — Relationship / specialist storefront**
- **M6 — Stable release**

Current runtime shape: **43 quests** (31 frozen baseline + 12 M3 operations), **15 offers** (4 Baseline + 3 Relationship + 8 Milestone).

Install, replacement and clean-removal instructions are in `docs/INSTALL.md`. Always remove both known folder spellings before installing the canonical `Admiral-Trader` directory; this prevents two copies of the same persistent trader identity from loading together.

Relationship progression uses the existing Admiral loyalty thresholds: LL2 requires level 15 and 0.10 standing, LL3 requires level 25 and 0.30, and LL4 requires level 35 and 0.55. White, yellow and blue signalling flares provide finite specialist availability at those tiers. The existing MS2000 marker offer keeps its ID and ₽16,500 price while requester-local stock/buy limits rise from 12/4 at LL1 to 16/6, 20/8 and 24/10. The projection modifies only SPT's profile-scoped assort response; it never mutates the global trader table.

The bounded M4 decision is recorded in `manifests/m4-selective-content-absorption.json`. Natalya's Exfil route and logistics ideas and the selected Andrudis capability themes already have explicit runtime homes in the 12 M3 operations. The rejected Pay Back, Weapons Training, boss and cultist copies do not add distinct player decisions, so M4 adds no parallel quest records and leaves the validated graph and balance intact.

## Product-design scope — complete

The finite pre-implementation product-design scope is **closed**.

Final authority:

- human-readable: `docs/product-design-authority.md`;
- machine-readable: `manifests/product-design-final.json`.

These two files define the final design outcome:

- **12 operations across four acts**;
- final `KEEP / REWRITE / MERGE / DROP` disposition of the original 15-operation wave;
- final EN/RU voice and editorial policy;
- reward envelope and progression model;
- selective absorption map for Natalya, Andrudis/QuestManiac, Admiral Artyom Revival, Scorpion C#, Ref Friendly Quests C# and legacy acidphantasm sources;
- closed/deferred concepts and explicit implementation boundary.

The detailed `m3-*` manifests remain supporting evidence and implementation detail. They are **not parallel design authorities** and should not create further design churn by default.

No further product-design expansion is expected unless the user changes direction, a demonstrated runtime constraint invalidates a final choice, or a new source materially improves one specific operation.

## Final M3 shape

### Act I — Establish the Network / Развернуть сеть

Acoustic Discipline, Forward Reserve, Low Profile, Mobility Doctrine, Borrowed Access.

### Act II — Keep the Routes Open / Удержать маршруты

Acoustic Contact, Route Security, Contractor Intercept.

### Act III — Deny the Threat / Лишить противника инициативы

Observation Window, Heavy Assault, Break the Perimeter.

### Act IV — Operate Without Support / Работать автономно

Internal Security.

Final design envelope after the bounded campaign-audit correction: **133,000 XP / 752,000 RUB / +0.179 standing**, with no selected item rewards or permanent unlocks in this M3 slice.

## Reference-first engineering

Admiral is a consolidation/adaptation product, not a greenfield trader framework.

Required implementation references include:

- `mods/Admiral-Artyom-Revival`;
- `Colobos9mm/Natalya`;
- `laurentmekka/AndrudisQuestManiac`;
- `acidphantasm/scorpion-csharp`;
- `acidphantasm/acidphantasm-scorpion`;
- `acidphantasm/acidphantasm-refchanges`;
- `acidphantasm/reffriendlyquests-csharp`.

Prefer native SPT behavior and maintained C# patterns. Reuse proven organization, registration, quest, assort and localization patterns where appropriate. Do not copy obsolete dependencies, legacy defects or unrelated machinery merely for fidelity.

## Product constraints

- one NPC: Admiral / Адмирал;
- trader ID: `d5c27bb3169f8dfbc13f6b69`;
- no wholesale QuestManiac port or legacy trader zoo;
- no repetitive filler/count ladders;
- preserve only distinct authored concepts with clear Why / What / Context / Payoff;
- EN/RU player-facing presentation is authored, not literal translation;
- finite progression-aware rewards and unlocks;
- Economy Admiral remains owner of global economy normalization;
- no speculative destructive profile mutation.

## Implementation boundary

M5 is materialized in native assort data and the existing Trader DLL. M6 changes no quest, offer, mechanic or persistent ID. Relationship offers use Admiral's native loyalty levels, remain finite, have no quest gates, and cannot replace Access or Arsenal capability unlocks. The historical frozen `0.1.0` commit and its 31-quest / 11-offer package remain unchanged.
