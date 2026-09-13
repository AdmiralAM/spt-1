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

Equivalent-item groups describe practical substitutes: medicines with the same role, usable ammunition tiers, comparable armor, storage, backpacks and weapons with complete support. Scarcity is evaluated against the group as well as the exact template, so one expensive model does not trigger protection when a reasonable replacement exists.

A deterministic progression-path simulation checks representative early and middle stages without assuming access to the Flea Market, rare keys or a lucky single-source drop. It blocks a planned mutation when that change would remove the last practical route to a required quest hand-in, hideout prerequisite or viable basic loadout. This is a hard dead-end safeguard, not dynamic newcomer assistance.

Weapons are evaluated as usable systems rather than bare receivers. Their effective cost includes required magazines, available ammunition and installed parts. Presets and assembled quest rewards use the value and availability of the complete build, while individual parts retain their own evidence.

Weapon and ammunition availability is paired. An accessible weapon must have at least one viable magazine and ammunition route at the same progression stage; premium ammunition may remain limited by loyalty, stock, barter or quest unlock.

Economy Admiral may consume read-only valuation evidence exposed by the maintained **Item Intelligence Admiral** and **Item Valuation MOD SPT** modules. It does not copy their pricing, slot-efficiency or presentation engines, write their configuration, or require either module. Missing or incompatible evidence simply removes that signal from the confidence score.

Acceptance: maintained fixtures cover each category and stage; required progression paths retain at least one practical acquisition route; equivalent groups and usable weapon/ammunition pairs exist at their authored stage; optional valuation evidence is proven present/absent; rare items remain meaningfully valuable.

## E3 — targeted pressure engine

Replace broad surface multipliers with bounded target bands derived from acquisition and utility evidence:

- adjust only demonstrated outliers;
- preserve authored barter ingredients, loyalty levels, quest locks and finite stock identity;
- keep buy and sell changes internally consistent;
- normalize quest rewards by difficulty, risk, duration and progression stage rather than by reward type alone;
- audit trader offers for unusable barters, dominated purchases, underpriced assembled builds, unlimited rare supply and offers with no rational use;
- use stock quantity and restock cadence as bounded pressure tools instead of solving every imbalance through price;
- reduce oversupply only when multiple renewable sources prove it, leaving items with uncertain or narrow supply unchanged;
- price repair and durability loss so repairing ordinary equipment remains useful while badly damaged premium equipment creates a real replace-or-repair decision;
- balance the complete money flow across raid sales, quests, traders, flea fees, barters, crafts, insurance, repair and healing rather than cutting every incoming reward;
- keep transactional rollback, idempotency and pristine-data protection.

Trader specialization is a maintained invariant. Economy adjustments must preserve each trader's authored strong categories, useful barters and loyalty progression. A proposed change is blocked when it would make another trader's equivalent offer strictly better across price, access, stock and quality without an authored reason.

The engine computes once during database startup. It adds no raid polling, client-frame work or repeated filesystem scans.

Acceptance: two applications yield the same database; failed validation restores the complete pre-change state; scenario tests prove that pressure is selective rather than a uniform cut.

## E4 — presets as economic outcomes

Easy, Normal and Hard define target outcomes instead of fixed global percentages:

- **Easy:** removes extreme money loops while keeping generous access;
- **Normal:** makes raid loot, barters, trader progression and purchases remain relevant together;
- **Hard:** increases scarcity and recovery time without blocking required progression.

Custom exposes bounded policy controls. Cluster switches remain hard gates. F12 remains a settings client and never becomes a second economy engine.

Audit mode produces developer-only structured logs comparing Easy, Normal and Hard from the same immutable database snapshot. Reports include the evidence, original value, proposed value, applied guardrail and blocked mutations. These diagnostics are not shown in normal gameplay UI and are not written repeatedly during raids.

Acceptance: the same scenario suite shows ordered but non-linear pressure from Easy to Hard; Audit changes no database values; disabling a cluster leaves that surface byte-identical; normal gameplay receives no diagnostic spam.

## E5 — ecosystem adapters

Use optional, fail-closed adapters for known content:

- Admiral Trader: retain its finite stock, loyalty, quest unlock and authored reward semantics;
- WTT Armory and Content Backport: classify their actual templates and sources without blanket price or loot changes;
- Icebreaker: include its map sources only when the installed location and tables are valid;
- Belt: recognize owned container categories without copying or rewriting Belt behavior.

Adapters contain IDs and schema checks only. Economy Admiral gains no mandatory third-party dependency and does not control third-party spawns or inventories.

Economy Admiral preserves the authored identity and relative loot character of every location. Map-specific redistribution is outside this roadmap; installed locations contribute only evidence about real item supply.

The startup snapshot carries a database fingerprint per supported source. New or changed third-party records are classified normally only when their schema and evidence are understood. An anomalous or incompatible source is quarantined from mutation and reported internally; it never disables unrelated base-game or Admiral Suite handling.

Acceptance: each supported mod is tested both present and absent; schema drift disables only its adapter and leaves the base economy operational.

## E6 — validation and promotion

Before stable promotion:

1. Run deterministic classification, equivalent-group, progression-path, mutation, rollback and idempotency scenarios.
2. Build against exact SPT 4.1.5 while retaining `~4.1.0` compatibility metadata.
3. Validate Trader-only, Economy-only and combined server startup.
4. Inspect a generated change report grouped by progression stage and category, including untouched and blocked records.
5. Perform one combined fresh-profile gameplay pass after the full mod set is ready: early survival, raid loot, trader buy/sell, barters, flea, quest rewards, middle-game recovery and bounded Easy/Hard sanity.
6. Fix concrete failures, repeat the same gate once, then promote Economy 0.1.0 from preview to stable.

Stable acceptance requires useful choices across progression, no forced grind bottleneck, no uncontrolled third-party mutations, green exact-head CI, a clean server smoke and an install-ready Admiral Suite artifact whose provenance identifies each component's release channel.

## E7 — post-stable experiments

These experiments begin only after Economy Admiral 0.1.0 is stable. They ship in developer-only Audit mode first and cannot silently change an established profile economy.

### Adaptive early-game protection

Evaluate whether a new profile retains practical routes to basic medicine, usable ammunition, an affordable weapon path, simple armor, rigs and backpacks. Unlike the core dead-end safeguard, this experiment may recommend softer prices or stock during early level bands even when progression remains technically possible. It must prove that the result improves choice without making rare equipment common or creating special treatment that persists into the middle game.

### Arbitrage graph

Detect repeatable no-raid profit routes such as buy-disassemble-sell, barter-sell and buy-craft-sell. The calculation includes fees, quantities, durability, finite stock, loyalty and reset limits. Audit first reports the complete route and confidence. A later opt-in enforcement experiment may adjust only the smallest safe edge and must never disable a barter or craft from a single suspicious reference price.

Post-stable promotion requires separate scenario evidence and explicit acceptance for each experiment. Neither experiment is part of the Economy 0.1.0 stable gate.

## Explicit boundaries

- No second trader or economy engine.
- No profile cleanup or persistent-ID migration.
- No continuous telemetry, raid polling or per-frame client logic.
- No universal price, reward or loot multiplier as the final model.
- No duplication of Item Intelligence Admiral or Item Valuation MOD SPT calculations or UI.
- No mandatory WTT, Icebreaker, Belt or other third-party dependency.
- No Economy stable claim before the combined gameplay gate.
- No adaptive early-game enforcement or arbitrage correction before Economy 0.1.0 stable.
