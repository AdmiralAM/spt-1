from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SERVER = ROOT / "server"
violations = []

contract = (SERVER / "WearableOfferHostContract.cs").read_text(encoding="utf-8-sig")
armband_item = (SERVER / "WristWalletItem.cs").read_text(encoding="utf-8-sig")
dogtag_item = (SERVER / "DogtagCaseItem.cs").read_text(encoding="utf-8-sig")
dogtag_snapshot = (SERVER / "DogtagCaseHostContract.cs").read_text(encoding="utf-8-sig")
armband = (SERVER / "RuntimeCandidateAssort.cs").read_text(encoding="utf-8-sig")
wallet = (SERVER / "WristWalletAssort.cs").read_text(encoding="utf-8-sig")
dedicated = (SERVER / "DedicatedWearableAssort.cs").read_text(encoding="utf-8-sig")
dogtag = (SERVER / "DogtagCaseAssort.cs").read_text(encoding="utf-8-sig")

for token in [
    "internal static void RequireArmBandProduct(TemplateTable templateTable, MongoId productTemplate)",
    'RequireSingleSlot(templateTable, "ArmBand")',
    "filter.Contains(BroadBeltParentTpl)",
    "filter.Contains(DedicatedMagazineBeltTpl) || filter.Contains(UtilityHeadBandTpl)",
    "!filter.Contains(productTemplate)",
    "internal static void RequireDedicatedProducts(TemplateTable templateTable)",
    "RuntimeIdentity.DedicatedBeltWireSlotId",
    "RuntimeIdentity.DedicatedHeadBandWireSlotId",
    "BeltSlotMongoId",
    "HeadBandSlotMongoId",
    "DedicatedMagazineBeltTpl",
    "UtilityHeadBandTpl",
    ".Take(2)",
    "matches.Length != 1",
    "accepted.Count != 1 || !accepted.Contains(allowedTemplate)",
]:
    if token not in contract:
        violations.append(f"offer-host contract missing token {token!r}")

for token in [
    "DedicatedMagazineBeltTpl = new(RuntimeIdentity.DedicatedMagazineBeltItemId)",
    "UtilityHeadBandTpl = new(RuntimeIdentity.EmergencyHeadBandItemId)",
    "filter.Contains(DedicatedMagazineBeltTpl) || filter.Contains(UtilityHeadBandTpl)",
    "refusing Belt/HeadBand host overlap",
]:
    if token not in armband_item:
        violations.append(f"ArmBand registration missing exact cross-host isolation token {token!r}")

for token in [
    "PersistentIdentityManifest.IsOwnedTemplate(templateId)",
    "!string.Equals(templateId, TemplateId, StringComparison.Ordinal)",
    "already contaminated by a different owned product template",
    "HashSet<MongoId> hostFilter = groups[0].Filter;",
    "DogtagCaseHostContract.CaptureVanillaEntries(vanillaEntries);",
    "DogtagCaseHostContract.RequirePreserved(filter);",
    "DogtagCaseHostContract.RequireCommitted(filter);",
]:
    if token not in dogtag_item:
        violations.append(f"Dogtag Case preload missing exact B&A&HB cross-host/preservation/commit token {token!r}")

for token in [
    "private static readonly object SnapshotSync = new();",
    "private static HashSet<MongoId>? capturedVanillaEntries;",
    "var snapshot = acceptedTemplates.ToHashSet();",
    "lock (SnapshotSync)",
    "capturedVanillaEntries.SetEquals(snapshot)",
    "captured = capturedVanillaEntries.ToArray();",
    "private static HashSet<MongoId> SnapshotCurrentFilter(HashSet<MongoId> currentFilter)",
    "return currentFilter.ToHashSet();",
    "private static void RequirePreservedSnapshot(HashSet<MongoId> current)",
    "foreach (MongoId entry in captured)",
    "if (!current.Contains(entry))",
    "public static void RequireCommitted(HashSet<MongoId> currentFilter)",
    "HashSet<MongoId> current = SnapshotCurrentFilter(currentFilter);",
    "RequirePreservedSnapshot(current);",
    "!current.Contains(caseTpl)",
]:
    if token not in dogtag_snapshot:
        violations.append(f"Dogtag Case host snapshot contract missing token {token!r}")

if "RequirePreserved(currentFilter);\n\n        var caseTpl" in dogtag_snapshot:
    violations.append("Dogtag Case committed host verification must not re-read the live mutable filter after preservation proof")

prepare_call = dogtag_item.find("HashSet<MongoId> dogtagSlotFilter = PrepareDogtagSlotFilter();")
first_commit = dogtag_item.find("CommitDogtagSlotExposure(dogtagSlotFilter);")
prepare_def = dogtag_item.find("private HashSet<MongoId> PrepareDogtagSlotFilter()")
host_capture = dogtag_item.find("HashSet<MongoId> hostFilter = groups[0].Filter;", prepare_def)
capture = dogtag_item.find("DogtagCaseHostContract.CaptureVanillaEntries(vanillaEntries);", prepare_def)
prepare_return = dogtag_item.find("return hostFilter;", prepare_def)
if min(prepare_call, first_commit) < 0 or prepare_call > first_commit:
    violations.append("Dogtag Case must prepare/snapshot its host before the first container exposure")
