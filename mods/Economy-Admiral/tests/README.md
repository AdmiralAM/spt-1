# Economy Admiral config validation harness

This directory contains a dependency-light executable regression harness for `EconomyConfigValidator`.

Run with:

```powershell
dotnet run --project mods/Economy-Admiral/tests/Economy-Admiral.ConfigTests.csproj -c Release --nologo
```

The process exits non-zero if any fixture violates the expected fail-closed behavior.

Covered fixtures include:

- valid defaults and nested report paths;
- supported `Exceptional` manual rarity;
- empty/rooted/traversal report paths;
- zero/equal/inverted rarity thresholds;
- zero/negative/NaN/infinite policy values;
- negative structural weights/caps;
- invalid duplicate-trader threshold;
- unsupported manual rarity / empty template ID;
- malformed JSON and unsupported mode/preset enum strings.

This harness does not replace the SPT 4.1.3 physical runtime gate. It proves configuration-domain behavior without adding a third-party test framework.
