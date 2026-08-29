from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SERVER = ROOT / "server"
taxonomy = (SERVER / "WearableTaxonomyRegistration.cs").read_text(encoding="utf-8-sig")
armband = (SERVER / "RuntimeCandidateBeltItem.cs").read_text(encoding="utf-8-sig")
violations = []

for token in [
    "[Injectable(TypePriority = OnLoadOrder.Preload)]",
    "EnsureNode(SearchableParentTpl",
    "EnsureNode(BeltParentTpl",
    "EnsureNode(HeadBandParentTpl",
    "templateTable.Items[id] = new TemplateItem",
]:
    if token not in taxonomy:
        violations.append(f"taxonomy owner missing token {token!r}")

for token in [
    "[Injectable(TypePriority = OnLoadOrder.Preload + 1)]",
    "ValidateTaxonomyParents();",
    "ValidateTaxonomyParent(CustomTemplateParentTpl",
    "ValidateTaxonomyParent(CustomBeltParentTpl",
    "was not registered by the Preload taxonomy owner",
]:
    if token not in armband:
        violations.append(f"Magazine Armband consumer missing token {token!r}")

for forbidden in ["EnsureCustomParents()", "EnsureCustomParent(", "templateTable.Items[id] = new TemplateItem"]:
    if forbidden in armband:
        violations.append(f"Magazine Armband must not be a second persistent-taxonomy mutation owner: {forbidden!r}")

if violations:
    raise SystemExit("B&A&HB taxonomy-ownership gate failed:\n" + "\n".join(violations))

print("B&A&HB taxonomy-ownership gate: OK (Preload taxonomy registrar is the sole persistent-parent mutation owner; later item registration validates only)")
