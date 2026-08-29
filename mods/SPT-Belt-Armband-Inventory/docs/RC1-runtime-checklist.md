# B&A&HB #2 MOD SPT — combined post-stable runtime gate

Target: SPT 4.1.3.

Stable production remains in `main` through PR #264 and is the rollback baseline. This gate covers only the post-stable product/presentation package in PR #266. Do not reopen the already accepted death/insurance matrix unless a new Belt-specific regression appears.

## Candidate product contract

| Product | Host | Capacity / filter | Progression |
| --- | --- | --- | --- |
| Wrist Wallet | ArmBand | `1x1`, RUB/USD/EUR | Ragman LL1 — 12,500 RUB |
| Magazine Armband | ArmBand | `1x2`, MAGAZINE-only | Ragman LL1 — 25,000 RUB |
| Utility HeadBand | slot16 | two native `1x1`: currency/wallet + cigarettes | Ragman LL1 — 25,000 RUB |
| Magazine Belt | slot15 | `2x2`, MAGAZINE-only | Ragman LL2 — 45,000 RUB |

HeadBand currency/wallet cell accepts RUB/USD/EUR, Simple Wallet and WZ Wallet. Cigarettes cell accepts Apollo Soyuz, Malboro, Wilston and Strike.

## One combined user gate

1. **First Character → Items open after launch**
   - Open once without switching tabs for refresh.
   - FaceCover should be visibly reduced and HeadBand compactly positioned above it **inside the old FaceCover footprint**.
   - The rest of Gear Panel must not be globally shifted or protrude upward.
   - Belt and HeadBand must appear immediately; no duplicate/missing slot16.

2. **Repeat Items / stash lifecycle**
   - Close/reopen Items and stash several times.
   - Compact Face/HeadBand geometry must remain stable with no drift, duplicate slot, disappearing HeadBand or manual refresh requirement.

3. **Ragman product roster**
   - Wrist Wallet: 12,500 RUB / LL1.
   - Magazine Armband: 25,000 RUB / LL1.
   - Utility HeadBand: 25,000 RUB / LL1.
   - Magazine Belt: 45,000 RUB / LL2.
   - No user-visible `Runtime Candidate Magazine Belt` name remains.

4. **Filters and HeadBand two-cell layout**
   - Wrist Wallet accepts only RUB/USD/EUR.
   - Magazine Armband and Magazine Belt accept magazines according to their `1x2` / `2x2` capacities.
   - Utility HeadBand displays two independent `1x1` cells within its total `1x2` footprint.
   - currency/wallet cell accepts RUB/USD/EUR, Simple Wallet, WZ Wallet and rejects cigarettes/unrelated loot;
   - cigarettes cell accepts the four intended cigarette items and rejects currency/wallet/unrelated loot.

5. **Existing HeadBand content migration**
   - If the profile already had items inside the old Stable Baseline 1 HeadBand, verify nothing disappeared.
   - one currency/wallet item should remain in `main` and one cigarette item should move to its cigarettes cell;
   - same-category overflow from the former `1x2`, if present, is expected in the PMC sorting table rather than deleted.
   - If the old HeadBand was empty, this row is automatically PASS.

6. **One normal PMC lifecycle**
   - Proceed through normal pre-raid screens into one PMC raid.
   - Open inventory once in raid and return normally.
   - PASS: no B&A&HB exception, frozen inventory/pre-raid UI, missing/duplicate wearable tree or refresh requirement.

Report only `1 PASS / 2 PASS / ...` and attach a screenshot/log only for a failed row.

## Automated-only boundaries

CI owns Scav compatibility, profile migration regression, backup/recovery, persistent identity uniqueness, hot-path safety, compact-layout ownership and client/server build/package checks. These are not additional user tests.

Death/protection/insurance retesting is required only if a later change touches those runtime owners or new evidence points to a Belt-specific regression.
