# B&A&HB #2 MOD SPT RC1 — focused window-fit remediation gate

Target: SPT 4.1.3.

The previous physical pass already proved startup/profile safety, dedicated Belt/HeadBand routing and Alt-placement, close/open + unequip/re-equip persistence, restart persistence, one supported loot/unload/reload interaction and idle-cost sanity. **Do not repeat those checks for this step unless a new regression is visible.**

The only primary acceptance target of this candidate is physical GridWindow fit. A secondary observation is the already-known first-Items-tab HeadBand presentation delay; it must not distract from the window-fit check.

Current Emergency HeadBand test contract remains intentionally **1x1 and medical-only** (`MED_KIT`, `STIMULATOR`, `MEDICAL`). Cigarettes/currency are later product work.

| # | Exact action | Expected PASS | Explicit FAIL | Evidence |
| --- | --- | --- | --- | --- |
| 1 | Start SPT, enter **Items once**, and look at the dedicated HeadBand row. | Compact slot caption fits as `ГОЛ. ПОВЯЗКА` / `HEADBAND`. If the slot still requires one tab switch to appear, record that as the known low-priority presentation defect and continue to #2. | New panel distortion, missing vanilla equipment, or another new regression. | Screenshot only if layout regressed. |
| 2 | Open the `1x1` Emergency HeadBand container. | Outer GridWindow frame hugs the single 63px cell with only ordinary EFT title/border chrome; no broad empty strip to the right/bottom. Runtime target is `73x95`, and the log must end with `WINDOW FIT FINAL ... exact=True`. | Visible filler around the cell, clipping, or final-size drift. | One screenshot + the `WINDOW FIT PROOF/FINAL` lines. |
| 3 | Open the `1x2` ArmBand container. | Outer frame hugs the two stacked cells; runtime target is `73x158`, with `WINDOW FIT FINAL ... exact=True`. | Visible filler, clipping, or final-size drift. | One screenshot only if it fails + fit lines. |
| 4 | If convenient, open the `2x2` Belt container. | Outer frame target is `136x158`, with tight native chrome and no minimum-width padding. | Visible filler, clipping, or final-size drift. | Optional screenshot only on failure. |

The sizing path is event-driven. It applies immediately and then performs at most eight bounded settle passes after the window is opened so EFT/UI layout patches cannot restore the old minimum size. It does not add idle polling or a permanent `Update` loop.

If #2 and #3 pass physically, the current window-fit blocker is closed. The remaining first-entry HeadBand/preset-panel positioning issue stays a separate lowest-priority UI-polish item unless it becomes destructive.

Profile recovery remains packaged under `SPT_Runtime/user/mods/B&A&HB #2 MOD SPT/profile-safety/` and is not part of this focused test.
