# SPT Item Intelligence — Phase 12 EFT Hover Integration

## Scope

Phase 12 connects the already tested hover/presentation pipeline to live EFT item views without adding polling or moving decision work into Unity render callbacks.

## Runtime path

1. At plugin startup, scan loaded game assemblies for `ItemView` types exposing `OnPointerEnter` and `OnPointerExit`.
2. Patch discovered methods through the Harmony instance shipped with BepInEx.
3. Resolve the hovered item's template id through cached property/field metadata.
4. Pass the normalized id to `ItemHoverRuntimeController`.
5. Reuse the immutable presentation snapshot and cached `ItemHoverText`.
6. Publish only changed text to the minimal overlay sink; clear on exit.

## Compatibility and failure boundary

The integration uses no compile-time EFT type dependency. It understands public and obfuscated aliases such as `Item`, `_item`, `TemplateId`, `_tpl` and nested `Template._id`.

If Harmony is missing, pointer methods are renamed, an item shape is unknown, or IMGUI drawing fails, the bridge disables or ignores only that path. Plugin loading and the data/index pipeline remain available. Unknown shapes are logged once instead of once per hover.

## Performance constraints

- no `Update()` polling;
- assembly/type discovery only during installation;
- reflection member lookup cached by type and alias;
- normalized template ids enter the existing O(1) presentation lookup;
- unchanged hover state reuses cached text and produces no sink update;
- diagnostics remain explicit/on-demand.

## Phase boundary

This phase proves the real EFT hover event and minimal sink boundary. Population of live price/requirement snapshots and final visual tuning remain separate work. Physical runtime validation is the final release gate, not a blocker for further implementation.
