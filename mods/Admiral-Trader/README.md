# Admiral Trader

Official curated successor to the legacy Andrudis/QuestManiac ecosystem.

## Canonical authority

Admiral Trader now has **one active workstream**:

- canonical issue: **#192**;
- active Draft PR: **#328**;
- active branch: `feature/admiral-trader-canonical-milestones`;
- runtime target: **SPT 4.1.5**;
- historical frozen `0.1.0` reference: `053a62ff5f1cb545f13bc89a96bba3acd319a823`;
- historical frozen shape: **31 runtime quests / 11 finite offers**;
- QuestManiac/Andrudis research archive: **#115**.

PRs #193, #297 and #327 are superseded historical references. Do not resume product work on them and do not create parallel Trader implementation branches for work that belongs to the current milestone.

## Current P0 — quest lifecycle correctness

The unresolved physical defect is the native quest lifecycle, not merely quest visibility:

1. an eligible quest auto-enters the active lifecycle instead of waiting for explicit **Accept**;
2. after the objective is satisfied, the quest auto-resolves/turns in without explicit **Complete**;
3. the transition becomes visible after refreshing/reopening the trader/menu instead of through the expected button-driven flow;
4. the expected quest-success dialogue/chat path does not occur;
5. authored rewards do not materialize through the expected native success path.

Expected lifecycle:

`Offered -> explicit Accept -> Started -> progress -> AvailableForFinish -> explicit Complete -> Success -> success dialogue/mail -> reward delivery -> questassort unlock -> persistence`

The user physically repeated the defect with **AllQuestsCheckmarks disabled**. The problem remained, so that mod is ruled out as the root cause unless new contrary evidence appears.

The level-1 onboarding/HTTP diagnostics remain useful evidence but do not close this P0: HTTP visibility/acceptance is not proof of correct in-client manual Accept, manual Complete, success-message and reward behavior.

## Milestone order

### M1 — Lifecycle correctness
Fix the auto-accept / auto-turn-in / missing Complete / missing success-message-reward path. No campaign expansion while this is unresolved.

### M2 — Existing campaign acceptance
Prove the current 31-quest / 11-offer campaign as one coherent playable baseline with manual lifecycle, representative reward delivery, success message/mail, questassort unlock and restart persistence.

### M3 — Runtime campaign expansion
Materialize the already-prepared post-0.1.0 operation wave into actual runtime quests in bounded player-visible slices. Manifest-only research is not counted as product completion.

Current **design-only** M3 authority is kept on this canonical branch so the eventual runtime slice does not regress to the superseded PR #297 wording or count-driven structure:

- `docs/m3-quest-writing-standard.md` — Admiral voice, EN/RU policy, uniqueness gate, anti-filler policy and reward doctrine;
- `manifests/m3-campaign-product-spec.json` — curated 12-operation campaign shape, merge/hold decisions and runtime proof gates;
- `manifests/m3-campaign-editorial-copy.json` — authored EN/RU player-facing copy for every admitted operation;
- `manifests/m3-campaign-reward-plan.json` — conservative reward pacing and Economy Admiral handoff rules;
- `manifests/m3-reward-comparator-review.json` — historical vanilla reward sanity check; exact SPT 4.1.5 nearest-comparator review remains a runtime gate;
- `manifests/m3-campaign-uniqueness-review.json` — explicit conflict groups, operation fingerprints and merge triggers so cosmetic variants cannot re-enter the wave;
- `manifests/m3-operation-context-review.json` — map/target context selection and deliberate continuity/contrast between operations;
- `manifests/m3-campaign-progression.json` — non-linear level/prerequisite graph and narrative links between the four campaign acts;
- `manifests/m3-existing-campaign-integration.json` — justified prerequisite links from M3 into the accepted Access/Arsenal campaign, plus explicit non-dependencies to prevent artificial gating;
- `manifests/m3-deferred-concepts-review.json` — fail-closed disposition for medical, chemical, endurance, boss and cultist themes that are not yet strong or observable enough to admit;
- `tests/test_m3_campaign_product_spec.py` — deterministic cross-manifest validation for copy, rewards, uniqueness, contexts, progression, existing-campaign integration and the fail-closed M1/M2 boundary.

These files **do not authorize M3 runtime materialization before M1 and M2 acceptance**. They replace the old assumption that every prepared PR #297 operation must survive unchanged. Weak duplicates may be merged or held instead of being replaced with filler.

### M4 — Selective content absorption
Use QuestManiac/Andrudis/Natalya only as curated source material. No second trader, duplicate campaign or unnecessary dependency.

### M5 — Relationship / specialist storefront
Add bounded relationship progression and finite specialist stock after lifecycle/campaign foundations are stable. Economy Admiral remains owner of global economy normalization.

### M6 — Stable release
One exact-head install-ready SPT 4.1.5 candidate, one final batched physical gate, then deliberate promotion.

## Product constraints

- one NPC: Admiral / Адмирал;
- frozen trader ID: `d5c27bb3169f8dfbc13f6b69`;
- initial standing remains zero unless a later milestone explicitly changes the product contract;
- no wholesale QuestManiac port or legacy trader zoo;
- no repetitive filler/count ladders merely to increase quest count;
- preserve only distinct authored concepts with clear Why / What / Context / Payoff;
- finite progression-aware rewards and unlocks;
- EN/RU player-facing presentation;
- backup-first, ownership-scoped profile safety;
- no speculative destructive profile mutation;
- no second economy engine.

## Historical evidence

Nothing from the superseded workstreams is deleted. Exact heads, CI runs, artifacts, migration research, post-0.1.0 authored-operation work, relationship-stock work and compatibility findings remain valid references where applicable. They are evidence, not competing execution authority.

## Validation

Module validation remains under `mods/Admiral-Trader/tests` and associated deterministic tools/workflows. CI is evidence for a milestone, not a substitute for fixing the current P0 or for the required batched physical acceptance.
