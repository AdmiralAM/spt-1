# Economy Admiral packaging contract

Target runtime: SPT 4.1.3.

Economy Admiral is a server mod loaded from the SPT runtime mod directory. The release/test artifact MUST be a root-relative drop-in package whose top-level directory is `user` and whose assembly is located at:

`user/mods/Economy Admiral/Economy-Admiral.dll`

The package MUST NOT add an extra wrapper directory above `user`, and MUST NOT nest another `Economy Admiral` directory inside the mod folder.

CI must validate the staged package with `tools/Validate-PackageLayout.ps1` before upload. A package lacking a DLL in the exact mod root is invalid even if the .NET build itself succeeded.

Physical evidence from 2026-08-26 showed SPT 4.1.3 loading working Economy Admiral from `C:\Games\SPT\SPT_Runtime\user\mods\Economy Admiral` and later rejecting a malformed test package with `No Assemblies found in path` at that exact location. This contract exists to prevent recurrence.
