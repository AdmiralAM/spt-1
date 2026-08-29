from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SERVER = ROOT / "server"
taxonomy = (SERVER / "WearableTaxonomyRegistration.cs").read_text(encoding="utf-8-sig")
armband = (SERVER / "RuntimeCandidateBeltItem.cs").read_text(encoding="utf-8-sig")
wallet = (SERVER / "WristWalletItem.cs").read_text(encoding="utf-8-sig")
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
    "ArmBand host exposure is intentionally owned by WristWalletItem",
]:
    if token not in armband:
        violations.append(f"Magazine Armband consumer missing token {token!r}")

if "EnsureArmBandAccepts" in armband:
    violations.append("Magazine Armband must not mutate ArmBand host filters before Wrist Wallet exists")

for token in [
    "[Injectable(TypePriority = OnLoadOrder.Preload + 2)]",
    "if (!templateTable.Items.ContainsKey(MagazineArmbandTpl))",
    "EnsureArmBandAcceptsExactProducts();",
    '.Where(x => string.Equals(x.Name, "ArmBand", StringComparison.Ordinal))',
    ".Take(2)",
    "armBands.Length != 1",
    "filterGroups.Length != 1",
    "filter.Contains(BroadBeltParentTpl)",
    "if (!filter.Contains(MagazineArmbandTpl)) filter.Add(MagazineArmbandTpl);",
    "if (!filter.Contains(WristWalletTpl)) filter.Add(WristWalletTpl);",
    "ArmBand host exposure requires both exact product templates to exist",
]:
    if token not in wallet:
        violations.append(f"Wrist Wallet ArmBand owner missing token {token!r}")

if 'FirstOrDefault(x => string.Equals(x.Name, "ArmBand"' in wallet:
    violations.append("ArmBand owner must not mutate the first ArmBand slot when the host boundary is ambiguous")
if "filter.Add(BroadBeltParentTpl)" in wallet or "filter.Add(RuntimeCandidateBeltItem.CustomBeltParentTpl)" in wallet:
    violations.append("ArmBand filter must never admit the broad Belt parent shared by dedicated Magazine Belt")

# New Wrist Wallet path: creation must complete before host mutation. The broad
# parent collision check must occur before either exact product is added.
details = wallet.find("var details = new NewItemFromCloneDetails")
create = wallet.find("var result = customItemService.CreateItemFromClone(details);", details)
failed = wallet.find("if (!result.Success)", create)
post_create_host = wallet.find("EnsureArmBandAcceptsExactProducts();", failed)
if min(details, create, failed, post_create_host) < 0 or not (details < create < failed < post_create_host):
    violations.append("new Wrist Wallet path must create successfully before exposing exact ArmBand products")

method_start = wallet.find("private void EnsureArmBandAcceptsExactProducts()")
method = wallet[method_start:] if method_start >= 0 else ""
broad_check = method.find("if (filter.Contains(BroadBeltParentTpl))")
mag_add = method.find("filter.Add(MagazineArmbandTpl)")
wallet_add = method.find("filter.Add(WristWalletTpl)")
if min(broad_check, mag_add, wallet_add) < 0 or not (broad_check < mag_add and broad_check < wallet_add):
    violations.append("broad Belt-parent collision must be rejected before any exact ArmBand filter mutation")

for forbidden in ["EnsureCustomParents()", "EnsureCustomParent(", "templateTable.Items[id] = new TemplateItem"]:
    if forbidden in armband:
        violations.append(f"Magazine Armband must not be a second persistent-taxonomy mutation owner: {forbidden!r}")

if violations:
    raise SystemExit("B&A&HB taxonomy-ownership gate failed:\n" + "\n".join(violations))

print("B&A&HB taxonomy-ownership gate: OK (atomic single-owner taxonomy; Magazine Armband creates first; Wrist Wallet exposes only two exact ArmBand products after both exist; broad Belt parent rejected pre-mutation)")
