# B&A&HB #2 MOD SPT

Wearable inventory extension for SPT 4.1.3. The physically accepted production baseline is integrated into `main` through PR #264 at `0879a97ce835c0c109fb1bb6c24dbd142b743405`; its accepted runtime source is `d6336f290361b16c4aa54f9d7dddfe0e8f7f9bbf`. Current development is isolated in PR #266 on `belt-post-stable-development`.

Current module version: **0.1.0**.

## Product roster

- **Wrist Wallet** — ArmBand host, `1x1`, RUB/USD/EUR only, Ragman LL1, 12,500 RUB.
- **Magazine Armband** — ArmBand host, `1x2`, MAGAZINE-only, Ragman LL1, 25,000 RUB.
- **Magazine Belt** — dedicated slot **15**, `2x2`, MAGAZINE-only, Ragman LL2, 45,000 RUB.
- **Utility HeadBand** — dedicated slot **16**, total `1x2` footprint made from two native independent `1x1` grids, Ragman LL1, 25,000 RUB:
  - `main` — currency/wallet only: RUB, USD, EUR, Simple Wallet, WZ Wallet;
  - `cigarettes` — Apollo Soyuz, Malboro, Wilston, Strike only.

The original HeadBand grid identity `68ac00000000000000000010` is preserved as `main`. The cigarettes grid adds persistent identity `68ac00000000000000000012`. No existing distributed ID is repurposed.

## Compact Face + HeadBand presentation

Post-stable presentation keeps the redesign inside the **original FaceCover footprint** instead of expanding Gear Panel:

- FaceCover retains its original width and is reduced to roughly half its original height;
- HeadBand is `44 px` high with a `4 px` local gap above FaceCover;
- both occupy the original FaceCover footprint;
- unrelated native equipment slots are not moved;
- Gear Panel is not resized or translated;
- no `LayoutElement.preferredHeight`, Canvas force-refresh, coroutine, retry positioner or idle polling is used by the compact owner.

Stable Baseline 1's older 48 px reflow remains compiled as a fallback only. It is suppressed **after** the compact `EquipmentTab.Show` postfix installs successfully; if compact installation fails, accepted stable presentation remains active unchanged.

Expected runtime evidence:

`B&A&HB COMPACT FACE/HEADBAND PROOF: stable reflow suppressed; ... hostPanelMutation=false.`

## HeadBand split-grid profile migration

The server uses SPT 4.1's native `AbstractProfileMigration` lifecycle before profile deserialization.

For existing Utility HeadBands created by Stable Baseline 1:

- currency/wallet children remain under preserved grid `main` and are normalized to `1x1` origin;
- cigarettes move to grid `cigarettes` and are normalized to `1x1` origin;
- if an old `1x2` contains multiple items of the same new category, one remains in the appropriate `1x1` and overflow is **preserved** in the PMC sorting table;
- descendants of an overflow root remain attached to that root;
- no inventory item is deleted by the migration;
- the migration is idempotent and CI executes a real legacy-profile fixture against the compiled server implementation.

## Death / insurance protection

F12 exposes independent `Protected` / `LostOnDeath` settings for ArmBand, Belt and HeadBand. All default to `Protected`.

Protection is exact-root scoped to registered B&A&HB wearable templates and their descendant trees. Death retention and insurance-loss filtering consume the same server snapshot. The Admiral Trader insurer `DEFAULT_VALUE` incident is outside B&A&HB and is not worked around here.

The accepted protection/death/insurance runtime was not redesigned by the post-stable product/presentation work.

## Dedicated lifecycle

- Belt and HeadBand keep dedicated wire slot IDs `15` and `16`.
- slot16 insertion/recovery remains in the `EquipmentTab.Show` prefix before native `_slotViews` enumeration.
- a live slot16 mapping is retained; only a stale Unity-null mapping is recovered.
- late `SlotView.Show` remains forbidden from adding/removing/cloning slot-map entries while EFT enumerates it.
- exact Alt-pickup and compatibility routing remain bounded to registered wearable identities.
- Scav compatibility remains automated-only and does not use idle polling or scene/inventory sweeps.

## Profile / uninstall safety

The package maintains an authoritative persistent-identity manifest and backup-first ownership-scoped cleanup tooling. Do not manually delete arbitrary profile nodes. Use the packaged recovery tooling when disabling/uninstalling a build that has already written B&A&HB identities.

## Performance contract

Production behavior is interaction/event driven:

- no permanent production `MonoBehaviour.Update` loop;
- no scene-wide scans;
- no hierarchy-wide polling;
- no unbounded deferred refresh;
- reflection-heavy host discovery is bounded to installation paths;
- deferred GridWindow sizing drains to zero.

Development CI validates hot paths, product contract, compact-layout ownership, profile migration, profile recovery, client/server compilation and exact-head packaging.

## Repository layout

- `src/` — client runtime and presentation;
- `server/` — SPT item/slot/trader/protection/profile-migration integration;
- `profile-safety/` — identity manifest and recovery tooling;
- `tests/` and `tools/` — development-only regression/validation material on PR #266, not part of stable `main` production source;
- `docs/` — current development/runtime acceptance notes.

## Compatibility

Pack 'n' Strap and Trenchfoot BeltSlot are reference/archaeology sources, not runtime dependencies. Remove/disable legacy `Trenchfoot-BeltSlot.dll` before using B&A&HB so two implementations do not patch the same host behavior.

The server project targets SPT 4.1.3 `SPTushonka.*` packages.

## Development boundary

PR #266 is the single post-stable development/evidence record. Stable `main` remains the rollback product. The next promotion is allowed only after one combined exact-head runtime gate for compact presentation, split HeadBand grids/product filters and one ordinary PMC lifecycle.
