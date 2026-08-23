# Phase 13 — live requirement bootstrap

Phase 13 closes the gap between the Phase 12 EFT hover hook and the existing immutable requirement/presentation pipeline.

## Runtime flow

1. The client performs one bounded background request to `/spt-item-intelligence/v1/snapshot` through the SPT `RequestHandler` already loaded by the game.
2. Newtonsoft is discovered at runtime, so the client remains free of a hard compile-time SPT client dependency.
3. The snapshot projector builds owned template totals, current/future quest requirements and future hideout requirements.
4. A new immutable `ItemRequirementStateIndex` and `ItemPresentationIndex` are published atomically.
5. If an item is already hovered, only that item is reprojected. There is no update-loop polling.

## Conservative behavior

- completed quests and completed hideout stages are excluded;
- completed-condition progress is not guessed, so unresolved quest counts remain conservative;
- duplicate `FindItem` + `HandoverItem` pairs are collapsed;
- malformed/missing server data never produces a Safe-to-Sell decision;
- transport, schema, profile-readiness and reflection failures are contained and logged once.

## Visible diagnostic fallback

When the EFT hover hook resolves an item before live data is ready, the overlay shows `ITEM INTELLIGENCE`, the normalized template id and one explicit state:

- `LOADING ITEM DATA`;
- `NO REQUIREMENT DATA`;
- `DATA UNAVAILABLE`.

The fallback makes hook/data failures observable without inventing a sell recommendation.

Physical in-game validation remains the final validation step and does not block subsequent software work.
