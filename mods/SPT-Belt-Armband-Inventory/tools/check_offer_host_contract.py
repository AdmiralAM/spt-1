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


def require_tokens(label, text, tokens):
    for token in tokens:
        if token not in text:
            violations.append(f"{label} missing token {token!r}")


require_tokens("offer-host contract", contract, [
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
])

require_tokens("ArmBand registration exact cross-host isolation", armband_item, [
    "DedicatedMagazineBeltTpl = new(RuntimeIdentity.DedicatedMagazineBeltItemId)",
    "UtilityHeadBandTpl = new(RuntimeIdentity.EmergencyHeadBandItemId)",
    "filter.Contains(DedicatedMagazineBeltTpl) || filter.Contains(UtilityHeadBandTpl)",
    "refusing Belt/HeadBand host overlap",
])

# Dogtag preload must own one exact live DefaultInventory -> Dogtag slot -> sole
# filter-group -> HashSet chain. Value-equivalent detached replacements are not
# sufficient publication authority.
require_tokens("Dogtag Case preload exact host boundary", dogtag_item, [
    "private sealed class DogtagHostBoundary(object inventory, object slot, object filterGroup, HashSet<MongoId> filter)",
    "public object Inventory { get; } = inventory;",
    "public object Slot { get; } = slot;",
    "public object FilterGroup { get; } = filterGroup;",
    "public HashSet<MongoId> Filter { get; } = filter;",
    "DogtagHostBoundary dogtagHost = PrepareDogtagSlotFilter();",
    "private DogtagHostBoundary PrepareDogtagSlotFilter()",
    "return new DogtagHostBoundary(inventory, slots[0], groups[0], hostFilter);",
    "private void RequireLiveDogtagHostIdentity(DogtagHostBoundary boundary)",
    "!ReferenceEquals(liveInventory, boundary.Inventory)",
    "!ReferenceEquals(liveSlots[0], boundary.Slot)",
    "!ReferenceEquals(liveGroups[0], boundary.FilterGroup)",
    "!ReferenceEquals(liveGroups[0].Filter, boundary.Filter)",
    "private void CommitDogtagSlotExposure(DogtagHostBoundary boundary, CancellationToken cancellationToken)",
    "HashSet<MongoId> filter = boundary.Filter;",
    "PersistentIdentityManifest.IsOwnedTemplate(templateId)",
    "!string.Equals(templateId, TemplateId, StringComparison.Ordinal)",
    "already contaminated by a different owned product template",
    "DogtagCaseHostContract.CaptureVanillaEntries(vanillaEntries);",
    "DogtagCaseHostContract.RequirePreserved(filter);",
    "DogtagCaseHostContract.RequireCommitted(filter);",
    "bool addedHere = filter.Add(DogtagCaseTpl);",
    "if (addedHere)",
    "filter.Remove(DogtagCaseTpl);",
])

require_tokens("Dogtag Case host snapshot contract", dogtag_snapshot, [
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
])

if "RequirePreserved(currentFilter);\n\n        var caseTpl" in dogtag_snapshot:
    violations.append("Dogtag Case committed host verification must not re-read the live mutable filter after preservation proof")

# Preload order: capture the exact boundary before any exposure.
prepare_call = dogtag_item.find("DogtagHostBoundary dogtagHost = PrepareDogtagSlotFilter();")
first_commit = dogtag_item.find("CommitDogtagSlotExposure(dogtagHost, cancellationToken);")
prepare_def = dogtag_item.find("private DogtagHostBoundary PrepareDogtagSlotFilter()")
host_capture = dogtag_item.find("HashSet<MongoId> hostFilter = groups[0].Filter;", prepare_def)
capture = dogtag_item.find("DogtagCaseHostContract.CaptureVanillaEntries(vanillaEntries);", prepare_def)
boundary_return = dogtag_item.find("return new DogtagHostBoundary(inventory, slots[0], groups[0], hostFilter);", prepare_def)
if min(prepare_call, first_commit) < 0 or prepare_call > first_commit:
    violations.append("Dogtag Case must capture/snapshot its exact host boundary before the first cancellation-atomic exposure")
if min(prepare_def, host_capture, capture, boundary_return) < 0 or not (prepare_def < host_capture < capture < boundary_return):
    violations.append("Dogtag Case host preparation must bind one validated filter, capture non-owned baseline entries, then return the exact inventory/slot/group/filter boundary")

# Commit order: exact live identity and preserved baseline are proven before Add;
# committed proof and a second live-identity proof occur while rollback ownership
# is still bounded by HashSet.Add's return value.
commit_def = dogtag_item.find("private void CommitDogtagSlotExposure(DogtagHostBoundary boundary, CancellationToken cancellationToken)")
commit_end = dogtag_item.find("public static void RequireCanonicalRegisteredTemplate", commit_def)
commit_region = dogtag_item[commit_def:commit_end] if min(commit_def, commit_end) >= 0 else ""
first_identity = commit_region.find("RequireLiveDogtagHostIdentity(boundary);")
preserved = commit_region.find("DogtagCaseHostContract.RequirePreserved(filter);")
second_identity = commit_region.find("RequireLiveDogtagHostIdentity(boundary);", first_identity + 1)
add = commit_region.find("bool addedHere = filter.Add(DogtagCaseTpl);")
committed = commit_region.find("DogtagCaseHostContract.RequireCommitted(filter);")
post_commit_identity = commit_region.find("RequireLiveDogtagHostIdentity(boundary);", second_identity + 1)
rollback = commit_region.find("filter.Remove(DogtagCaseTpl);")
if min(first_identity, preserved, second_identity, add, committed, post_commit_identity, rollback) < 0 or not (
    first_identity < preserved < second_identity < add < committed < post_commit_identity < rollback
):
    violations.append("Dogtag Case exposure must remain live-identity -> preserved -> live-identity -> owned Add -> committed -> live-identity -> owned rollback")
if commit_region.count("cancellationToken.ThrowIfCancellationRequested();") < 4:
    violations.append("Dogtag Case exposure must observe cancellation before mutation and within the owned commit/rollback boundary")

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

require_tokens("Dogtag Case offer exact committed publication/host contract", dogtag, [
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
])

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

print("B&A&HB offer-host gate: OK (Ragman offers require exact live equipment hosts; Dogtag preload pins DefaultInventory/slot/group/filter by reference, re-proves that chain around the owned mutation, preserves vanilla/foreign baseline entries, and rolls back only its own exact append)")