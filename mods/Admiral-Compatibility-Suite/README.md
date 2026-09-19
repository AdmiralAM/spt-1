# Admiral Compatibility Suite

One user-facing compatibility package for Admiral extensions of third-party SPT
mods. Components remain isolated internally so one upstream update cannot disable
the whole suite.

## Ownership boundary

- Item Intelligence owns its optional Amands Sense presentation.
- Admiral Trader owns Painter, Artem and TGC storefront/quest consolidation.
- Compatibility Suite owns Foldables Extended, Stackable Armor Plates, fixes to
  third-party behavior, and cross-mod adapters currently embedded in Belt.
- Belt keeps its native Belt/ArmBand/HeadBand product behavior. Its UI Fixes,
  Use Items Anywhere, TGC and Pack 'n' Strap adapters are migration candidates.

`manifests/compatibility-ownership.json` is the machine-readable authority. The
audit tool inventories dependencies, Harmony patches, reflection, template
mutation and file/config replacement without treating every reflection use as a
foreign integration.

## Initial components

- `SPT-Foldables-Extended`
- `SPT-Stackable-Armor-Plates`

They retain their existing GUIDs and runtime paths while being distributed and
versioned as Suite components.
