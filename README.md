# Artem Revival MOD SPT runtime

Stable runtime baseline **r5-RU-compat** for **SPT 4.1.3**.

Validated with WTT Server/Client CommonLib **3.0.6**. Source was integrated through PR #68 at commit `b1a93bdd2c08e00fc88af3fb8aed0a72d160af96`.

## What this channel preserves

This branch is the permanent install/runtime identity for the accepted Artem revival and contains the validated SPT 4.1.3 server DLL plus an immutable runtime manifest.

The authored Artem database/assets are not duplicated here. They are preserved by the authoritative archived Artem core source set and the deterministic import/repair/localization tooling in `main` under `mods/WTT-Artem-Revival/`. The accepted complete runtime candidate is `r5-RU-compat` and its SHA-256 is recorded in `runtime-manifest.json`.

## Runtime location

The validated DLL belongs at:

```text
SPT_Runtime/user/mods/WTT-Artem Revival/WTT-Artem.dll
```

The installed Artem directory also retains the repaired r5 core data and the external `Bundles/` set used during validation.

## External assets

The approximately 1.5 GB authored Unity `Bundles/` payload is intentionally not committed to Git. Keep the already assembled Artem `Bundles/` directory made from the six supplied bundle archives. All **239/239** manifest bundle paths were validated.

Do **not** restore the legacy SPT 4.0 `WTT-Artem.dll`; the DLL in this runtime branch is the stable SPT 4.1.3 build.

For development/reconstruction details, use `main:mods/WTT-Artem-Revival/` and PR #68. This branch is not a development branch.
