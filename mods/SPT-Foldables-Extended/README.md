# Admiral Foldables Extended

Optional add-on for [ozen-m/SPT-Foldables](https://github.com/ozen-m/SPT-Foldables). Foldables 1.1.1 or newer remains a required dependency and is not redistributed.

## Stage 1

- every 2x2 poster folds to 1x1, including the complete Flyer category, mod-added descendants and the four Arena quest poster packs;
- body armor and armored rigs use quarter-area folded geometry:
  - 3x4 -> 3x1;
  - 4x4 -> 2x2;
  - 3x5 -> 4x1;
- all four-cell headwear folds to 1x2 vertically;
- every multi-cell face cover is foldable: two-cell masks fold to 1x1 and four-cell masks fold to 1x2; one-slot masks remain unaffected;
- ordinary grid contents and every occupied unlocked slot are spilled through EFT inventory transactions before folding;
- locked/integrated armor slots remain attached and do not block folding;
- folded items keep their durability, integrated armor and original template identity.

The add-on does not edit or replace Foldables files. It registers additional compatible item types and adds the removable-slot spill boundary that armor requires.

## Build

Build the client with `SPTPath` pointing at an SPT 4.1.x installation containing Foldables 1.1.1. Build the server and tests normally with .NET 10.

## Attribution

Foldables concepts and public extension types are provided by ozen-m under the MIT License. This add-on keeps Foldables as an external dependency and does not redistribute its binaries or assets.
