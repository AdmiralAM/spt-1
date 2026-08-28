# B&A&HB #2 MOD SPT RC1 — combined SPT 4.1.3 runtime gate

Target: SPT 4.1.3.

This checklist describes the **current exact-head mechanics**. Historical 1x1 medical HeadBand candidates are obsolete. The current HeadBand is a `1x2` utility container with an exact currency/cigarette/wallet whitelist. The current mechanical Belt candidate is `2x2` magazine-only; ArmBand remains `1x2` magazine-only. The matching technical architecture is recorded in `DESIGN-SPT-4.1.3-BELT.md`.

Do not use the user as a per-patch debugger. Automated CI owns Scav compatibility and profile-recovery coverage. The user runtime gate is limited to PMC/gameplay behavior that cannot be proven outside SPT itself.

| # | Exact action | Expected PASS | Explicit FAIL | Minimal evidence |
| --- | --- | --- | --- | --- |
| 1 | Start SPT and open the **Items** inventory as PMC for the first time in the session. Do not switch to another tab and back. | Existing vanilla equipment remains intact; Belt is in dedicated slot 15; HeadBand is in slot 16 above Headwear immediately on first entry; captions are human-readable; no manual tab refresh is required. | HeadBand/Belt layout becomes correct only after changing tabs, missing/duplicated vanilla equipment, numeric-only dedicated caption, destructive panel distortion, startup/load failure. | One screenshot + relevant B&A&HB error/proof lines only on FAIL. |
| 2 | Open ArmBand `1x2`, Belt `2x2`, and Utility HeadBand `1x2`. | Each opens through native GridWindow with its declared grid; frame is tight to the cells and final fit reports `exact=True`. For `1x2` the calibrated target is `73x158`; for `2x2` it is `136x158`. | Container does not open, wrong grid geometry, clipping/filler, or `WINDOW FIT FINAL ... exact=False`. | On FAIL: one screenshot + matching `WINDOW FIT PROOF/FINAL` lines. |
| 3 | Verify item filters. Put a magazine in ArmBand and Belt. Put RUB/USD/EUR, one allowed cigarette type, and a supported wallet in HeadBand; also try one ordinary medical item or unrelated loot item. | Magazine wearables accept magazines according to their current policy. HeadBand accepts the current exact utility whitelist and rejects ordinary medical/unrelated items. | Cross-category acceptance, HeadBand behaving as generic secure/medical storage, or a currently whitelisted utility item rejected. | Item name/TPL only for the failing case. |
| 4 | Unequip/re-equip each wearable with contents, close/reopen inventory, then restart SPT/profile once. | Wearable root and descendants persist correctly; contents remain attached to their own root; no duplication/orphaning. | Lost contents, duplicate items, orphaned descendants, profile-load/save failure. | Relevant log lines + affected wearable/content IDs. |
| 5 | Exercise one normal raid lifecycle as PMC with contents in the three wearables and return alive. | Inventory remains coherent through raid transition; no unexpected migration/deletion or duplicated descendants. | Any root/tree corruption or profile-save issue. | Relevant IDs/log excerpt on FAIL. |
| 6 | With all three F12 `Protection` entries at default `Protected`, die as PMC carrying identifiable contents in ArmBand, Belt and HeadBand. | Each exact B&A&HB wearable root and its complete descendant tree is retained. No unrelated vanilla item is protected merely because of slot placement. | Root retained but descendants lost, protected family lost, or unrelated item receives B&A&HB protection. | Before/after wearable/content IDs + server B&A&HB protection lines on FAIL. |
| 7 | Change exactly one family to `LostOnDeath`, leave the other two `Protected`, then perform the next death test with identifiable contents. Repeat only as needed to cover all three family toggles. | The selected family receives normal SPT death/loss semantics; the other protected families retain root + descendants. F12 changes do not leak across families. | Toggle ignored, wrong family affected, or cross-family protection leak. | F12 state + affected IDs on FAIL. |
| 8 | Where an affected wearable/content item is insured, inspect post-raid insurance behavior after a protected death. | Items actually retained by B&A&HB are not simultaneously recorded as lost-insured/returned later. Items genuinely lost under `LostOnDeath` follow normal SPT insurance semantics. | False lost-insured event for retained tree, duplicate insurance return, or B&A&HB suppression of a genuinely lost item. | Insurance item IDs/server log excerpt on FAIL. |

## Deferred product polish

- Wallet coverage is not considered final: EFT/SPT has more than one wallet-style item. Expanding the exact HeadBand wallet whitelist is a later content pass rather than a blocker for the first-open layout fix.
- A possible final-stage HeadBand redesign is two independent `1x1` internal cells: one cigarette-only and one currency/wallet-only. Feasibility must be proven against the native multi-grid UI/template behavior before committing to it.

## Automated-only boundaries

Scav `ReplaceInventory` compatibility remains part of the implementation and automated hardening, but is **not a user runtime acceptance step** because the target play workflow does not use Scav. Profile recovery also remains packaged and CI-tested but is not part of the normal runtime gate unless an actual uninstall/recovery problem appears.

## Performance boundary

The client must remain interaction/event driven. GridWindow correction performs at most eight bounded settle passes per relevant opened window. First-open HeadBand stabilization performs only a short bounded post-`SlotView.Show` settle after the native Headwear presentation event. Scav compatibility resolves property/field runtime shape once and then uses cached delegates over only the three wearable slots. There must be no permanent `Update` polling, scene-wide inventory scan, per-`ReplaceInventory` reflection scan, or global UI mutation loop.

## Decision rule

A runtime PASS requires the applicable PMC product-critical rows above to show no profile/lifecycle/death/insurance defect. On FAIL, return only the smallest evidence requested by the failed row; remediation resumes from that concrete boundary.

Profile recovery remains packaged under `SPT_Runtime/user/mods/B&A&HB #2 MOD SPT/profile-safety/` for exceptional recovery use.
