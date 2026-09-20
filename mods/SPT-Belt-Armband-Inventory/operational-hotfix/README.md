# B&A&HB operational access hotfix — RC1

User-requested repair of the installed private v0.3.0 client. This is a small client-side overlay, not a replacement stable distribution and not a new wearable product. The existing base client, server, configuration, imported databases, bundles, persistent IDs and profiles are not replaced or uploaded.

## Exact input and installation

Required base client SHA-256: `ca2774ba4fc6cc1183863b8f008916f40a78e2b517e4934344286e233c26453a`.
The loaded base DLL must match this hash; otherwise the overlay disables itself with a diagnostic rather than applying to an unknown build. The supplied server hash was `f769b100df87a82aa802bed711ac408754443612e4cdf343a891542e024fee92`; this patch does not replace it.

Stop the game. Copy `BepInEx/plugins/BAndHB.OperationalAccess.dll` from the artifact into that same relative path under the existing SPT installation. Keep `SPT Belt Armband Inventory v0.1.0.dll` and the whole existing server mod unchanged. No new bundles or server files are needed. Rollback: stop the game and remove only `BAndHB.OperationalAccess.dll`.

## Behavior and limits

- Appends contents of a supported belt actually equipped in slot15 to native fast-access/binding/explicit-Belt queries. Keeps the complete vanilla priority prefix.
- Traverses storage grids inside belt pouches. Does not traverse weapon/mod slots or cartridges installed in weapons and magazines.
- Promotes otherwise-unreachable items only along a proven storage-grid parent chain back to the belt equipped in the same controller's inventory. A belt in stash/backpack and detached pouches do not qualify.
- Appends examined belt grenades through the native throwable-priority-list boundary, retaining native grenade order and examination checks.
- Depth 8, 256 descendant items and 1024 traversal edges are hard safety bounds. Cycles, unexpected shapes and ambiguous native bindings fail closed.
- No profile writes, static slot-array mutation, per-frame scan, permanent coroutine, global UI mutation or third-party configuration rewrite.
- Existing Use Items Anywhere configuration remains authoritative. This overlay does not enable disabled foreign modules.

The base mod's own existing behavior is not removed. This overlay repairs missing access paths; it does not certify every behavior already present in the supplied modified binary.

## Validation status

CI builds the actual netstandard2.1 DLL and runs the shared-policy deterministic tests. This is not a physical SPT test and not proof that native runtime binding succeeds. The package remains an RC until the installed SPT 4.1.5 gate below passes. It does not claim compatibility with other base DLL hashes.

## One batched physical gate

1. Startup: log contains `B&A&HB operational.1 ACTIVE`, not `DISABLED`.
2. Compatible magazine only on the equipped belt, then in a belt pouch: normal reload finds it; native rig/pocket sources retain priority.
3. Loose compatible ammunition directly on belt, then in a pouch: the applicable native loading action sees and consumes it once. Test a weapon that natively loads loose rounds separately from magazine loading.
4. Examined grenade only in a belt pouch: grenade selection/throwing works and consumes exactly one grenade.
5. Medicine only in a belt pouch: the intended binding/use action works under the current Use Items Anywhere configuration and consumes normal charges.
6. Move the pouch off the belt or remove the belt: the overlay no longer grants its access. Native or separately configured Use Items Anywhere access may still apply.

Return numbered results and, on failure, only log lines containing `operational.1` plus the failing action. Do not run profile cleanup as part of this hotfix.
