# SPT Item Intelligence — Phase 3

Version `0.3.0` adds the native SPT **4.1.2** server data boundary required by Safe-to-Sell.

## Runtime components

Phase 3 ships two independent Item Intelligence components in the same runtime channel:

- BepInEx client DLL: semantic registry, Safe-to-Sell evaluator and shared data contract;
- SPT server DLL: profile/quest/hideout snapshot provider.

Neither component contains Tactical HUD code.

## Route contract

- Route: `/spt-item-intelligence/v1/snapshot`
- Schema: `1`
- Invocation: on demand only
- Cancellation: propagated from the SPT 4.1 route token
- Payload:
  - generation time;
  - explicit `profileReady` state;
  - current PMC profile when available;
  - quest templates;
  - hideout table.

The server does not poll, scan Unity objects, write profile state or cache stale inventory counts. Consumers decide when to request a fresh snapshot.

## Why this is not the old MoreCheckmarks backend

The previous MoreCheckmarks server was built against the SPT 4.0 server packages. SPT 4.1 refuses server mods compiled against a different Core version and changed metadata, lifecycle, router signatures and table injection.

This implementation targets `net10.0` and SPT packages `4.1.2`, uses `IModMetadata`, async lifecycle and a custom route positioned at `OnLoadOrder.Routers + 1`.

References:

- [SPT 4.0 → 4.1 server migration](https://github.com/SP-Tushonka/wiki/blob/main/modding/SPT_41_Modding/Server_40_to_41.md)
- [MoreCheckmarks 4.0 reference implementation](https://github.com/TommySoucy/MoreCheckmarks)

## Phase boundary

Phase 3 does not yet project raw profile/quest/hideout data into per-template requirement reasons. It also adds no inventory UI, tooltip, checkmark or automatic item action. That projection is the next consumer of this transport contract.
