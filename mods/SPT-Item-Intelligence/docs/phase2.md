# SPT Item Intelligence — Phase 2

Version `0.2.0` adds the **Safe-to-Sell** decision model on top of the Phase 1 semantic registry. It remains a standalone Item Intelligence DLL and has no Tactical HUD dependency.

## Input contract

A consumer provides one `ItemRequirementSnapshot` per template:

- normalized template id;
- total owned count;
- owned found-in-raid count;
- zero or more requirement reasons.

Each reason declares its scope, required count, FIR eligibility, optional prerequisite distance and enabled state.

Supported scopes, in default priority order:

1. active quest requiring FIR;
2. active quest;
3. selected hideout target;
4. next available hideout upgrade;
5. near-future quest within the configured prerequisite horizon;
6. wishlist;
7. optional barter;
8. optional craft.

Barter and craft protection are disabled by default. The near-future horizon defaults to prerequisite distance `2`.

## Eligibility and surplus

The evaluator reserves FIR items for FIR-only requirements first. Flexible requirements then consume ordinary items before any remaining FIR items. Requirements never protect owned units that are not eligible to satisfy them.

Outputs include:

- `ProtectedOwned`;
- `MissingFoundInRaid`;
- `MissingFlexible`;
- `MissingTotal`;
- `SafeSurplus`;
- ordered allocations and the highest-priority reason;
- `KEEP`, `SAFE TO SELL` or `NO CURRENT/NEAR REQUIREMENT` summary.

This means a shortage of FIR items does not incorrectly prevent unrelated non-FIR surplus from being sold.

## Phase boundary

Phase 2 deliberately does not add:

- inventory or stash scanning;
- recurring Unity reflection/object searches;
- server/profile acquisition;
- tile colors or checkmarks;
- hover tooltips;
- automatic selling or item movement;
- Tactical HUD integration;
- Hideout Target Planner or Quest/Raid Planner behavior.

Those runtime consumers can be attached later to this tested data model without changing the decision contract.
