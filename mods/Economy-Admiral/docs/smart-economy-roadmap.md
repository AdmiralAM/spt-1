# Economy Admiral smart-economy roadmap

Economy Admiral remains an optional module of **Admiral Suite**. Admiral Trader 0.3.0 is the stable campaign and storefront; Economy Admiral 0.1.0 stays in preview until the complete economic loop passes one combined gameplay acceptance. Each DLL remains independently removable and the Trader never depends on Economy.

The target is an economy that reacts to an item's real place in progression. It must preserve useful early choices, reward risky play, prevent easy arbitrage and avoid applying one percentage indiscriminately to every item or quest.

## E1 — acquisition graph and evidence

Build one deterministic startup snapshot for every item from the live SPT database:

- trader cash and barter offers, loyalty level, stock, reset limits and quest locks;
- crafts, hideout requirements, quest hand-ins and quest rewards;
- flea eligibility and handbook/reference value without treating either as guaranteed supply;
- loose and container loot sources, map availability and found-in-raid constraints;
- optional Admiral Trader, WTT Armory, WTT Content Backport, Icebreaker and Belt items when their runtime IDs exist.

Every classification records its source and confidence. Unknown or conflicting records stay unchanged and appear in diagnostics. Missing optional mods never fail startup.

Acceptance: identical databases produce byte-stable classifications; every mutation can name the evidence that authorized it; unsupported templates remain untouched.

## E2 — effective scarcity and player utility

Score supply and usefulness separately. Supply considers source count, loyalty stage, stock, reset cadence, barter/craft cost, map risk and replacement paths. Utility distinguishes:

- early essentials, medicines, ammunition and basic equipment;
- quest and hideout bottlenecks;
- weapons, parts, armor and ammunition tiers;
- containers and quality-of-life storage;
- valuables, collectibles and luxury equipment.

Level bands represent early, middle and late progression. A common low-level necessity must not receive the same pressure as a rare late-game luxury item merely because their handbook prices are similar.

Acceptance: maintained fixtures cover each category and stage; early essentials and required quest paths retain at least one practical acquisition route; rare items remain meaningfully valuable.

## E3 — targeted pressure engine

Replace broad surface multipliers with bounded target bands derived from acquisition and utility evidence:

- adjust only demonstrated outliers;
- preserve authored barter ingredients, loyalty levels, quest locks and finite stock identity;
- keep buy and sell changes internally consistent;
- detect cash, barter, craft and flea arbitrage loops before committing mutations;
- normalize quest rewards by difficulty, risk, duration and progression stage rather than by reward type alone;
- keep transactional rollback, idempotency and pristine-data protection.

The engine computes once during database startup. It adds no raid polling, client-frame work or repeated filesystem scans.

Acceptance: two applications yield the same database; failed validation restores the complete pre-change state; scenario tests prove that pressure is selective rather than a uniform cut.

## E4 — presets as economic outcomes

Easy, Normal and Hard define target outcomes instead of fixed global percentages:

- **Easy:** removes extreme money loops while keeping generous access;
- **Normal:** makes raid loot, barters, trader progression and purchases remain relevant together;
- **Hard:** increases scarcity and recovery time without blocking required progression.

Custom exposes bounded policy controls. Cluster switches remain hard gates. F12 reports effective behavior and the reason for a classification; it remains a settings client and never becomes a second economy engine.

Acceptance: the same scenario suite shows ordered but non-linear pressure from Easy to Hard, and disabling a cluster leaves that surface byte-identical.

## E5 — ecosystem adapters

Use optional, fail-closed adapters for known content:

- Admiral Trader: retain its finite stock, loyalty, quest unlock and authored reward semantics;
- WTT Armory and Content Backport: classify their actual templates and sources without blanket price or loot changes;
- Icebreaker: include its map sources only when the installed location and tables are valid;
- Belt: recognize owned container categories without copying or rewriting Belt behavior.

Adapters contain IDs and schema checks only. Economy Admiral gains no mandatory third-party dependency and does not control third-party spawns or inventories.

Acceptance: each supported mod is tested both present and absent; schema drift disables only its adapter and leaves the base economy operational.

## E6 — validation and promotion

Before stable promotion:

1. Run deterministic classification, mutation, rollback, idempotency and arbitrage scenarios.
2. Build against exact SPT 4.1.5 while retaining `~4.1.0` compatibility metadata.
3. Validate Trader-only, Economy-only and combined server startup.
4. Inspect a generated change report grouped by progression stage and category, including untouched and blocked records.
5. Perform one combined fresh-profile gameplay pass after the full mod set is ready: early survival, raid loot, trader buy/sell, barters, flea, quest rewards, middle-game recovery and bounded Easy/Hard sanity.
6. Fix concrete failures, repeat the same gate once, then promote Economy 0.1.0 from preview to stable.

Stable acceptance requires useful choices across progression, no forced grind bottleneck, no uncontrolled third-party mutations, green exact-head CI, a clean server smoke and an install-ready Admiral Suite artifact whose provenance identifies each component's release channel.

## Explicit boundaries

- No second trader or economy engine.
- No profile cleanup or persistent-ID migration.
- No continuous telemetry, raid polling or per-frame client logic.
- No universal price, reward or loot multiplier as the final model.
- No mandatory WTT, Icebreaker, Belt or other third-party dependency.
- No Economy stable claim before the combined gameplay gate.
