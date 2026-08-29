# Economy Admiral player package contract

The install artifact is a player/runtime package, not a development bundle.

Required files:

- `SPT_Runtime/user/mods/Economy Admiral/Economy-Admiral.dll`
- `SPT_Runtime/user/mods/Economy Admiral/config/config.json`
- `SPT_Runtime/user/mods/Economy Admiral/README.md`
- `BepInEx/plugins/Economy Admiral/Economy Admiral v0.1.0.dll`
- root `runtime-manifest.json`

Development-only files such as `RUNTIME_TEST.md` and `Validate-*.ps1` remain in the source repository and must not be copied into the player artifact.

The package must never contain BepInEx core/config/patcher runtime files or root bootstrap files such as `winhttp.dll` and `doorstop_config.ini`.
