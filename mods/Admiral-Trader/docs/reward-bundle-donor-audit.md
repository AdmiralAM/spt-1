# Reward bundle donor audit

## Audited source authority

- ISB Aishi: `c0025d2afbdeeb687f7ef9ec426c4ebaf1de2495` (`v2.0.2` repository tag; source metadata still reports `1.0.4` and `~4.0.13`).
- Weekend Drops: `3ded7b608fabd6b6c8c5747d526202410f9f55f8` (`1.0.6`).

Neither donor is an Admiral runtime dependency. No donor trader, narrative, faction, asset, persistent ID, offer, quest or profile record is imported.

## Decisions

| Mechanism | Decision | Admiral boundary |
| --- | --- | --- |
| Aishi quest and unlock catalogues | Adapt the data-validation principle | Keep Admiral's native SPT 4.1.5 quest and lower-case quest-assort lifecycle. |
| Aishi optional-mod gates | Adapt with stronger evidence | Require actual published template capabilities, not only a folder or mod name. |
| Aishi custom items, bots, cosmetics, services and story | Exclude | Outside the approved reward-composition milestone. |
| Weekend Drops role-based crate slots | Adapt | Replace broad random groups with explicit thematic required and optional roles. |
| Weekend Drops duplicate-category avoidance | Adapt | Validate duplicate function before publication without a Harmony loot patch. |
| Weekend Drops price-band weapon parts | Rewrite and extend | Enforce one bundle budget plus weapon, part, magazine and ammunition compatibility. |
| Weekend Drops random selection | Replace | Use an injectable deterministic seed in automated tests. |
| Weekend Drops parallel JSON progress and claim stores | Reject | Native SPT remains the only quest lifecycle and reward-delivery authority. |
| Silent/default-on-read failure handling | Reject | Distinguish catalogue, generation, validation and publication failures and retain the last valid state. |

## Accepted implementation scope

The new layer may compose future thematic quest rewards and finite storefront packages. Existing quests, rewards, assortment, balance and profiles remain unchanged until a separate authored reward or offer explicitly opts into a reviewed bundle specification.

The initial implementation consists of typed bundle specifications, catalogue capability resolution, deterministic selection, weapon/loadout validation, publication staging and regression tests. It must work with the base SPT catalogue alone and safely enrich eligible pools when supported optional templates exist.
