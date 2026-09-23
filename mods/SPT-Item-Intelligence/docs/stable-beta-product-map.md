# Item Intelligence Admiral v1.2.1 Stable Beta product map

This map describes the code that is actually compiled at the Stable Beta head. Stable Beta freezes the currently playable feature set; physical testing, optimization, visual polish and later extensions remain follow-up work and do not turn this prerelease into final stable.

## Shipped binaries and runtime identity

| Binary | Runtime location | Identity and ownership |
|---|---|---|
| `Item Intelligence Admiral.dll` | `BepInEx/plugins/Admiral SPT/SPT Item Intelligence/` | Client plugin `com.admiralam.spt.itemintelligence`, version `1.2.1`. Owns cached decisions, item-cell marker/card presentation, raid inventory accounting and optional adapters. |
| `Item Intelligence Admiral Server.dll` | `SPT_Runtime/user/mods/Item Intelligence Admiral Server/` | Server mod `com.admiralam.spt.itemintelligence.server`, version `1.2.1`, compatible with SPT `~4.1.0`. Owns the bounded authoritative snapshot. |

The client and server are both required for the complete product. No external quest, valuation, Sense or compatibility mod is required.

## Product modules

| Module | What it owns | Replaces or complements | Outside Item Intelligence |
|---|---|---|---|
| Requirement/FIR/hideout truth model | Per-template active quest, future quest and incomplete current/future hideout demand; FIR-first allocation; any-item allocation; additive consumptive requirements; owned, FIR-owned, required, remaining and Keep state without double counting. Hideout In Progress deposits are deducted only from their matching current upgrade. | Independently replaces the useful requirement summary role of AllQuestsCheckmarks and Task Item Indicator without copying their code or assets. | Quest execution, hand-in, profile mutation and hideout construction remain native SPT/EFT. |
| Presentation index | Combines requirement, price and relevance snapshots into immutable template-keyed states. Unknown template IDs remain valid and simply have no known requirement/value entry. | Shared data source for every Item Intelligence presentation. | It does not maintain a manual catalog for WTT or other content mods. |
| Contextual marker | One crisp item-cell badge for relevant items, with requirement-source and coverage colors, selectable symbol/frame, placement, fill and halo. | Makes an external AQC marker unnecessary for the covered II decisions. | Native quest icons and other mods' unrelated item decorations remain theirs. |
| Information card | Minimal, Normal, Detailed and Full cards; RU for Russian game UI and EN otherwise; owned/FIR/required/remaining, target hierarchy, selected value and craft/barter relevance. | Consolidates the useful AQC-style compact hierarchy with Item Intelligence data. | No transaction, QuickSell button or automatic item action. |
| Value and relevance | Cached best trader value, flea value, handbook fallback, per-slot value, craft-input count and barter-input count. Default presets are valued with their included parts. | Absorbs the useful information side of Item Valuation and pricing references. | No flea tax, price age/freshness, manual refresh, ignored-trader list or sale execution. |
| Background coloring | Writes the accepted value-tier backgrounds to authoritative templates; ammunition uses penetration tiers; keys are deliberately skipped. Can be enabled without markers/tooltips. | Supersedes standalone Item Valuation for normal operation. Complements ammo/armor/key specialists without taking their visual ownership. | BetterKeys keeps key colors. Dedicated ammo and armor mods keep their own indicators. |
| Raid refresh and ledger | During an active raid, reads the main player's real inventory every `0.2 s`, caches reflected accessors by item type, tracks stack/FIR state by item instance, removes dropped items and resets outside raids. UI/Sense refresh only when the ledger changes. | Fixes stale pre-raid owned counts and complements Sense pickup events. | No scene-wide scan and no server snapshot request every `0.2 s`; hideout inventory never starts the raid ledger. |
| Container logic | Traverses loose items and nested container trees, preserves distinct template identities, aggregates only useful unfinished contents and shows completed tracked containers as a compact green state. | Extends Sense container awareness with II requirement semantics. | Native container opening/searching and Sense's base rendering remain external. |
| Amands Sense adapter | Optional soft adapter for requirement-first loot, key/grenade/currency/food/water categories, stock/count colors, total container value tiers, pickup/drop/reset events and a smaller native container-name line. Reuses Sense-loaded sprites. | Quest/hideout requirements precede requirement-complete state; existing protected native value/favorite marks remain ahead of fallback categories; aggregate container price follows requirement status. | Complements Amands Sense and leaves its body, exfil, sound and base world rendering intact. Item Intelligence ships no Sense assets or config rewrite. |
| CompatibilityHighlighter bridge | Adds ItemViews already registered by II in detached container windows to CompatibilityHighlighter's own view list. | Complements CompatibilityHighlighter so its native blue compatibility outline reaches container windows. | Compatibility rules, colors, hover selection and outline rendering remain entirely owned by CompatibilityHighlighter. |

## Client runtime composition

Every client source unit compiled into the DLL is listed here. The product-map guard fails when another source unit is added without being documented.

