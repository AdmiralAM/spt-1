# SPT Tactical HUD / Server QoL research — 2026-08-22

Protected baseline: **v1.13.0**. Current client work: **v1.13.1**. This document is research/backlog only; it does not change runtime behavior.

## Performance audit — next safe code pass

High-value low-risk targets found in the current HUD code:

1. `Plugin.Refresh()` currently allocates a new `List<object>` and `HashSet<string>` every refresh (up to 4 Hz in raid), and uses LINQ `Where(...).ToList()` to collect removed tracked IDs. Replace these with reusable scratch collections cleared between refreshes.
2. Kill-feed `WeaponKey()` uses `params string[]` in repeated `HasAny(...)` calls. Those token arrays are created repeatedly during rendering. Replace with static readonly token arrays or allocation-free ordinal searches.
3. `HitKey()` lowercases the hit string every draw. Replace with `IndexOf(..., StringComparison.OrdinalIgnoreCase)` tests to avoid transient strings.
4. `DrawKillFeed()` calls `CleanWeapon()` and classification logic again on every repaint for every visible kill. Precompute normalized weapon key / hit key when a `KillLine` is created, then render cached values.
5. `Text()` constructs a new `GUIContent` for every measured value. Reuse a single mutable `GUIContent` instance inside the renderer.
6. Mouse/edit IMGUI events currently traverse the same layout/text-measure paths as repaint. Cache last rendered cluster rects so non-repaint mouse events only run drag-hitbox logic.

Acceptance gate: no gameplay/runtime behavior change, no new recurring reflection/global object scans, no extra logging, and no measurable FPS/frametime/stutter regression.

## Current mod-stack finding: MoreCheckmarks on SPT 4.1.2

User-side log evidence shows `MoreCheckmarks` is installed, but its client repeatedly receives HTTP 404s for `/MoreCheckmarksRoutes/...`, then reports `Failed to parse quest data` and loads zero quests. This matches the upstream architecture gap: current upstream MoreCheckmarks is a 4.0-era two-part client/server mod, while SPT 4.1 requires server mods to be rebuilt/migrated against the new 4.1 server API.

### Decision

Do **not** build new QoL logic around MoreCheckmarks as if it were healthy on this 4.1.2 install. Treat its concept as a reference and either:

- port/adapt its backend to 4.1.x,
- reuse only its client-side presentation concepts with our own lightweight data source,
- or supersede it with a separate focused `Item Intelligence` module.

## Existing concepts worth keeping/reusing

### Task Item Indicator

Already available specifically for SPT 4.1.2. It only indicates true quest task-items (the items that enter the task-items inventory), not generic hand-in loot such as Salewas. This makes it complementary to—not a replacement for—inventory/quest requirement intelligence.

**Action:** keep as a separate lightweight client QoL concept; do not duplicate it in Tactical HUD.

### MoreCheckmarks concept

Strong ideas worth preserving:

- hideout requirement counts;
- current/future quest requirement awareness;
- prerequisite-distance concept for future quests;
- configurable priority between quest/hideout/wishlist/barter/craft;
- explicit stash possessed-count context.

Weakness for a large quest stack: marking every future requirement creates too much signal. Our version should add progression horizon and surplus logic.

### Hideout In Progress concept

Turning construction items in gradually as they are found is a strong anti-stash-clutter concept. This is preferable to forcing the player to retain an entire future upgrade set in junk boxes.

**Action:** investigate a 4.1.x port/adaptation separately from Tactical HUD.

## Proposed module: Item Intelligence

Goal: answer one question quickly: **keep, use soon, or sell?**

For each item template, maintain compact requirement state:

- active quest deficit;
- near-future quest deficit (bounded prerequisite depth, configurable);
- selected hideout-target deficit;
- next-level hideout deficit;
- optional craft/barter/wishlist reasons;
- FIR requirement split;
- owned total / owned FIR;
- safe surplus.

### Priority model

Suggested default order:

1. active quest + FIR required;
2. active quest;
3. selected hideout target;
4. next available hideout upgrades;
5. near-future quest (small prerequisite distance only);
6. wishlist;
7. optional barter/craft.

The UI should show only the highest-value reason on the item tile/checkmark; full reasons live in tooltip/context view.

### Safe-to-Sell

Core formula:

`safeSurplus = max(0, ownedEligible - protectedRequirement)`

Where `protectedRequirement` is computed from enabled progression scopes and respects FIR/non-FIR eligibility.

Output examples:

- `KEEP 2 — Therapist quest`
- `KEEP 1 — Water Collector 3`
- `SAFE TO SELL: 3`
- `NO CURRENT/NEAR REQUIREMENT`

This is more useful than a binary future-checkmark because it prevents both accidental sales and hoarding.

## Proposed module: Hideout Target Planner

Player chooses a target station/level, for example `Intelligence Center 3`.

The planner resolves:

- prerequisite station levels;
- required items for every missing prerequisite step;
- owned / FIR-owned / missing counts;
- already satisfied steps;
- a flattened raid shopping list.

Only items on the selected dependency path receive elevated loot priority. This prevents the usual problem where virtually every barter item is marked because it is needed somewhere eventually.

## Proposed module: Quest / Raid Planner

The problem is not merely quest location; it is objective overload.

Build a lightweight planner that groups current objectives by map and assigns a score using only known profile data:

- number of active objectives on map;
- multiple objectives sharing an area;
- unlock-chain value / prerequisite depth;
- nearly completed tasks;
- task-item / hand-in items already owned;
- optional user pinning;
- penalties for restrictive equipment or awkward one-off trips.

Output should be compact, e.g.:

`Customs — 5 useful objectives / 3 quests`

with expandable detail outside raid. Avoid displaying hidden enemy/loot information; this is organization, not cheating.

## Architecture recommendation

Keep responsibilities separated:

- **Tactical HUD:** raid-time display, kill feed, population, player status, minimal contextual raid cues.
- **Item Intelligence:** stash/item tooltip/checkmark + keep/sell logic.
- **Planner:** hideout dependency and quest/map planning, primarily outside raid.

Do not turn Tactical HUD into a monolith. Share a small common data model only if necessary.

## Research next

1. Inspect MoreCheckmarks backend and SPT 4.0→4.1 server migration requirements to estimate a clean port.
2. Inspect All Quests Checkmarks, UI Fixes, Stash Management Helper, Hideout In Progress, AutoDeposit, Quick Move To Containers, Task List Fixes, Quest Tracker, Expanded Task Text, and Quest Presence Detector for reusable UX patterns.
3. Identify which are already present in the user's current stack to avoid duplication.
4. Prototype `Safe-to-Sell` data model before any UI work.
5. Apply the low-risk allocation reductions listed above only after preserving v1.13.0 rollback and keeping client packaging isolated from unchanged server runtime.
