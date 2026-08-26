# Quest Planner delayed availability semantics

## Why this matters

A non-zero `availableAfter` on a quest prerequisite is configuration, not a countdown. The planner must distinguish the configured delay from the player's current delayed-availability state or it can tell the player to wait after the delay has already elapsed.

## Source-backed SPT model

Current SPT quest documentation defines raw quest status `9` as `AvailableAfter`: the quest is delayed from being shown to the player. The quest prerequisite `availableAfter` field is a duration in seconds.

Current SPT server/profile documentation additionally exposes two runtime provenance fields on the profile quest status:

- `AvailableAfter`: absolute timestamp when the delayed quest becomes available;
- `StatusTimers`: timestamps of quest-status transitions.

These are different from the static prerequisite duration.

## Previous Quest Planner gap

`ProfileProjection` previously attempted to read a singular `statusTimer` scalar and did not preserve the authoritative `AvailableAfter` timestamp or the per-status `StatusTimers` dictionary. That made exact remaining-delay evidence impossible even when SPT already exposed it.

The projection now preserves all three separately for compatibility/provenance:

- legacy scalar `StatusTimer` when present;
- `AvailableAfterUnixSeconds` as the authoritative delayed-availability timestamp;
- raw-status keyed `StatusTimers` as transition evidence.

Named enum keys such as `Success` and `AvailableAfter` and numeric keys are both normalized to their raw SPT status numbers.

## Evidence states

`PlannerQuestDelayTimingBuilder` is intentionally conservative:

1. **NotDelayed** — authoritative raw profile status is not `9`. A stale `AvailableAfter` field does not override the current quest status.
2. **PendingKnown** — raw status is `9`, an absolute `AvailableAfter` timestamp exists, snapshot time is known, and `AvailableAfter > snapshot time`. Exact remaining seconds may be shown.
3. **ElapsedPendingRefresh** — raw status remains `9` but the absolute timestamp is already at or before snapshot time. The planner must not locally promote the quest to Available; it waits for authoritative server/profile refresh.
4. **TimingUnresolved** — raw status is `9` but the absolute timestamp or snapshot clock is unavailable. The planner may state that availability is delayed, but must not invent a countdown.

## Decision-policy consequence

Configured `availableAfter > 0` still suppresses hypothetical **immediate unlock** claims. Runtime timing evidence can explain whether an already-materialized delayed quest is pending and, when proven, how long remains. It does not retroactively turn a delayed structural edge into an immediate-completion benefit.

This keeps two questions separate:

- **If I complete source quest S now, does T unlock immediately?** Static edge semantics answer this; non-zero configured delay means no.
- **T is already in delayed state in my profile; how long is left?** Runtime `AvailableAfter` provenance answers this when available.

## Runtime/performance contract

No new route, polling loop, global scan, Harmony patch or wall-clock timer is required. The state snapshot already has `GeneratedAtUnixSeconds`; delayed evidence is a constant-time comparison against the profile's absolute timestamp. UI may render the snapshot-derived remaining duration, but should refresh through the existing state-refresh boundary rather than creating a new per-frame countdown authority.