| Source unit | Responsibility |
|---|---|
| `Plugin.cs` | BepInEx lifecycle, module activation, bounded snapshot task, hideout/menu refresh coalescing, active-raid poll and presentation invalidation. |
| `UiSettings.cs` | F12 configuration, ranges, module dependency keys and change revision. |
| `ModuleSelection.cs` | Deterministic enable/disable rules and zero-work consumer/data keys. |
| `RequirementDataContract.cs` | Shared snapshot DTOs and route `/spt-item-intelligence/v2/snapshot`. |
| `RuntimeDataBootstrap.cs` | Reflection HTTP transport, JSON decode and projection of the server snapshot. |
| `RelevanceSnapshotDecoder.cs` | Craft/barter relevance decode with module-aware early exit. |
| `RequirementIndex.cs` | Canonical active/future/hideout contributions and normalized template index. |
| `RequirementAllocation.cs` | Coverage result used by UI and Sense. |
| `ItemRequirementState.cs` | Requirement state builder/store. |
| `FirRequirementRegistry.cs` | FIR semantics shared with display formatting. |
| `SafeToSell.cs` | Deterministic consumptive allocation and final Keep/Need More/Enough/Not Needed decision. |
| `AqcQuestRequirementProjector.cs` | Compatibility projection of the independent requirement model; no AQC dependency or copied implementation. |
| `Pricing.cs` | Price tiers, evaluation and immutable price indexes. |
| `BackgroundPalette.cs` | Value and penetration background palettes plus dedicated-owner exclusions. |
| `ItemRelevanceRegistry.cs` | Cached craft/barter counts. |
| `PresentationState.cs` | Combined immutable requirement/value presentation index. |
| `MarkerPresentation.cs` | Requirement-priority marker classification. |
| `HoverFormatting.cs` | Mode-specific RU/EN card text, count hierarchy and bounded caches. |
| `HoverPresentation.cs` | Hover state adapter. |
| `HoverRuntimeController.cs` | Hover lifecycle and cached view updates. |
| `PolishedTooltipRenderer.cs` | Auto-sized rounded card drawing and typography. |
| `ItemHoverOverlaySink.cs` | ItemView registry, marker attachment, hover target, background restoration and IMGUI card output. |
| `EftHoverIntegration.cs` | Cached template/stack reflection and EFT ItemView Harmony lifecycle bridge. |
| `RaidRequirementLedger.cs` | Instance-id raid inventory baseline/current state and requirement evaluation. |
| `RaidInventoryRuntimeScanner.cs` | Main-player inventory snapshot, FIR/stack reads and hideout exclusion. |
| `SenseRequirementPresentation.cs` | Sense category/icon/stock policy and container priority aggregation. |
| `AmandsSenseIntegration.cs` | Optional Sense reflection/Harmony adapter and nested-container traversal. |
| `CompatibilityHighlighterIntegration.cs` | Optional detached-window view bridge. |
| `GameLanguageDetector.cs` | Detects Russian versus English presentation. |
| `GameUiText.cs` | Two-language UI text selection. |
| `ItemValueMode.cs` | Vendor/Flea selected-value mode. |
| `ItemIntelligence.cs` | Generic item semantic registry and unknown-item-safe classification. |
| `Diagnostics.cs` | Bounded diagnostic decision objects. |
| `R33Revision.cs` | Retained revision compatibility marker used by regression coverage. |

## Harmony/runtime patches

No SPT `ModulePatch` class is compiled. Three bounded Harmony owners are installed through reflection and unpatch themselves on shutdown or module disable:

| Harmony owner | Patched runtime methods | Purpose |
|---|---|---|
| `com.admiralam.spt.itemintelligence.hover` | Discovered EFT `ItemView`/`ItemCell` `OnPointerEnter`, `OnPointerExit`, one initialization method (`Init`, `Show` or `SetItem`) and one cleanup method (`Kill`, `OnDisable` or `Close`). | Register/unregister visible cells, attach markers and open/close the information card. |
| `com.admiralam.spt.itemintelligence.amandssense` | `AmandsSenseItem.SetSense`, `AmandsSenseContainer.SetSense`, `AmandsSenseContainer.UpdateSense`, `AmandsSenseItem.RemoveLootItem`, `AmandsSenseClass.Clear`. | Apply cached II semantics, observe pickups/drops, reset raid state and keep only the native container-name line at its configured size. |
| `com.admiralam.spt.itemintelligence.compatibilityhighlighter` | `CompatibilityHighlighter.HoverHighlightDriver.GetAllItemViews`. | Merge II-tracked visible views from detached container windows into the external mod's result; rules and rendering are untouched. |

## Server registrations and snapshot

`ServerMod.cs` is the only server source unit and contains:

| Registration | Function |
|---|---|
| `ModMetadata` | GUID, name, MIT license, version `1.2.1` and SPT compatibility `~4.1.0`. |
| `RequirementDataService` (`[Injectable]`) | Freezes the current profile, then builds quest/hideout/localization, price, background and craft/barter data. Reads `Tyfon.HideoutInProgress.json` only when present. |
| `ItemIntelligenceRouter` (`[Injectable(TypePriority = OnLoadOrder.Routers + 1)]`) | Registers `/spt-item-intelligence/v2/snapshot`. |
| `ItemIntelligenceLoadNotice` (`[Injectable(TypePriority = OnLoadOrder.PostLoad)]`) | One successful startup notice. |

