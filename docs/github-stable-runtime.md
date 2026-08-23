# GitHub stable/runtime model

## Branch roles

| Branch | Contents | CI behavior |
| --- | --- | --- |
| `main` | Active source for every independent mod | Triggers the complete suite gate |
| `stable` | Exact multi-mod commit that passed the gate | Advanced only after all builds and artifacts succeed |
| `runtime` | Install-only SPT Tactical HUD | Rebuilt from scratch; never contains Item Intelligence |
| `runtime-item-intelligence` | Install-only SPT Item Intelligence | Rebuilt from scratch; never contains Tactical HUD |
| `archive/v1.13.0` | Frozen full Tactical HUD 1.13.0 reserve | Never advanced by CI |

The split runtime channels prevent an update to one mod from silently installing the other. Each uses a version-independent BepInEx plugin directory while its assembly and manifest retain the real semantic version.

## Current releases

| Mod | Client version | Server version | Runtime path |
| --- | --- | --- | --- |
| SPT Tactical HUD | `1.13.2` | `1.13.0` optional | `BepInEx/plugins/SPT Tactical HUD/` |
| SPT Item Intelligence | `0.6.0` | `0.3.0` | `BepInEx/plugins/SPT Item Intelligence/` |

Tactical HUD `1.14.0` is intentionally retired because it mixed the initial Item Intelligence source into the HUD assembly. No `1.14.0` runtime remains after the corrected publication.

## Promotion gate

A source commit is promoted only after:

1. Tactical HUD asset generation, optics and hot-path checks pass;
2. Item Intelligence regression assertions pass;
3. Tactical HUD client, Tactical HUD server and Item Intelligence compile successfully;
4. both independent install packages exist;
5. both workflow artifacts upload successfully.

Only then does CI atomically advance `stable` and regenerate both runtime branches. Failed or superseded builds cannot replace a published channel.
