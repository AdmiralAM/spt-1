# Artem Revival — SPT 4.1.3 Status

## Target

Restore the complete archived WTT-Artem content set as a stable SPT 4.1.3 server mod while preserving the authored trader/campaign identity.

## Confirmed compatibility break

The archived runtime DLL is from the SPT 4.0 generation:

- .NET 9 target;
- legacy `AbstractModMetadata` model;
- legacy synchronous `IOnLoad.OnLoad` lifecycle.

That binary is not a viable SPT 4.1.3 runtime artifact.

The current upstream Artem source already demonstrates the required 4.1-era direction:

- `IModMetadata`;
- `IOnLoad.OnLoadAsync(CancellationToken)`;
- `net10.0`;
- WTT-ServerCommonLib 3.x dependency.

The revival therefore ports/rebuilds the loader rather than attempting to patch the archived DLL.

## Work phases

### Phase 0 — Archaeology

Status: **active / substantially mapped**

- archived runtime package tree inventoried;
- trader DB, custom items, clothing, quests, zones, locales and bundle manifest identified;
- six supplied bundle archives treated as one logical `Bundles/` directory;
- upstream 4.1-era loader inspected as migration reference.

### Phase 1 — Structural validation

Status: **active**

- quest graph: 23 quests, no missing prerequisite quest IDs, no cycles;
- bundle manifest: 239 entries, all 239 physically present in supplied bundle archives;
- trader assort root/barter/loyalty structure mapped;
- known dangling quest assortment unlock under investigation;
- known quest thumbnail extension mismatch identified.

### Phase 2 — 4.1.3 loader

Status: **next implementation gate**

Required:

- import a clean .NET 10 project under this module;
- use the SPT 4.1 lifecycle and DI model;
- pin/validate the compatible WTT-ServerCommonLib dependency;
- load archived Artem content without silently replacing it with a smaller upstream content snapshot;
- add explicit startup diagnostics around trader/items/quests/clothing/zones.

### Phase 3 — Runtime/content repairs

Status: pending loader gate

Repair only defects proven by structural or runtime evidence. Do not redesign campaign progression during compatibility work.

### Phase 4 — Economy review

Status: intentionally deferred

Only after the functional port is stable:

- trader prices;
- quest cash/item rewards;
- unlock pacing;
- resale/flea implications;
- compatibility with the project's broader economy plan.

### Phase 5 — Core / Optional assets

Status: intentionally deferred

Large cosmetics may become optional only after dependency tracing proves they are not required by quests, trader progression, item definitions, clothing records or mandatory bundle references.

## Stop conditions

A compatibility issue is not considered fixed because the server merely starts. Revival requires evidence across loader, content, progression and asset gates.