if min(prepare_def, host_capture, capture, prepare_return) < 0 or not (prepare_def < host_capture < capture < prepare_return):
    violations.append("Dogtag Case host preparation must bind one validated host filter, capture every non-owned entry, then return that same mutable reference")

for label, text in [("Magazine Armband", armband), ("Wrist Wallet", wallet)]:
    host = text.find("WearableOfferHostContract.RequireArmBandProduct(templateTable, templateId);")
    trader = text.find("tradersTable.GetValueOrDefault(")
    first_mutation = text.find("trader.Assort.Items.Add(")
    if min(host, trader, first_mutation) < 0 or not (host < trader < first_mutation):
        violations.append(f"{label} offer must prove exact ArmBand host before resolving/mutating Ragman assort")

host = dedicated.find("WearableOfferHostContract.RequireDedicatedProducts(templateTable);")
belt_prepare = dedicated.find("OfferPlan? beltPlan = PrepareOffer(")
head_prepare = dedicated.find("OfferPlan? headBandPlan = PrepareOffer(")
first_commit = dedicated.find("CommitOffer(")
if min(host, belt_prepare, head_prepare, first_commit) < 0 or not (
    host < belt_prepare < first_commit and host < head_prepare < first_commit
):
    violations.append("dedicated offers must prove exact slot15/slot16 hosts before either offer is prepared or committed")

for token in [
    "new MongoId(RuntimeIdentity.DogtagCaseItemId)",
    "new MongoId(RuntimeIdentity.DogtagCaseAssortId)",
    "private static void RequirePublicationBoundary(TemplateTable templateTable, MongoId templateId)",
    "DogtagCaseItem.RequireCanonicalRegisteredTemplate(templateTable);",
    "RequireExactDogtagHost(templateTable, templateId);",
    'string.Equals(x.Name, DogtagSlotName, StringComparison.Ordinal)',
    "groups.Length != 1",
    "var hostFilter = groups[0].Filter;",
    "hostFilter == null || hostFilter.Count < 2",
    "DogtagCaseHostContract.RequireCommitted(hostFilter);",
    "requested template identity is not the exact Dogtag Case product",
]:
    if token not in dogtag:
        violations.append(f"Dogtag Case offer missing exact committed publication/host token {token!r}")

if "hostFilter.Contains(templateId)" in dogtag or "hostFilter.Any(x => !Equals(x, templateId))" in dogtag:
    violations.append("Dogtag Case offer must not re-read the mutable Dogtag host after centralized committed-snapshot verification")

publication_call = dogtag.find("RequirePublicationBoundary(templateTable, templateId);")
dogtag_trader = dogtag.find("tradersTable.GetValueOrDefault(")
dogtag_first_mutation = dogtag.find("trader.Assort.Items.Add(")
if min(publication_call, dogtag_trader, dogtag_first_mutation) < 0 or not (
    publication_call < dogtag_trader < dogtag_first_mutation
):
    violations.append("Dogtag Case offer must execute centralized canonical-template + committed-host validation before resolving or mutating Ragman assort")

boundary_def = dogtag.find("private static void RequirePublicationBoundary")
boundary_template = dogtag.find("DogtagCaseItem.RequireCanonicalRegisteredTemplate(templateTable);", boundary_def)
boundary_host = dogtag.find("RequireExactDogtagHost(templateTable, templateId);", boundary_def)
require_host_def = dogtag.find("internal static void RequireExactDogtagHost", boundary_def)
if min(boundary_def, boundary_template, boundary_host, require_host_def) < 0 or not (
    boundary_def < boundary_template < boundary_host < require_host_def
):
    violations.append("Dogtag Case publication boundary must prove canonical live template then exact committed host through the centralized helper")

commit_verify = dogtag.find("DogtagCaseHostContract.RequireCommitted(hostFilter);", require_host_def)
host_method_end = dogtag.find("private static void ValidateExisting", require_host_def)
if min(require_host_def, commit_verify, host_method_end) < 0 or not (require_host_def < commit_verify < host_method_end):
    violations.append("Dogtag Case exact-host helper must include complete committed snapshot/case/ownership proof")

if "filter.Add(" in contract or "slots.Add(" in contract:
    violations.append("offer-host validation must be read-only and must not repair equipment filters/slots during trader registration")
if "hostFilter.Add(" in dogtag or "groups[0].Filter.Add(" in dogtag or "slots.Add(" in dogtag:
    violations.append("Dogtag Case offer-host validation must remain read-only; host mutation belongs to DogtagCaseItem preload only")

if violations:
    raise SystemExit("B&A&HB offer-host gate failed:\n" + "\n".join(violations))

print("B&A&HB offer-host gate: OK (Ragman offers require exact live equipment hosts; ArmBand rejects broad-parent and exact Belt/HeadBand cross-host contamination; slot15/slot16 require unique exact product contracts; Dogtag Case centralizes canonical-template + committed-host publication proof, snapshots one point-in-time host view, and keeps validation read-only)")
