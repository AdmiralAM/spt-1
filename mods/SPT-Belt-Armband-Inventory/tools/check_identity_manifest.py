from pathlib import Path
import json
import re

ROOT = Path(__file__).resolve().parents[1]
RUNTIME = ROOT / "src" / "RuntimeIdentity.cs"
CS_MANIFEST = ROOT / "server" / "PersistentIdentityManifest.cs"
JSON_MANIFEST = ROOT / "profile-safety" / "persistent-identities.json"
violations = []

runtime_text = RUNTIME.read_text(encoding="utf-8-sig")
cs_text = CS_MANIFEST.read_text(encoding="utf-8-sig")
data = json.loads(JSON_MANIFEST.read_text(encoding="utf-8-sig"))

string_constants = dict(re.findall(r'internal const string\s+(\w+)\s*=\s*"([^"]+)";', runtime_text))

families = {
    "templateIds": [
        "CandidateItemId", "WristWalletItemId", "DedicatedMagazineBeltItemId", "EmergencyHeadBandItemId"
    ],
    "parentIds": [
        "SearchableTemplateParentId", "BeltItemParentId", "HeadBandItemParentId"
    ],
    "gridIds": [
        "CandidateGridId", "WristWalletGridId", "DedicatedMagazineBeltGridId",
        "EmergencyHeadBandGridId", "EmergencyHeadBandCigarettesGridId"
    ],
    "assortIds": [
        "CandidateAssortId", "WristWalletAssortId", "DedicatedMagazineBeltAssortId", "EmergencyHeadBandAssortId"
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
    if actual != expected:
        violations.append(f"{json_key} mismatch: expected {expected!r}, got {actual!r}")

if data.get("schemaVersion") != 1:
    violations.append("persistent-identities.json schemaVersion must remain 1 until a deliberate schema migration exists")
if data.get("workstream") != "B&A&HB #2 MOD SPT":
    violations.append("persistent-identities.json workstream identity drifted")
if data.get("targetSpt") != "4.1.3":
    violations.append("persistent-identities.json targetSpt drifted from candidate target")

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
