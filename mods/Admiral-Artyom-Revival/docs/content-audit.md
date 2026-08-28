# Admiral Artyom Revival — Content Audit Baseline

This file records structural findings from the archived upstream WTT-Artem runtime package before economy rebalance or optional-content pruning.

## Archived source set

The supplied upstream mod is physically split only for upload/storage. Treat it as one logical source set:

```text
WTT-Artem/
├─ core/base runtime package
└─ Bundles/
   ├─ Hands
   ├─ Pants
   ├─ Tops
   ├─ bundle group 1
   ├─ bundle group 2
   └─ bundle group 3
```

The six bundle archives must never be interpreted as six independent mods.

## Content inventory

Archived runtime counts:

- custom item definitions: 131;
- custom clothing definitions: 64;
- quests: 23;
- quest dependency edges: 22;
- trader assort item records: 702;
- trader root offers: 280;
- quest-success assortment mappings: 38;
- bundle manifest entries: 239.

All 280 archived root offers have barter schemes and loyalty levels.

After repair R4 (restored Sweden Patch offer), Admiral Artyom Revival's maintained assort becomes 703 item records / 281 root offers.

## Quest graph

Result:

- no missing prerequisite quest IDs;
- no dependency cycles detected;
- authored chain is structurally recoverable and is preserved during the compatibility port.

### Proven quest/content repairs

#### Thumbnail extension mismatch

Quest `673f0f4d219756e158de7ab3` (`An Eye for an Eye`) references `ARTT_3thumbnail.jpg`, while the supplied asset is `ARTT_3thumbnail.png`.

Repair: normalize only this quest image reference to the existing `.png` asset.

#### Missing Sweden Patch assortment offer

`Expanding Wardrobe` contains an `AssortmentUnlock` targeting offer ID `675267324707588d57c75972` with item template `6752641b1470fc33b675d59a`.

The TPL exists and is the authored **Sweden Patch** custom item, but the corresponding root trader offer is absent from archived `assort.json`. Adjacent quest-unlocked patches use a consistent LL1 offer pattern.

Repair: restore the missing authored offer rather than deleting the quest reward:

- offer ID: `675267324707588d57c75972`;
- TPL: `6752641b1470fc33b675d59a`;
- price: 200 RUB;
- stock: 500;
- buy limit: 3;
- loyalty level: 1.

This repair is implemented deterministically by `tools/import_runtime_core.py` and guarded by `tests/validate_content.py`.

## Bundle integrity

Manifest-to-physical resolution uses the **exact manifest path**, not basename-only matching.

- manifest entries: 239;
- required exact paths physically present: 239;
- missing required bundles: 0;
- physical `.bundle` files across the six archives: 262;
- physical files outside the manifest: 23.

Therefore bundle absence is not a startup-root-cause candidate.

### Cleanup candidates

The 23 out-of-manifest physical files divide into:

- 20 files whose basenames are not referenced anywhere by `bundles.json`;
- 3 path-collision copies in `Hands/`: `artem_top_29.bundle`, `artem_top_30.bundle`, `artem_top_31.bundle`.

The manifest selects the corresponding `Tops/artem_top_29.bundle`, `Tops/artem_top_30.bundle`, and `Tops/artem_top_31.bundle` files. The `Hands/` copies differ in size/CRC and remain retained for provenance review until runtime packaging tests confirm they are stale.

No bundle is deleted merely because it is currently outside the manifest.

## Classification policy

Every upstream Artem asset/content record should eventually receive one of these classifications:

- **Core** — required for trader/campaign/runtime identity;
- **Campaign-required** — directly or transitively required by quest progression/rewards/unlocks;
- **Optional cosmetic** — removable package weight with no progression/runtime dependency;
- **Orphan/stale** — unreferenced and safe to remove after validation;
- **Needs review** — ambiguous ownership or duplicate mapping.

Nothing is moved into Optional or deleted solely because it is large.

## Economy policy

Structural compatibility comes first. Existing reward and assort values are recorded during archaeology but are not normalized until loader/content/runtime gates pass.

Artem custom gear is not automatically inserted into PBS loot/equipment pools.
