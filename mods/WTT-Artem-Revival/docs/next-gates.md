# Artem Revival remaining gates

The revival is considered ready for merge only when all gates below are satisfied.

1. **Server build** — .NET 10 server project restores and builds cleanly against the SPT 4.1/CommonLib dependency line.
2. **Core import** — authoritative `artem main 1.zip` imports without the legacy DLL and produces the expected runtime layout.
3. **Content integrity** — quest graph, quest images, quest assort, trader assort, barter schemes, loyalty levels, and known campaign unlocks pass automated validation.
4. **Bundle integrity** — every path declared in `bundles.json` resolves to exactly one selected physical bundle in the reconstructed bundle set; duplicate basenames are explicitly resolved.
5. **Runtime smoke test** — SPT 4.1.3 boots with Artem and CommonLib, trader loads, Artem quests appear, custom items/clothing resolve, and no loader/database errors occur.
6. **Campaign smoke test** — introduction and at least one chained quest can be accepted/completed; quest unlocks populate trader offers correctly.
7. **Economy pass** — trader prices/rewards/stock are reviewed against the project economy without flattening Artem progression.
8. **Packaging** — Core package is reproducible and heavy optional cosmetics/assets are separated only where proven safe.

No gate may be marked complete from static inference alone when it requires runtime evidence.
