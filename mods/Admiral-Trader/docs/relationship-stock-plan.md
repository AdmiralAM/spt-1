# Relationship stock progression slice

This is a post-gate design slice. It does not modify the frozen `0.1.0` runtime candidate.

## Purpose

Admiral standing should have a visible gameplay consequence without becoming a second capability gate. Relationship stock is therefore bounded specialist logistics at LL2-LL4 while Access/Arsenal quest milestones retain exclusive authority over Labs access and controlled ammunition privileges.

Relationship stock is **quality-gated, not quota-filled**. A loyalty tier is allowed to remain empty when no candidate creates genuine specialist availability. Filling LL2/LL3/LL4 with duplicates, raw barter inputs or generic convenience items merely to make every tier look populated is explicitly rejected.

## Tier semantics

- **LL2 / 0.10 — Trusted field logistics.** Mission preparation, navigation, observation and field-maintenance convenience. Small finite quantities only when the offer adds independent availability.
- **LL3 / 0.30 — Specialist expedition support.** Rarer reconnaissance and expedition equipment with recurring utility. Existing renewable vanilla supply disqualifies a duplicate.
- **LL4 / 0.55 — Command-trust logistics.** Scarce high-trust observation/logistics equipment. The reward is specialist availability, not unrestricted combat power or a nominal tier prize.

## Selection gate

No exact offer is approved merely because it fits the prose category or because a tier is empty. Every candidate must have:

1. an exact SPT 4.1.3 TPL from the pinned source set;
2. a direct overlap result for vanilla, Scorpion and Artem;
3. independent recurring specialist utility rather than a tier-fill rationale;
4. a role that is not generic-supermarket stock, raw barter supply or story/combat capability bypass;
5. finite stock and buy restriction;
6. Economy Admiral price/supply review;
7. no quest gate and no capability-milestone substitution.

Candidates that fail any gate are rejected rather than silently substituted. The reviewed Documents case, Key tool and SICC pouch are examples: all fit the broad logistics fantasy, but pinned SPT 4.1.3 already provides renewable unlimited vanilla sources, so they do not create an Admiral relationship privilege.

## Runtime boundary

`manifests/relationship-stock.json` is authoritative for the class and tier contract. `materialization.enabled=false` means the active `db/assort.json` must remain the frozen 11-offer set. Materialization is a later separately reviewable product commit and must add explicit machine-readable Relationship classification to the Economy Admiral adapter before any Relationship offer enters the runtime assort.
