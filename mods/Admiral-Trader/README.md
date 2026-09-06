# Admiral Trader

Official curated successor to the legacy Andrudis/QuestManiac ecosystem.

## Canonical authority

Admiral Trader has **one active workstream**:

- canonical issue: **#192**;
- active Draft PR: **#328**;
- active branch: `feature/admiral-trader-canonical-milestones`;
- current development/validation baseline: **SPT 4.1.5**;
- runtime metadata compatibility range: **`~4.1.0`**;
- historical frozen `0.1.0`: `053a62ff5f1cb545f13bc89a96bba3acd319a823`, 31 runtime quests / 11 finite offers;
- QuestManiac/Andrudis research archive: **#115**.

PRs #193, #297 and #327 are historical evidence only. Do not resume product work on them or create parallel Trader implementation branches for work that belongs to #328.

## SPT compatibility policy

SPT version matters for development, API compatibility and release evidence, but Admiral Trader is **not hard-pinned to one exact SPT patch through server metadata**.

- Current development/validation baseline: **SPT 4.1.5**.
- Runtime metadata: **`~4.1.0`**, allowing compatible later `4.1.x` patches.
- Exact-version builds/tests are reproducibility evidence, not an automatic runtime refusal policy.
- Narrow the supported range only after a demonstrated API/data incompatibility.

## Current milestone — M1 lifecycle correctness

The unresolved physical defect is the native quest lifecycle:

1. eligible quest can enter the active lifecycle without explicit **Accept**;
2. objective completion can resolve/turn in without explicit **Complete**;
3. state transitions may appear only after trader/menu refresh;
4. expected success dialogue/chat is missing;
5. authored reward is not delivered through the expected native success path.

Expected lifecycle:

`Offered -> explicit Accept -> Started -> progress -> AvailableForFinish -> explicit Complete -> Success -> success dialogue/mail -> reward delivery -> questassort unlock -> persistence`

**AllQuestsCheckmarks has been physically ruled out as the root cause unless new contrary evidence appears.**

## Milestone order

- **M1 — Lifecycle correctness**
- **M2 — Existing 31-quest / 11-offer campaign acceptance**
- **M3 — Runtime campaign expansion**
- **M4 — Selective external-content absorption**
- **M5 — Relationship / specialist storefront**
- **M6 — Stable release**

Do not expand runtime scope while M1 is unresolved.

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

Final design envelope: **106,500 XP / 661,000 RUB / +0.157 standing**, with no selected item rewards or permanent unlocks in this M3 slice.

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

The completed design authority does **not** authorize new runtime implementation by itself.

Functional work beyond the currently approved scope remains gated by M1/M2 and explicit user direction where required. CI and exact-version evidence do not replace the required meaningful physical acceptance gate.
