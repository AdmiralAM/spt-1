# Artem Revival — SPT 4.1.3 Status

## Target

Restore the complete archived WTT-Artem content set as a stable SPT 4.1.3 server mod while preserving the authored trader/campaign identity.

## Confirmed compatibility break

The archived runtime DLL is from the SPT 4.0 generation:

- .NET 9 target;
- legacy `AbstractModMetadata` model;
- legacy synchronous `IOnLoad.OnLoad` lifecycle.

That binary is not a viable SPT 4.1.3 runtime artifact.

The revival rebuilds the loader for SPT 4.1 using:

- `IModMetadata`;
- `IOnLoad.OnLoadAsync(CancellationToken)`;
- .NET 10;
- WTT-ServerCommonLib 3.0.4 build dependency with `~3.0.0` runtime metadata compatibility.

## Current result

A first SPT 4.1.3 revival candidate now exists.

Confirmed evidence:

- GitHub Actions .NET 10 server build: **PASS**;
- server DLL artifact produced: `WTT-Artem.dll`;
- deterministic import of authoritative `artem main 1.zip`: **PASS**;
- legacy 4.0 DLL excluded from imported Resources;
- repaired core static integrity: **PASS**;
- quests: 23;
- prerequisite edges: 22;
- quest dependency cycles: 0;
- repaired trader root offers: 281;
- repaired assort item records: 703;
- missing quest images after repair: 0;
- dangling quest `AssortmentUnlock` targets after repair: 0;
- bundle manifest exact-path resolution: 239/239 present.

The current candidate is a **core overlay**: it contains the rebuilt 4.1 server DLL and repaired core/base runtime data. The existing six-archive Artem Bundles set remains unchanged and must be retained for runtime testing.

## Work phases

### Phase 0 — Archaeology

Status: **complete for compatibility baseline**

- archived runtime package tree inventoried;
- trader DB, custom items, clothing, quests, zones, locales and bundle manifest identified;
- six supplied bundle archives mapped as one logical `Bundles/` directory;
- upstream 4.1 loader used as migration reference.

### Phase 1 — Structural validation

Status: **pass for first candidate**

- quest graph structurally valid;
- all 239 manifest bundle paths physically present;
- trader assort/barter/loyalty invariants valid after repair;
- quest image mismatch repaired;
- Sweden Patch quest unlock repaired by restoring the missing authored trader offer.

### Phase 2 — 4.1.3 loader

Status: **build pass / runtime pending**

Implemented:

- isolated .NET 10 server project;
- SPT 4.1 lifecycle and DI model;
- WTT-ServerCommonLib 3.0.4 dependency;
- CI build and artifact publication;
- deterministic core import tool;
- integrity validator.

Remaining gate: actual SPT 4.1.3 server boot with CommonLib and the reconstructed Artem package.

### Phase 3 — Runtime/content repairs

Status: **static repairs complete; runtime evidence pending**

No additional content repair is authorized until SPT runtime logs or campaign testing proves another defect.

### Phase 4 — Economy review

Status: intentionally deferred until runtime/campaign gates pass.

### Phase 5 — Core / Optional assets

Status: intentionally deferred.

Exact bundle audit currently finds 23 physical files outside the manifest: 20 unique orphan-basename candidates plus three `Hands/` path-collision copies of `artem_top_29/30/31.bundle`. Nothing has been deleted.

## Next hard gate

Install the first candidate into SPT 4.1.3 with WTT-ServerCommonLib 3.0.4 and the existing Artem `Bundles` folder, then collect server startup/runtime evidence for:

1. mod discovery/dependency resolution;
2. custom item creation;
3. quest-zone creation;
4. trader registration;
5. custom quest creation;
6. clothing creation;
7. assort overwrite;
8. client-side bundle resolution.

The revival is not merge-ready until this runtime gate passes.