## Scheduled and background work

| Runtime path | Boundary |
|---|---|
| `Task.Run` snapshot load | Runs only when an enabled data key needs a snapshot; serialized by a lock and cancelled/replaced on module changes. |
| `RefreshInventorySessionAfterBurst` | One coalesced coroutine after non-raid inventory/hideout UI bursts; waits `0.65 s` and enforces a `1.5 s` minimum snapshot interval. |
| `Plugin.Update` raid inventory | Active only while at least one consumer is enabled and a real raid session is active; polls player inventory through `RaidInventoryPollSeconds` (`0.2 s`); publishes UI work only on change. |
| `Plugin.OnGUI` | Draws only while view tracking is enabled. Uses prepared presentation data. |
| Sense active refresh | Event/change-driven after raid-ledger changes; tracked objects are bounded and stale objects are removed. |

## F12 controls

All current entries are listed exactly as `section / key`:

- `Modules / Markers`, `Modules / Tooltips`, `Modules / Quests`, `Modules / Future Quests`, `Modules / Hideout`, `Modules / Value`, `Modules / Craft and Barter`, `Modules / Background Coloring (Valuation)`.
- `Tooltip / Mode`, `Tooltip / Value Source`, `Tooltip / Scale`, `Tooltip / Opacity`, `Tooltip / Font Size`, `Tooltip / Maximum Width`, `Tooltip / Inner Padding`.
- `Marker / Side`, `Marker / Symbol`, `Marker / Frame`, `Marker / Size`, `Marker / Opacity`, `Marker / Circle Background Color`, `Marker / Circle Background Opacity (%)`, `Marker / Offset X`, `Marker / Offset Y`, `Marker / Halo`, `Marker / Halo Strength`.
- `Marker Colors / Default Color`, `Marker Colors / Quest Now Color`, `Marker Colors / Hideout Color`, `Marker Colors / Quest Later Color`.
- `Tooltip Colors / Complete Color`, `Tooltip Colors / Partial Color`, `Tooltip Colors / Missing Color`.
- `Amands Sense / Integration`, `Amands Sense / Required Items`, `Amands Sense / Category Markers`, `Amands Sense / Container Value Colors`, `Amands Sense / Secondary Reason Outline`, `Amands Sense / Remaining Count Text`, `Amands Sense / Container Name Scale`.
- `Amands Sense Colors / Active Quest`, `Amands Sense Colors / Hideout`, `Amands Sense Colors / Future Quest`, `Amands Sense Colors / Food`, `Amands Sense Colors / Water`, `Amands Sense Colors / Keys`, `Amands Sense Colors / Grenades`, `Amands Sense Colors / Currency`.
- `Amands Sense Container Value Colors / Up to 50k`, `Amands Sense Container Value Colors / 50k to 100k`, `Amands Sense Container Value Colors / 100k to 200k`, `Amands Sense Container Value Colors / 200k Plus`.
- `Amands Sense Stock Colors / Missing`, `Amands Sense Stock Colors / Partial`, `Amands Sense Stock Colors / Next Covered`, `Amands Sense Stock Colors / Complete`.
- `Amands Sense Count Colors / One Item`, `Amands Sense Count Colors / Two to Three`, `Amands Sense Count Colors / Four Plus`.

## Dependencies and ownership boundaries

| Dependency | Status | Contract |
|---|---|---|
| SPT 4.1.x / EFT runtime | Required | Server targets `~4.1.0`; current validation baseline is SPT 4.1.5. |
| BepInEx 5, Unity and Harmony runtime | Required client platform | Provided by the SPT client runtime. `UnityEngine.Modules` is a build-time package, not an extra user install. |
| SPT server libraries (`SPTushonka.Common`, `SPTushonka.DI`, `SPTushonka.Server.Core`) | Required server platform | Provided by SPT; package references are build inputs. |
| `xyz.drakia.Sense` | Optional soft dependency | Enables the Sense adapter. Absence leaves core II unchanged and performs no Sense work. |
| `com.awnova.compatibilityhighlighter` | Optional soft dependency | Enables detached-container view bridging. Absence leaves core II unchanged. |
| Tyfon Hideout In Progress data file | Optional detected integration | Deposited current-upgrade components are synchronized when its profile file exists. No DLL dependency. |
| BetterKeys, ammo/armor icon mods, WTT Armory/Backport/CommonLib | Optional coexistence | No required dependency. Key and specialist visual ownership is preserved; content templates are handled generically from final SPT tables. |
| AllQuestsCheckmarks, standalone Item Valuation, Task Item Indicator | Not required | Their useful approved concepts are independently covered; no GPL code/assets or mandatory runtime link. |

## Explicitly outside Stable Beta

Final-stable promotion, further physical acceptance, deeper performance profiling, visual polish, semantic world outlines, flea rarity/class indicators, instance-aware valuation beyond the current default-preset logic, price freshness, manual price refresh, flea tax, ignored traders, ammo summaries and transaction execution are not part of this Stable Beta package.
