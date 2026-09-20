# Admiral Trader

Official curated successor to the legacy Andrudis/QuestManiac ecosystem.

## Canonical authority

Admiral Trader has **one active workstream**:

- canonical issue: **#192**;
- current development/validation baseline: **SPT 4.1.5**;
- runtime metadata compatibility range: **`~4.1.0`**;
- stable gameplay baseline: **`0.2.0`**;
- stable campaign release: **`0.3.0`**, 172 core quests / 82 finite offers, plus 10 optional Icebreaker quests when its verified runtime is present;
- historical frozen `0.1.0`: `053a62ff5f1cb545f13bc89a96bba3acd319a823`, 31 runtime quests / 11 finite offers;
- QuestManiac/Andrudis research archive: **#115**.

PRs #193, #297, #327 and #328 are historical evidence only. Continue implementation only in the single live Admiral Trader PR discovered from GitHub; do not create a parallel Trader branch.

## SPT compatibility policy

SPT version matters for development, API compatibility and release evidence, but Admiral Trader is **not hard-pinned to one exact SPT patch through server metadata**.

- Current development/validation baseline: **SPT 4.1.5**.
- Runtime metadata: **`~4.1.0`**, allowing compatible later `4.1.x` patches.
- Exact-version builds/tests are reproducibility evidence, not an automatic runtime refusal policy.
- Campaign sizing and concurrent-load policy: `docs/campaign-portfolio-plan.md`.
- Optional-mod candidate and fallback policy: `docs/optional-content-candidates.md`.
- Narrow the supported range only after a demonstrated API/data incompatibility.

## Stable baseline — 0.2.0

M1 through M6 are closed for the first playable campaign baseline. Physical validation confirmed quest visibility, explicit acceptance and completion, reward delivery and the expanded storefront. The final stabilization amendment stages two weapon tracks, keeps complete requirements in readable multiline descriptions, expands the finite core using the measured catalog gap left by retiring Andrudis, and ships armored offers as complete native SPT 4.1.5 presets.

Expected lifecycle:

`Offered -> explicit Accept -> Started -> progress -> AvailableForFinish -> explicit Complete -> Success -> success dialogue/mail -> reward delivery -> questassort unlock -> persistence`

## Milestone order

- **M1 — Lifecycle correctness**
- **M2 — Existing 31-quest / 11-offer campaign acceptance**
- **M3 — Runtime campaign expansion**
- **M4 — Selective external-content absorption**
- **M5 — Relationship / specialist storefront**
- **M6 — Playable 0.2.0 baseline hardening**
- **M7 — Full campaign, retired-source and optional-content absorption — closed in `0.3.0`**
- **M8 — Admiral insurance service — closed**: native SPT insurance with relationship-scaled prices, 90% return chance, a fast 6–12 hour return window, 120-hour storage and authored EN/RU messages; the existing trader identity owns every return.
- **M9 — Editorial, technical and balance audit — closed in `0.3.0`**
- **Final gate — coherent fresh-profile acceptance and stable release**

Current runtime shape: **172 core quests** (72 validated foundation/rotation quests plus 100 authored story quests across ten map chains), **82 finite offers** (37 core/relationship/milestone offers, 10 story-finale unlocks and 35 Natalya weapon presets). Eighteen story beats use Natalya as a specialist inside Admiral's campaign; no second trader or external Natalya dependency is created. Verified Icebreaker 1.1.0 installations conditionally add the separate ten-operation **Boreas Protocol** chain, bringing the runtime total to 182 without changing the core graph.

WTT Artem is now embedded directly in Admiral Trader: all 23 quests, 281 offers / 703 assort rows, 41 unlocks, 64 suits, item definitions, quest zones, images, locales and 262 bundles load from the Admiral package. No `WTT-Artem.dll`, `Admiral Artem Content Provider.dll`, or active Artem mod folder is required. Painter/TGC content remains consolidated under Admiral as before, giving a combined 35 quests, 402 finite offer roots / 946 rows, 44 unlocks and 68 suits. Existing persistent IDs remain unchanged. The retired Artem trader ID is created only as a temporary migration shell and removed after every loaded profile has been migrated and saved. See `docs/painter-artem-consolidation-audit.md` for the measured contracts and migration behavior.

