# Admiral Tactical HUD 1.13.3 — Stable Beta

**Status:** Stable Beta — playable accepted baseline, **not final stable**.

## Baseline

- Product: **Admiral Tactical HUD**
- Version: **1.13.3**
- Release label: **Stable Beta**
- Runtime code baseline: `d7d63c0196af12141aedfc8f4896f0a9a84376ed`
- Physical status: **user-confirmed playable** on 2026-09-20
- Final stable: **no**

This baseline freezes the currently working HUD state as suitable for normal play while later polish and product expansion remain intentionally deferred.

## Included in Stable Beta

- Compact population display with the established Admiral role icons.
- Optional independent Full Census population display.
- Bot Census-derived glyph/classification support with retained MIT attribution.
- MoreBotsAPI and Fika as soft optional integrations only.
- Admiral status/body/self icon fallback architecture.
- HUD edit/configuration behavior and current raid/menu lifecycle.
- Deterministic cleanup/replacement manifest for installation.

## Kill feed boundary

The unsuccessful kill-feed implementation is **not part of this Stable Beta baseline**.

- Do not restore it.
- Do not treat its previous weapon-text or weapon-icon contracts as the next implementation design.
- Do not substitute an older RC artifact that still contains the retired kill-feed implementation.

Kill feed remains the **mandatory next product stage** after Stable Beta. It must be designed again from first principles as a clear, expressive and rapidly readable combat feed with:

- immediate at-a-glance hierarchy;
- compact presentation;
- low visual noise;
- bounded on-screen lifetime/count;
- no obstruction of important Tarkov UI or combat information;
- no inherited requirement to reproduce the rejected implementation.

No new kill-feed implementation is included in this release.

## Artifact authority

The Stable Beta runtime authority is the user-accepted package corresponding to the code baseline above, plus metadata-only Stable Beta packaging changes.

The older GitHub Actions RC2 artifact from head `615c99c3892e62764b5b748fed21ae3840fb9e28` is **historical RC evidence only** and must not be promoted or relabeled as Stable Beta because it predates the kill-feed removal.

## Forward roadmap

1. **Stable Beta baseline** — accepted here.
2. **Kill feed redesign** — mandatory next product stage.
3. Architecture consolidation.
4. Population/status polish and UX refinement.
5. Layout/editing refinement.
6. Performance hardening.
7. Final release candidate and **final stable** only after the remaining roadmap passes its acceptance gates.
