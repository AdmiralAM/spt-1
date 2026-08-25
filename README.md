# Admiral Artyom Revival runtime

Stable validated runtime baseline **r5-RU-compat** for **SPT 4.1.3**. Official maintained product name: **Admiral Artyom Revival**. Module version: **3.0.0**.

Validated with WTT Server/Client CommonLib **3.0.6**. The accepted runtime candidate predates the repository-wide product rename; its compiled `WTT-Artem.dll` filename and original source commit remain retained technical provenance identifiers until a separately runtime-validated renamed binary is deliberately promoted.

## What this channel preserves

`runtime-artem-revival` is the permanent publication compatibility identifier for the accepted Admiral Artyom Revival runtime. It contains the physically validated SPT 4.1.3 server DLL plus an immutable validation manifest.

The authored upstream WTT-Artem database/assets are not duplicated here. They are preserved by the authoritative archived source set and deterministic import/repair/localization tooling maintained in `main` under `mods/Admiral-Artyom-Revival/`. The accepted complete runtime candidate is `r5-RU-compat`; its SHA-256 remains recorded in `runtime-manifest.json`.

## Runtime location

For the already validated r5 candidate, retain the proven technical layout:

```text
SPT_Runtime/user/mods/WTT-Artem Revival/WTT-Artem.dll
```

That legacy folder/DLL identity is a compatibility detail of the accepted r5 binary, **not** the current product name. Do not rename or replace the validated binary in an existing installation merely for cosmetics. A future deliberately promoted candidate may adopt the new `Admiral Artyom Revival` binary/folder identity after the required runtime gate passes.

The installed module directory also retains the repaired r5 core data and the external `Bundles/` set used during validation.

## External assets

The approximately 1.5 GB authored Unity `Bundles/` payload is intentionally not committed to Git. Keep the already assembled Artem `Bundles/` directory made from the six supplied bundle archives. All **239/239** manifest bundle paths were validated.

Do **not** restore the legacy SPT 4.0 `WTT-Artem.dll`; the DLL pinned in this runtime branch is the accepted SPT 4.1.3 r5 build.

For current development/reconstruction details use `main:mods/Admiral-Artyom-Revival/`. This branch is publication/runtime state, not a development branch.
