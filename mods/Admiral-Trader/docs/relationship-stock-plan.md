# Relationship stock progression slice

This is a post-gate design slice. It does not modify the frozen `0.1.0` runtime candidate.

## Purpose

Admiral standing must have a visible gameplay consequence without becoming a second capability gate. Relationship stock therefore provides bounded specialist logistics at LL2-LL4 while Access/Arsenal quest milestones retain exclusive authority over Labs access and controlled ammunition privileges.

## Tier semantics

- **LL2 / 0.10 — Trusted field logistics.** Mission preparation, navigation, observation and field-maintenance convenience. Small finite quantities.
- **LL3 / 0.30 — Specialist expedition support.** Rarer reconnaissance and expedition equipment. Still no permanent high-end ammunition or access privilege.
- **LL4 / 0.55 — Command-trust logistics.** Scarce high-trust observation/logistics equipment. The reward is reliable specialist availability, not unrestricted combat power.

## Selection gate

No exact offer is approved merely because it fits the prose category. Every candidate must have:

1. an exact SPT 4.1.3 TPL from the pinned source set;
2. a direct overlap result for vanilla, Scorpion and Artem;
3. a role/rationale that is not generic-supermarket stock;
4. finite stock and buy restriction;
5. Economy Admiral price/supply review;
6. no quest gate and no capability-milestone substitution.

Candidates that fail any gate are rejected rather than silently substituted.

## Runtime boundary

`manifests/relationship-stock.json` is authoritative for the class and tier contract. `materialization.enabled=false` means the active `db/assort.json` must remain the frozen 11-offer set. Materialization is a later separately reviewable product commit and must add explicit machine-readable Relationship classification to the Economy Admiral adapter before any Relationship offer enters the runtime assort.
