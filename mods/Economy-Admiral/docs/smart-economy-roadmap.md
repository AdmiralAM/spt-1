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

Early-game protection is a progression guarantee, not a blanket discount. Before applying pressure, the engine proves that a new profile retains practical routes to basic medicine, usable ammunition, one affordable weapon path, simple armor, rigs, backpacks and required hideout or quest items. Protection can preserve one trader offer, barter, craft or sufficiently common loot source. It does not make rare equipment common and expires per category as replacements become available.

Weapons are evaluated as usable systems rather than bare receivers. Their effective cost includes required magazines, available ammunition and installed parts. Presets and assembled quest rewards use the value and availability of the complete build, while individual parts retain their own evidence.

Weapon and ammunition availability is paired. An accessible weapon must have at least one viable magazine and ammunition route at the same progression stage; premium ammunition may remain limited by loyalty, stock, barter or quest unlock.

Acceptance: maintained fixtures cover each category and stage; early essentials and required quest paths retain at least one practical acquisition route; usable weapon/ammunition pairs exist at their authored stage; rare items remain meaningfully valuable.

## E3 — targeted pressure engine

Replace broad surface multipliers with bounded target bands derived from acquisition and utility evidence:

- adjust only demonstrated outliers;
- preserve authored barter ingredients, loyalty levels, quest locks and finite stock identity;
- keep buy and sell changes internally consistent;
- detect cash, barter, craft and flea arbitrage loops before committing mutations;
- normalize quest rewards by difficulty, risk, duration and progression stage rather than by reward type alone;
- audit trader offers for unusable barters, dominated purchases, underpriced assembled builds, unlimited rare supply and offers with no rational use;
- use stock quantity and restock cadence as bounded pressure tools instead of solving every imbalance through price;
- reduce oversupply only when multiple renewable sources prove it, leaving items with uncertain or narrow supply unchanged;
- price repair and durability loss so repairing ordinary equipment remains useful while badly damaged premium equipment creates a real replace-or-repair decision;
- balance the complete money flow across raid sales, quests, traders, flea fees, barters, crafts, insurance, repair and healing rather than cutting every incoming reward;
- keep transactional rollback, idempotency and pristine-data protection.

An arbitrage loop is any repeatable route where the player can buy or barter inputs, transform or dismantle them, and sell the result for guaranteed profit without raid risk. Detection evaluates fees, component quantities, durability, finite stock, loyalty requirements and reset limits. The engine first reports the complete route, then adjusts the smallest safe economic edge. It never disables a barter or craft merely because one reference price looks suspicious.

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
