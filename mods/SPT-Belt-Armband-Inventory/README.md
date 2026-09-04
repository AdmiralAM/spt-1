# B&A&HB #2 MOD SPT

Stable version **0.1.0** for **SPT 4.1.4**.

- [Download the stable ZIP](https://github.com/AdmiralAM/spt-1/archive/refs/heads/runtime-belt-armband.zip)
- [View the install-ready package](https://github.com/AdmiralAM/spt-1/tree/runtime-belt-armband)
- [View the v0.1.0 source](https://github.com/AdmiralAM/spt-1/tree/bahb-v0.1.0)

No additional mods or libraries are required.

## What the mod adds

- **ArmBand** accessories with usable inventory, including Wrist Wallet support.
- A dedicated **Belt** equipment slot with a `2x2` magazine container.
- A dedicated **HeadBand** equipment slot with a compact `1x2` utility container.
- Independent F12 settings for ArmBand, Belt and HeadBand loss on death.

## Installation

1. Close the game, launcher and SPT server.
2. Download and unpack the stable ZIP.
3. Open the included `SPT_Runtime` directory.
4. Copy its **contents** into the existing `SPT_Runtime` directory of your SPT 4.1.4 installation.
5. Start the SPT server and launch the game normally.

The package installs both required components:

- `SPT_Runtime/BepInEx/plugins/SPT Belt Armband Inventory v0.1.0.dll`
- `SPT_Runtime/user/mods/B&A&HB #2 MOD SPT/SPT-Belt-Armband-Inventory.Server.dll`

If `Trenchfoot-BeltSlot.dll` is installed, remove it before using B&A&HB because both mods change the same equipment area.

## Updating

To update, replace both DLLs with the files from the stable ZIP.
