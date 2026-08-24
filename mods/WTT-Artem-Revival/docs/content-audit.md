# Artem Revival — Content Audit Baseline

This file records structural findings from the archived Artem runtime package before any economy rebalance or optional-content pruning.

## Archived source set

The supplied mod is physically split only for upload/storage. Treat it as one logical mod:

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

Current archaeology counts:

- custom item definitions: 131;
- custom clothing definitions: 64;
- quests: 23;
- quest dependency edges: 22;
- trader assort item records: 702;
- trader root offers: 280;
- quest-success assortment mappings: 38;
- bundle manifest entries: 239.

All 280 root offers have barter schemes and loyalty levels in the archived assort structure.

## Quest graph

Result:

- no missing prerequisite quest IDs;
- no dependency cycles detected;
- authored chain is structurally recoverable and should be preserved during the compatibility port.

### Known quest defects

#### Thumbnail extension mismatch

`An Eye for an Eye` references:

`ARTT_3thumbnail.jpg`

The supplied asset is:

`ARTT_3thumbnail.png`

Repair direction: normalize the quest image reference to the real asset unless runtime evidence proves an alternate image route exists.

#### Dangling assortment unlock

`Expanding Wardrobe` contains an `AssortmentUnlock` targeting offer ID:

`675267324707588d57c75972`

with item template:

`6752641b1470fc33b675d59a`

The referenced offer ID is absent from the archived `assort.json` inventory. Do not substitute an arbitrary offer. Resolve the intended item/offer relationship first, then repair the reward or assort data with regression coverage.

## Bundle integrity

Manifest-to-physical resolution:

- manifest entries: 239;
- required entries physically present: 239;
- missing required bundles: 0.

Therefore bundle absence is not currently a startup-root-cause candidate.

### Cleanup candidates

There are physical bundle files not registered by the current manifest. These are retained until dependency tracing is complete.

Known duplicate basenames include:

- `artem_top_29.bundle`;
- `artem_top_30.bundle`;
- `artem_top_31.bundle`.

Copies occur across supplied bundle groups and differ in file size. They require content/dependency comparison before any deletion.

## Classification policy

Every Artem asset/content record should eventually receive one of these classifications:

- **Core** — required for trader/campaign/runtime identity;
- **Campaign-required** — directly or transitively required by quest progression/rewards/unlocks;
- **Optional cosmetic** — removable package weight with no progression/runtime dependency;
- **Orphan/stale** — unreferenced and safe to remove after validation;
- **Needs review** — ambiguous ownership or duplicate mapping.

Nothing is moved into Optional or deleted solely because it is large.

## Economy policy

Structural compatibility comes first. Existing reward and assort values are recorded during archaeology but are not normalized until the loader/content gates pass.

Artem custom gear is not automatically inserted into PBS loot/equipment pools.
