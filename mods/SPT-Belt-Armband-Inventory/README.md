# B&A&HB #2 MOD SPT

Wearable inventory extension for SPT 4.1.3. The mechanically accepted rollback point is **Stable Baseline 1** at exact head `d6336f290361b16c4aa54f9d7dddfe0e8f7f9bbf`, preserved on branch `belt-stable-baseline-1`. Current development continues on PR #64 without changing persistent identities or the accepted slot15/slot16 lifecycle unless a concrete regression requires it.

Current module version: **0.1.0**.

## Product roster

B&A&HB keeps three explicit wearable categories with narrow item-specific roles:

- **ArmBand** — two specialist choices on the proven vanilla ArmBand host:
  - **Wrist Wallet** — `1x1`, currency-only, Ragman LL1, 12,500 RUB;
  - **Magazine Armband** — `1x2`, MAGAZINE-only, Ragman LL1, 25,000 RUB.
- **Belt** — dedicated pseudo-slot **15** between Pockets and Backpack:
  - **Magazine Belt** — `2x2`, MAGAZINE-only, Ragman LL2, 45,000 RUB.
- **HeadBand** — dedicated pseudo-slot **16** with the accepted compact first-open presentation:
  - **Utility HeadBand** — `1x2`, Ragman LL1, 25,000 RUB; exact whitelist only: RUB, USD, EUR, Apollo Soyuz, Malboro, Wilston, Strike, Simple Wallet and WZ Wallet.

The Utility HeadBand is deliberately not a generic secure, medical or container-class slot. Wallet support is exact-template based. The future split two-cell design remains deferred.

Distributed identities are immutable. They are recorded in the persistent identity manifest and must never be renamed, reused for another category, or silently dropped.

## Death / insurance protection

F12 exposes an independent **Protection** setting for every wearable family:

- `ArmBand`: `Protected` / `LostOnDeath`;
- `Belt`: `Protected` / `LostOnDeath`;
- `HeadBand`: `Protected` / `LostOnDeath`.

All three default to `Protected`. Protection is exact-root scoped: only registered B&A&HB wearable templates in their intended equipment hosts are protected, together with their complete descendant inventory trees. An arbitrary vanilla or third-party item does not become protected merely because it occupies ArmBand or pseudo-slot 15/16. `LostOnDeath` removes B&A&HB special retention for that family and delegates normal loss behavior to SPT.

The server publishes protection roots as one immutable atomic snapshot. Death retention and insurance-loss filtering consume the same snapshot, and the two SPT 4.1 runtime patches are installed as one DI-managed feature with rollback if either half cannot bind. This prevents a protected tree from being retained while still generating false lost-insured events, or insurance suppression from surviving without matching death retention.

The F12 protection route declares `WearableProtectionRequest` as its typed SPT request body. It does not stringify and reparse `IRequestData`; that older path received `EmptyRequestData` and failed JSON parsing at startup before any setting could be applied.

## Dedicated presentation and routing

The client projects Belt and HeadBand through native EFT equipment/slot boundaries rather than permanent UI polling:

- dedicated equipment slots are registered against the SPT 4.1.3 `InventoryEquipment` boundary;
- exact item filters prevent cross-category placement;
- Belt and HeadBand use dedicated wire slot IDs `15` and `16`;
- slot16 is inserted/recovered in the `EquipmentTab.Show` prefix before EFT enumerates `_slotViews`; a live mapped view is preserved and only a stale Unity-null entry is replaced;
- late `SlotView.Show` binding is forbidden from adding/removing/cloning slot-map entries while EFT enumerates the dictionary;
- the accepted HeadBand structural presentation uses one `44 + 4 px` row, keeps slot16 at the original Headwear position and translates individual native slot rectangles without resizing or moving the host Gear Panel;
- no `LayoutElement.preferredHeight`, global Canvas force-refresh, host-panel transform correction, coroutine retry or idle polling participates in HeadBand placement;
- exact dedicated-item Alt-pickup resolves to the matching dedicated slot when it is empty and compatible;
- visible dedicated captions are owned by dedicated presentation policy, including EN/RU labels;
- Belt ContainersPanel caption repair is bounded to the exact factory-created row;
- existing ArmBand container mechanics remain isolated from the dedicated slot15/slot16 locations.

## Scav / PMC lifecycle boundary

PMC behavior remains on the ordinary inventory lifecycle. Scav `ReplaceInventory` compatibility is deliberately narrow: SPT/EFT runtime members `Inventory`, `Equipment`, `ContainedItem`, `Deleted` and `StringTemplateId` are resolved once during patch installation as a property or field, cached delegates are generated, and the postfix inspects only the three wearable equipment slots. There is no reflection scan per replacement, no scene/inventory sweep and no idle polling.

Only wearable descriptors with the explicit Scav-host restoration capability can clear the transient deleted flag. Unrelated items and unregistered templates fail closed. Scav compatibility is CI-owned and is not part of the user's physical acceptance matrix.

## Profile and uninstall safety

B&A&HB persistent data is treated as a lifecycle contract, not only a runtime feature. The package includes:

- one authoritative persistent-identity manifest covering current distributed template, parent, slot, grid and assort IDs;
- backup-first ownership-scoped recovery tooling;
- deterministic cleanup regression coverage for stash/equipment, mail, insurance, build references and descendants while B&A&HB templates are absent;
- preservation of unrelated profile data and idempotent repeated cleanup;
- documented update/disable/uninstall recovery procedure packaged with the candidate.

Do not manually delete arbitrary profile nodes. Use the packaged recovery contract when disabling or uninstalling a build that has already written B&A&HB identities to a profile.

## Performance contract

Production client behavior is interaction/event driven:

- no `ItemView.Update` polling;
- no permanent production `MonoBehaviour.Update` loop;
- no scene-wide object scans;
- no hierarchy-wide polling;
- no repeated reflection in guarded hot paths where startup-bound delegates can be used;
- deferred GridWindow work is bounded and drains to zero;
- HeadBand visual creation is owned by native `EquipmentTab.Show` / `SlotView.Show` boundaries;
- Belt caption repair is bounded to the exact factory-created row at `ContainersPanel.Show` completion;
- server lifecycle target discovery is bounded and fails closed on zero/ambiguous candidates.

CI includes deterministic hot-path, product-contract and runtime-contract guards.

## Repository layout

- `src/` — client runtime registration, dedicated equipment projection and wearable integrations;
- `server/` — SPT 4.1.3 item, slot, trader and lifecycle integration;
- `tests/` — deterministic profile, identity, routing, presentation and lifecycle regressions;
- `tools/` — validation, product-contract and profile-recovery tooling;
- `docs/` — architecture, runtime acceptance and recovery documentation.

## Compatibility

Pack 'n' Strap and Trenchfoot BeltSlot are reference/archaeology sources, not runtime dependencies. If legacy `Trenchfoot-BeltSlot.dll` is installed, remove or disable it before using B&A&HB so two implementations do not patch the same host behavior.

The server project targets SPT 4.1.3 `SPTushonka.*` packages.

## Development boundary

PR #64 remains the single implementation/evidence record. Stable Baseline 1 is the rollback anchor for the accepted gameplay base. Product-pass changes must preserve persistent identities and the accepted runtime lifecycle; CI must prove the exact product roster and full client/server build before a new artifact is handed off.

The deferred final HeadBand visual concept is intentionally separate: reduce the Face window roughly by half and place a compact HeadBand above/adjacent using the ArmBand + Dogtag visual principle. The possible two-independent-`1x1` HeadBand layout (cigarettes-only + currency/wallet-only) is a final feasibility task, not part of the stable-base productization pass.
