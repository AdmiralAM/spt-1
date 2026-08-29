# Economy Admiral player package contract

The install artifact is a player/runtime package, not a development bundle.

Required packaged files:

- `SPT_Runtime/user/mods/Economy Admiral/Economy-Admiral.dll`
- `SPT_Runtime/user/mods/Economy Admiral/config/config.default.json`
- `SPT_Runtime/user/mods/Economy Admiral/README.md`
- `BepInEx/plugins/Economy Admiral/Economy Admiral v0.1.0.dll`
- root `runtime-manifest.json`

`config.json` is runtime user state and must **not** be included in an install/update artifact. On first start, Economy Admiral validates `config.default.json` and creates `config.json` only when no user config exists. Later installs therefore cannot overwrite F12/user settings merely by extracting a newer package over SPT.

Development-only files such as `RUNTIME_TEST.md` and `Validate-*.ps1` remain in the source repository and must not be copied into the player artifact.

The package must never contain BepInEx core/config/patcher runtime files or root bootstrap files such as `winhttp.dll` and `doorstop_config.ini`.
