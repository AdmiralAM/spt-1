from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SERVER = ROOT / "server"
taxonomy = (SERVER / "WearableTaxonomyRegistration.cs").read_text(encoding="utf-8-sig")
armband = (SERVER / "RuntimeCandidateBeltItem.cs").read_text(encoding="utf-8-sig")
violations = []

for token in [
    "[Injectable(TypePriority = OnLoadOrder.Preload)]",
    "TemplateItem? searchableAddition = PrepareNode(",
    "TemplateItem? beltAddition = PrepareNode(",
    "TemplateItem? headBandAddition = PrepareNode(",
    "if (searchableAddition != null) templateTable.Items.Add(",
    "if (beltAddition != null) templateTable.Items.Add(",
    "if (headBandAddition != null) templateTable.Items.Add(",
    "registered atomically",
]:
    if token not in taxonomy:
        violations.append(f"taxonomy owner missing token {token!r}")

first_add = taxonomy.find("templateTable.Items.Add(")
for prepare in [
    taxonomy.find("TemplateItem? searchableAddition = PrepareNode("),
    taxonomy.find("TemplateItem? beltAddition = PrepareNode("),
    taxonomy.find("TemplateItem? headBandAddition = PrepareNode("),
]:
    if prepare < 0 or first_add < 0 or prepare > first_add:
        violations.append("all persistent taxonomy nodes must be prepared/validated before the first TemplateTable mutation")
        break

if "templateTable.Items[id] =" in taxonomy:
    violations.append("taxonomy registrar must not mutate TemplateTable during per-node validation")

for token in [
    "[Injectable(TypePriority = OnLoadOrder.Preload + 1)]",
    "ValidateTaxonomyParents();",
    "ValidateTaxonomyParent(CustomTemplateParentTpl",
    "ValidateTaxonomyParent(CustomBeltParentTpl",
    "was not registered by the Preload taxonomy owner",
    '.Where(x => string.Equals(x.Name, "ArmBand", StringComparison.Ordinal))',
    ".Take(2)",
    "armBands.Length != 1",
    "filterGroups.Length != 1",
    "ArmBand slot boundary is missing or ambiguous",
    "ArmBand slot filter boundary is missing or ambiguous",
]:
    if token not in armband:
        violations.append(f"Magazine Armband consumer missing token {token!r}")

if 'FirstOrDefault(x => string.Equals(x.Name, "ArmBand"' in armband:
    violations.append("Magazine Armband must not mutate the first ArmBand slot when the host boundary is ambiguous")

for forbidden in ["EnsureCustomParents()", "EnsureCustomParent(", "templateTable.Items[id] = new TemplateItem"]:
    if forbidden in armband:
        violations.append(f"Magazine Armband must not be a second persistent-taxonomy mutation owner: {forbidden!r}")

if violations:
    raise SystemExit("B&A&HB taxonomy-ownership gate failed:\n" + "\n".join(violations))

print("B&A&HB taxonomy-ownership gate: OK (atomic single-owner taxonomy; later item registration validates parents and a unique ArmBand/one-filter host boundary before mutation)")
