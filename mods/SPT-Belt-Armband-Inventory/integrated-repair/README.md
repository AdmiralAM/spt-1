# B&A&HB primary-client repair RC2

This supersedes the separate operational-hotfix RC1 delivery for this user's request. The delivery is one replacement `SPT Belt Armband Inventory v0.1.0.dll`, still one BepInEx plugin. No `BAndHB.OperationalAccess.dll` or new runtime dependency is installed.

The original user-supplied client is hash-pinned to `ca2774ba4fc6cc1183863b8f008916f40a78e2b517e4934344286e233c26453a`. The offline build integrator copies the compiled integration type into that primary assembly, replaces the failed reachability/candidate install boundaries, and adds ownership cleanup to the existing Plugin.OnDestroy. Other original method bodies and embedded assets are fingerprint-verified unchanged. Imported private PackNStrap types stay in the primary DLL. No private binary, third-party source or asset is committed here.

Run the self-contained build integrator from its build-kit directory, where the exact netstandard2.1 reference assemblies are supplied for Cecil enum-constant resolution:

```
./BAndHB.Integrator original-client.dll BAndHB.Integration.BuildInput.dll output-client.dll <exact-source-sha>
```

This is a developer build operation, not an installation step assigned to the user. Package only the resulting primary DLL and its provenance. Never copy reference assemblies or the build input DLL into BepInEx.

The native-first implementation traverses only storage grids of the actually equipped Belt; nested pouches are bounded by depth and count. Weapon slots are not traversed. Unknown roots and malformed/cyclic trees fail closed. Candidate and grenade list results are copied rather than mutating native lists. The integration no longer relies on discovering a unique GetAllParentItems extension or a unique StringTemplateId declaration across the whole inheritance chain. Native slot-array installation, the existing server, private imports, UI, configured Use Items Anywhere integration, and persistent identities remain as supplied.

Validation comprises production-code fixture execution, emitted typed-postfix execution, build, and private output structural/provenance verification. Fixtures do not establish native Harmony detour success or raid behavior. The official SPT-only 4.1.5 archive was SHA-256 verified in run 34854814206 but contained no Assembly-CSharp.dll; therefore no native EFT signature test is claimed. The exact internal discovery condition that caused the original generic reachability warning remains unproven without the native client assemblies.

Installation: stop game; preserve original client outside active plugin paths; replace only the primary DLL; remove RC1 overlay if installed. Server, bundles, databases, profile and configuration remain untouched. Rollback restores the original client. RC2 is not stable promotion and does not resolve the separate protection-acknowledgement warning.
