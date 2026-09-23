from pathlib import Path
import json
import re

ROOT = Path(__file__).resolve().parents[1]
RUNTIME = ROOT / "src" / "RuntimeIdentity.cs"
CS_MANIFEST = ROOT / "server" / "PersistentIdentityManifest.cs"
VARIANT_CATALOG = ROOT / "src" / "ArmBandVariantCatalog.cs"
JSON_MANIFEST = ROOT / "profile-safety" / "persistent-identities.json"
violations = []

runtime_text = RUNTIME.read_text(encoding="utf-8-sig")
cs_text = CS_MANIFEST.read_text(encoding="utf-8-sig")
catalog_text = VARIANT_CATALOG.read_text(encoding="utf-8-sig")
data = json.loads(JSON_MANIFEST.read_text(encoding="utf-8-sig"))

string_constants = dict(re.findall(r'internal const string\s+(\w+)\s*=\s*"([^"]+)";', runtime_text))

families = {
    "templateIds": [
        "CandidateItemId", "WristWalletItemId", "DedicatedMagazineBeltItemId", "EmergencyHeadBandItemId",
        "DogtagCaseItemId"
    ],
    "parentIds": [
        "SearchableTemplateParentId", "BeltItemParentId", "HeadBandItemParentId"
    ],
    "gridIds": [
        "CandidateGridId", "WristWalletGridId", "DedicatedMagazineBeltGridId",
        "EmergencyHeadBandGridId", "EmergencyHeadBandCigarettesGridId", "DogtagCaseGridId"
    ],
    "assortIds": [
        "CandidateAssortId", "WristWalletAssortId", "DedicatedMagazineBeltAssortId", "EmergencyHeadBandAssortId",
        "DogtagCaseAssortId"
    ],
    "slotIds": ["DedicatedBeltWireSlotId", "DedicatedHeadBandWireSlotId"],
    "slotMongoIds": ["DedicatedBeltSlotMongoId", "DedicatedHeadBandSlotMongoId"],
    "slotSemanticIds": ["DedicatedBeltSlotName", "DedicatedHeadBandSlotName"],
}

for json_key, names in families.items():
    missing_constants = [name for name in names if name not in string_constants]
    if missing_constants:
        violations.append(f"RuntimeIdentity missing constants for {json_key}: {missing_constants}")
        continue
    expected = [string_constants[name] for name in names]
    actual = data.get(json_key)
    if json_key == "templateIds":
        actual = actual[:len(expected)] if isinstance(actual, list) else actual
    if json_key == "gridIds":
        actual = actual[:len(expected)] if isinstance(actual, list) else actual
    if actual != expected:
        violations.append(f"{json_key} mismatch: expected {expected!r}, got {actual!r}")

if data.get("schemaVersion") != 2:
    violations.append("persistent-identities.json schemaVersion must be 2 for the deliberate v0.3 ArmBand variant identity expansion")
if data.get("workstream") != "B&A&HB #2 MOD SPT":
    violations.append("persistent-identities.json workstream identity drifted")
if data.get("targetSpt") != "~4.1.0":
    violations.append("persistent-identities.json targetSpt must express the supported 4.1.x range")

variant_pattern = re.compile(
    r'new\("([0-9a-f]{24})", "([A-Za-z0-9]+)", ArmBandVisualPool\.(ExistingRaid|Standard|All), '
    r'ArmBandRole\.(Medical|Ammo|Magazine|Technical|Currency), "([0-9a-f]{24})", "([0-9a-f]{24})"\)')
catalog_variants = [
    {
        "sourceTemplateId": source,
        "visualKey": visual,
        "visualPool": pool,
        "role": role,
        "templateId": template,
        "gridId": grid,
    }
    for source, visual, pool, role, template, grid in variant_pattern.findall(catalog_text)
]
json_variants = data.get("armBandVariants")
if catalog_variants != json_variants:
    violations.append("armBandVariants mismatch between compiled catalog and recovery manifest")
if len(catalog_variants) != 130:
    violations.append(f"armBandVariants must contain 26 visuals x 5 roles, got {len(catalog_variants)}")
variant_templates = [entry["templateId"] for entry in catalog_variants]
variant_grids = [entry["gridId"] for entry in catalog_variants]
if data.get("templateIds", [])[len(families["templateIds"]):] != variant_templates:
    violations.append("templateIds variant suffix differs from armBandVariants")
if data.get("gridIds", [])[len(families["gridIds"]):] != variant_grids:
    violations.append("gridIds variant suffix differs from armBandVariants")

# Runtime C# ownership must reference every persistent identity family used for
# cleanup/collision decisions. Semantic slot names are presentation identifiers
# and are intentionally JSON-only; wire slot IDs are represented by SlotIds.
for name in (
    families["templateIds"]
    + families["parentIds"]
    + families["gridIds"]
    + families["assortIds"]
    + families["slotIds"]
    + families["slotMongoIds"]
):
    token = f"RuntimeIdentity.{name}"
    if token not in cs_text:
        violations.append(f"PersistentIdentityManifest.cs missing canonical reference {token}")

# Prevent accidental duplicate IDs across Mongo-like persistent families. Wire
# slot IDs 15/16 and semantic names live in separate namespaces by design.
mongo_values = []
for key in ("templateIds", "parentIds", "gridIds", "assortIds", "slotMongoIds"):
    mongo_values.extend(data.get(key, []))
if len(mongo_values) != len(set(mongo_values)):
    violations.append("persistent Mongo-style identities contain duplicate/reused values across families")

if violations:
    raise SystemExit("B&A&HB persistent-identity parity gate failed:\n" + "\n".join(violations))

print("B&A&HB persistent-identity parity gate: OK (RuntimeIdentity, compiled ownership manifest and shipped recovery JSON are exact; Mongo-style IDs unique across families)")
