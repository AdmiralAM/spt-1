# Artem Revival MOD SPT

Revival and compatibility workstream for **WTT-Artem** targeting **SPT 4.1.3**.

This module preserves Artem's authored trader, campaign, quest progression and unique equipment while bringing the runtime implementation and content package onto the SPT 4.1.x/.NET 10 stack.

## Scope

- port the server mod lifecycle and metadata to the SPT 4.1.x API;
- target SPT 4.1.3 and the compatible WTT-ServerCommonLib 3.x line;
- preserve the original Artem trader and quest campaign unless a concrete compatibility defect requires repair;
- validate trader assort, quest unlocks, custom items, clothing, zones, locales, images and bundle manifest references;
- repair broken or stale content references rather than silently dropping content;
- audit economy values and rewards after functional compatibility is proven;
- classify large cosmetic/asset content into Core vs Optional candidates without automatically removing authored content;
- keep Artem-specific gear out of PBS pools unless explicitly designed and reviewed later.

## Independence contract

`mods/WTT-Artem-Revival/` is a standalone workstream. It must not depend on Item Intelligence, Quest Planner, Belt/Armband Inventory, Pause or Tactical HUD internals.

Quest Planner and Item Intelligence may later consume Artem's ordinary SPT quest/item state through their normal data paths, but Artem must not require those mods to function.

## Source of truth

The revival uses two references:

1. the complete archived Artem runtime package supplied for archaeology and asset validation;
2. the upstream `WelcomeToThursday/WTT-Artem` source as the cleanest available 4.1-era loader baseline.

The archived package remains authoritative for the user's complete content set. Upstream code is a reference/porting baseline, not permission to discard archived content that is absent upstream.

## Current archaeology baseline

- archived runtime DLL is a SPT 4.0/.NET 9-era build and cannot be reused as-is on SPT 4.1.3;
- upstream loader already uses `IModMetadata`, `IOnLoad.OnLoadAsync` and `net10.0`;
- 23 Artem quests form an acyclic dependency graph with no missing prerequisite quest IDs;
- all 239 bundle-manifest entries are present in the supplied six-part Bundles archive set;
- known content defects include a mismatched quest thumbnail extension and at least one dangling quest assortment unlock;
- physical bundle extras/duplicates are tracked for later cleanup and Core/Optional classification, not treated as startup blockers.

## Repository layout

```text
mods/WTT-Artem-Revival/
├─ README.md
├─ docs/
│  ├─ revival-status.md
│  └─ content-audit.md
├─ src/          # real 4.1.3 loader/source when imported
├─ Resources/    # curated Artem runtime data/assets tracked where practical
└─ tests/        # structural/content regression tests
```

Empty implementation directories are intentionally not committed. They appear only when real ported files exist.

## Revival gates

1. **Loader gate** — clean SPT 4.1.3 startup with correct metadata/dependencies.
2. **Content gate** — trader, quests, items, clothing, zones, locales and images load without missing references.
3. **Bundle gate** — manifest references resolve and no required bundle is missing.
4. **Progression gate** — quest prerequisites, rewards and assortment unlocks are internally consistent.
5. **Economy gate** — rewards/assort pricing are reviewed against the project economy after compatibility is stable.
6. **Packaging gate** — Core/Optional assets are separated only where doing so cannot break authored progression.

See `docs/revival-status.md` and `docs/content-audit.md` for the live technical record.
