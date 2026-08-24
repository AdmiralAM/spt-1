# Artem Revival MOD SPT runtime

Stable install-only **r5-RU-compat** overlay for **SPT 4.1.3**.

Validated with WTT Server/Client CommonLib **3.0.6**. Source was integrated through PR #68 at commit `b1a93bdd2c08e00fc88af3fb8aed0a72d160af96`.

## Install

Download `Artem-Revival-SPT-4.1.3-r5-stable-overlay.zip` from this branch and extract it into the SPT root with replacement.

It updates:

`SPT_Runtime/user/mods/WTT-Artem Revival/`

## External Bundles requirement

The authored Unity `Bundles/` payload is intentionally **not included** in this Git runtime channel. Keep the already assembled Artem `Bundles/` directory made from the six supplied bundle archives. All **239/239** manifest bundle paths were validated.

The overlay also assumes the unchanged original Artem quest-image/avatar assets are already present from the base Artem core installation. Normal revival updates replace the DLL and maintained JSON/core data without duplicating the large asset payload.

Do **not** restore the legacy SPT 4.0 `WTT-Artem.dll`; use the DLL in this stable overlay.
