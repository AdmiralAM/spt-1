# Admiral Artyom Revival — Core / Campaign / Optional Classification

Classification is dependency-driven and intentionally conservative. No content is removed by this document.

## Custom items

Authoritative repaired core contains **131** custom item templates.

### Campaign-required — 27

27 custom templates are referenced directly by the 23-quest campaign. They are non-optional for the full revival profile.

Current family distribution:

- patches: **20**;
- masks: **5**;
- misc: **2**.

These templates and their required bundles must remain in the Core/campaign package unless the campaign itself is deliberately redesigned.

### Core trader catalog — 100

100 additional custom templates are not referenced directly by quests but are root offers in Artem's trader assort. They form the functional/cosmetic trader catalog and are retained in the default full revival.

Family distribution:

- helmets: **40**;
- patches: **21**;
- masks: **19**;
- vests/plate carriers: **16**;
- backpacks: **2**;
- misc: **2**.

This category is a later source for an optional/light package only after runtime and campaign tests prove that removing an item does not break mod-slot filters, assort children, clothing, presets or other indirect references.

### Orphan/stale candidates — 4

Four custom templates have no quest reference, no root trader offer and no reference from another custom item or assort record in the repaired core:

- `66bf757f27d0b097db0ace6c` — **Solaire Figure**;
- `6673b1ac5cae0610f1079d76` — **Denis' Collar**;
- `66bf757f27d0b097db0ace69` — **Carved Headcrab Trophy**;
- `66bf757f27d0b097db0ace6a` — **Crackbone Figure**.

They are candidates, not deletions. Keep them until a 4.1.3 runtime/package smoke test confirms that no loader-side or external authored behavior depends on them.

## Clothing and Unity bundles

`bundles.json` declares **239** required bundle paths and all 239 exist in the supplied six-archive Bundles source set.

Only **131** manifest assets are directly referenced through item/clothing prefab paths. The other **108** manifest entries are largely companion Hands/Pants/Tops assets and cannot be called orphan merely because a direct item prefab scan does not name them.

Therefore:

- every manifest-declared bundle remains **Core** for the first stable revival;
- the previously identified 23 physical files outside the manifest remain cleanup candidates;
- duplicate physical `artem_top_29/30/31` copies remain provenance-review items;
- an Optional Cosmetics package must be created only by tracing clothing top/bottom/hands relationships as complete sets, not by deleting individual bundles by size or basename.

## Packaging profiles — future

After runtime/campaign verification, the intended packaging split is:

- **Full** — complete authored upstream Artem content maintained by Admiral Artyom Revival;
- **Core** — trader + campaign + all transitive asset dependencies;
- **Optional Cosmetics** — proven removable clothing/cosmetic sets;
- **Removed/Archive** — proven stale/orphan physical assets only.

The first stable 4.1.3 candidate remains **Full**. This avoids silently changing the upstream Artem content identity while compatibility is still being proven.
