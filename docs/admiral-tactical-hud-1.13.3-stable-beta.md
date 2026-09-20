# Admiral Tactical HUD 1.13.3 — Stable Beta

**Release status:** Stable Beta — user-confirmed playable baseline. **This is not final stable.**

## Frozen baseline

- Product: **Admiral Tactical HUD**
- Version: **1.13.3**
- Release label: **Stable Beta**
- Runtime code baseline: `d7d63c0196af12141aedfc8f4896f0a9a84376ed`
- Physical acceptance: user-confirmed playable, 2026-09-20
- Kill feed included: **no**
- Final stable: **no**

The purpose of this baseline is to preserve a known-good gameplay state that can be used normally while further polish and expansion are deferred.

## Included

- Compact population HUD with the established Admiral role icons.
- Independent optional Full Census.
- Bot Census-derived glyph/classification support with MIT attribution.
- MoreBotsAPI and Fika soft integrations.
- Player status/body/self assets and fallback architecture.
- Current HUD edit/configuration behavior.
- Current raid/menu lifecycle and deterministic cleanup/replacement contract.

## Explicitly excluded: rejected kill feed

The unsuccessful kill-feed implementation is retired and is not part of Stable Beta.

It must not be restored, relabeled or used as the implementation template for final stable. In particular, its previous weapon-text / weapon-icon contracts are historical implementation details rather than future product requirements.

The old Actions RC2 artifact from `615c99c3892e62764b5b748fed21ae3840fb9e28` predates kill-feed removal and is **not** the Stable Beta artifact.

## Mandatory next product stage: kill feed redesign

When HUD development resumes, the first new product stage is a fresh kill-feed design.

Required qualities:

- understandable at a glance;
- expressive without becoming decorative noise;
- rapidly readable during combat;
- compact and bounded in number/lifetime;
- clear visual hierarchy;
- no overlap with important Tarkov HUD/combat information;
- no requirement to reuse the rejected implementation.

No new kill-feed implementation is authorized by this Stable Beta fixation itself.

## Artifact/provenance

The current user-accepted working runtime package is anchored to runtime code baseline `d7d63c0196af12141aedfc8f4896f0a9a84376ed`.

Stable-Beta-specific README, machine-readable release metadata and packaging rules are maintained on the live HUD PR as metadata-only follow-up. A newly generated GitHub Stable Beta artifact, when available, must preserve the same runtime code baseline and must not contain kill-feed code.

## Forward roadmap

1. Stable Beta baseline — accepted.
2. Kill feed redesign — mandatory next product stage.
3. Architecture consolidation.
4. Population refinement.
5. Status refinement.
6. HUD layout/editing refinement.
7. Performance hardening.
8. Final release candidate and **final stable** only after all remaining acceptance gates pass.
