# Admiral TGC Integration

Compatibility and integration layer for Tactical Gear Component 3.0.0 on SPT
4.1.x.

The upstream TGC content identity remains authoritative. This module does not
rename or duplicate its item, clothing, preset, offer, or bundle identifiers.
It replaces only the integration policy around that content:

- Admiral Trader owns the Painter/TGC storefront and progression;
- B&A&HB owns Belt, HeadBand, armband, secure-container, and protected-container
  admission;
- this module owns reproducible upstream import, ordinary item/clothing/preset
  registration, weapon compatibility, and bundle integrity.

The approximately 690 MB upstream Unity payload is deliberately not committed
to this repository. `tools/audit_upstream.py` validates an extracted official
TGC 3.0.0 runtime before it can be used to assemble a candidate.

The stock TGC server DLL and Painter are not part of the final runtime. The
stock DLL hard-codes Painter's trader ID and mutates the vanilla armband and
secure-container filters; both responsibilities belong to the integration
owners above.

Upstream:

- <https://github.com/thuynguyentrungdang/TGC/releases/tag/3.0.0>
- <https://github.com/thuynguyentrungdang/Painter/releases/tag/3.0.0>

