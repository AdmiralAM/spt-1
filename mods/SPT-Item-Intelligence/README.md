# SPT Item Intelligence

Independent item-semantics mod for SPT 4.1.x. Current version: **0.1.0** (`Phase 1`).

This is not part of SPT Tactical HUD and does not modify HUD visuals or HUD runtime behavior.

Phase 1 provides:

- immutable normalized `ItemDefinition` records;
- the approved item categories and semantic tags;
- one process-wide matcher/registry;
- exact-template and parent-template overrides;
- cached resolution of SPT item/template objects;
- an explicit `unknown` fallback;
- an autonomous executable regression suite.

Install from the separate `runtime-item-intelligence` channel. The plugin lives at `BepInEx/plugins/SPT Item Intelligence/` and has its own GUID, DLL and version lifecycle.

See [the Phase 1 contract](docs/phase1.md).
