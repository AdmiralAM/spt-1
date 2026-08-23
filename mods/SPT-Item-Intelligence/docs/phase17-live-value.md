# Phase 17 — live Value pipeline

Phase 17 connects the existing pricing/value model to the real SPT 4.1.2 runtime.

- snapshot schema v2 publishes flea, highest trader and handbook fallback unit values;
- item width/height are included for ₽/slot;
- the client chooses the highest available source and keeps the source data cached;
- each registered ItemView contributes its live stack count, so total Value is instance-correct;
- Value remains tooltip content only and never changes the requirement-priority marker color;
- the complete table is loaded once in the existing background snapshot path, with no polling.