Imported storefront presentation is fail-visible and complete. Sold embedded Artem templates require non-empty Russian names/descriptions and verified bundle entries; all five Painter prefabs are declared explicitly; optional TGC items retain their original English authored copy as a Russian-client fallback only when TGC provides no Russian field. Snacky-Z is identified as a 2x1 pouch with a 5x5 internal grid, and both Snacky-Z and Painter's Special Delivery use finite thematic barters rather than unrestricted cash purchase.

M7 content implementation is closed: the complete campaign, Natalya absorption, optional integrations, storefront and progressive reward layers are present in runtime. The release remains an RC until the later coherent fresh-profile review has exercised progression, pacing, rewards, unlocks and storefront tiers. Fixes from that review must preserve every persistent identity already in use.

The active weapon campaign uses two synchronized 20-step lanes. At every step the player can choose between contrasting roles: sidearms, SMGs, shotguns and support weapons on one side; carbines, assault rifles, battle rifles, 9x39 systems, marksman rifles and bolt-actions on the other. The runtime family matrix covers 143 native weapon templates, progresses from common starter weapons to rare specialist platforms, avoids model-to-single-map locks, and accepts 50 verified WTT Armory/Content Backport alternatives when installed. WTT remains optional: every objective has a complete native weapon pool and no WTT quest gates the campaign.

Every one of the 172 core quests now has XP, Admiral standing, at least ₽10,000 and a useful item, conditional item or permanent store unlock. Arsenal contributes 30 staged field-support items and ten complete configured weapons, one at every fourth step in both lanes. The wider campaign retains 20 native field-support trades, four early complete weapons, eleven native tactical rewards, ten Natalya signature presets and 32 conditional B&A&HB wrist/belt/head-band/container trades. Early container rewards remain front-loaded. Each item replaces a bounded part of the quest's rouble payout instead of stacking free value; optional rewards leave the corresponding cash untouched when their template is absent.

The opening story keeps five focused Ground Zero operations, then follows the early vanilla map cadence through Customs, Woods, Factory and Interchange before closing its first circuit on Customs. These operations remain one persistent chain, but their client location metadata and native objective conditions now name the actual map so Dynamic Maps and the quest list can group them correctly.

Admiral uses SPT's native insurance purchase, raid-loss, return scheduling and mail delivery. His defining advantage is the fast 6–12 hour return window; the return chance is 90%, while the price coefficient improves from 25% at LL1 to 16% at LL4. Labs and Labyrinth retain their native no-return behavior; no profile migration or parallel recovery store is introduced.

Install, replacement and clean-removal instructions are in `docs/INSTALL.md`. The canonical runtime directory is `Admiral Trader`, matching the established installation. Replacement tooling removes the obsolete `Admiral-Trader` alias so two copies of the same persistent trader identity cannot load together.

Relationship progression uses level, reputation and cumulative trade turnover like native traders. LL2 requires level 15, 0.10 standing and ₽500,000 turnover; LL3 requires level 25, 0.30 and ₽1,200,000; LL4 requires level 35, 0.55 and ₽2,200,000. White, yellow and blue signalling flares provide finite specialist availability at those tiers. The existing MS2000 marker offer keeps its ID and ₽16,500 price while requester-local stock/buy limits rise from 12/4 at LL1 to 16/6, 20/8 and 24/10. The projection modifies only SPT's profile-scoped assort response; it never mutates the global trader table.

Routine field stock remains available for roubles. Six specialist offers use authored item barters so valuable raid loot retains a purpose: RAPTAR, injector case, military battery, FLIR and the two quest-gated Labs access-card offers. Their loyalty, quest unlock, stock and purchase limits remain unchanged.

The original bounded M4 decision is recorded in `manifests/m4-selective-content-absorption.json`. M7 supersedes its old content boundary: Natalya now appears in 18 story beats and contributes all 35 compatible native weapon presets, while selected Andrudis capability and hunt themes are distributed across the ten story chains and Arsenal lanes. The retired traders, source quest IDs, repetitive count ladders, custom Natalya items/zones and unsafe armour presets remain excluded.

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

M5 is materialized in native assort data and the existing Trader DLL. The subsequent stabilization amendment changes prerequisites, objective-row copy and the bounded core assortment in response to physical review; it adds no quest, mechanic or persistent-identity replacement. Relationship offers use Admiral's native loyalty levels, remain finite, have no quest gates, and cannot replace Access or Arsenal capability unlocks. The historical frozen `0.1.0` commit and its 31-quest / 11-offer package remain unchanged.
