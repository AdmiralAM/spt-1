# Admiral Trader loyalty role

## Decision

Admiral loyalty is **relationship/status feedback**, not an authority boundary for capability access.

The capability-broker loop remains authoritative:

`prove capability -> quest success -> bounded offer/sample`

Trader standing may summarize the player's overall relationship with Admiral, but reaching a loyalty tier must never reveal an offer that its capability quest has not already unlocked.

## Why keep standing at all

The 31-quest campaign spans access operations and seven weapon specializations. A small global reputation signal gives the trader a readable overall relationship arc without forcing those independent capability tracks into one linear prerequisite chain.

That distinction is important:

- **quest state answers:** what has this player proven and therefore earned access to?
- **standing answers:** how established is the overall relationship with Admiral?

Standing is therefore presentation/progression feedback, not a substitute for proof.

## Static contract

The current data intentionally enforces the separation:

- every permanent Admiral offer has loyalty level `1`;
- every permanent Admiral offer is separately listed in `questassort.Success`;
- no offer can be obtained through standing alone;
- all loyalty tiers have `minSalesSum = 0`, so purchase-volume grinding is not a progression gate;
- all loyalty tiers use the same buy-price coefficient, so standing grants no price advantage;
- repair and insurance remain disabled;
- authored campaign standing totals `0.65`, while the final status threshold is `0.55`, so completing enough of the curated campaign can naturally reach the highest relationship status without requiring generic sales grinding.

These are regression-tested through `tests/test_gameplay_policy.py` and encoded in `manifests/gameplay-policy.json`.

## Consequences for future design

A future feature that wants to gate an item, service, discount, or capability by Admiral loyalty must be treated as a doctrine change, not routine tuning. The preferred design remains an explicit quest/capability proof with a bounded reward.

If loyalty levels ever become functionally unnecessary or confusing in the actual client UI, collapsing or renaming their presentation can be evaluated in a later runtime-backed slice. The current static contract deliberately avoids changing SPT loyalty data shape before that boundary is proven.
