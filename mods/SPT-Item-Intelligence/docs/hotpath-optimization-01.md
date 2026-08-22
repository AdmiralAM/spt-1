# Item Intelligence hot-path optimization 01

## Finding

`ItemRegistry.Resolve(object)` currently calls `ItemDescriptor.FromObject(...)` before consulting `exact`/`cache`. `ItemObjectReader.Read(...)` then reflects `Template`, `Props`, signal members and materializes a signal dictionary. Repeated resolves of an already-known template therefore still pay reflection/allocation cost before the cache can return.

## Safe optimization

Add a narrow template-id probe before full descriptor construction:

1. If the input is already an `ItemDescriptor`, keep the existing descriptor path.
2. Otherwise read only the cheapest template-id candidates from the source (`TemplateId`, `Tpl`, `_tpl`) and, only if necessary, from `Template` (`TemplateId`, `Id`, `_id`).
3. Normalize the id once.
4. Under the existing registry lock, probe `exact` and `cache`.
5. On hit, return the existing `ItemDefinition` immediately.
6. On miss or missing id, fall back to the existing full `ItemDescriptor.FromObject(...)` + matcher path unchanged.

This is intentionally not a semantic rewrite. It only removes work from repeated cache hits.

## Acceptance criteria

- Same `ItemDefinition` result as the current path for cache misses and unknown/malformed inputs.
- Registered exact definitions still override inferred cache entries.
- No new polling, global scans or per-frame work.
- Repeated resolution of the same known template does not allocate the signals dictionary or enumerate all signal members.
- Fallback remains fully functional when the source exposes no cheap template id.
- Add regression coverage for: descriptor input, direct `TemplateId`, `_tpl`, nested template id, missing id, exact-hit precedence, inferred-cache hit.

## Follow-up candidates

After this change is measured/validated, inspect `SearchText` + `Has(... params string[])`: `SearchText` materializes a lowercased string and repeated `params` calls allocate marker arrays. Do not change those in the same patch; keep rollback scope narrow.
