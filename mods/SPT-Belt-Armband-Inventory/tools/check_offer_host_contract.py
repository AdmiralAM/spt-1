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

require_tokens("Dogtag Case preload exact host boundary", dogtag_item, [
    "private sealed class DogtagHostBoundary(object inventory, object slot, object filterGroup, HashSet<MongoId> filter)",
    "public object Inventory { get; } = inventory;",
    "public object Slot { get; } = slot;",
    "public object FilterGroup { get; } = filterGroup;",
    "public HashSet<MongoId> Filter { get; } = filter;",
    "public object? InventoryProperties { get; init; }",
    "public object? SlotsCollection { get; init; }",
    "public object? SlotProperties { get; init; }",
    "public object? FiltersCollection { get; init; }",
    "private sealed class DogtagHostCommitReceipt",
    "internal void Accept()",
    "internal bool TryRollback()",
    "DogtagHostBoundary dogtagHost = PrepareDogtagSlotFilter();",
    "private DogtagHostBoundary PrepareDogtagSlotFilter()",
    "return new DogtagHostBoundary(inventory, slots[0], groups[0], hostFilter)",
    "InventoryProperties = inventoryProperties",
    "SlotsCollection = slotsCollection",
    "SlotProperties = slotProperties",
    "FiltersCollection = filtersCollection",
    "private void RequireLiveDogtagHostIdentity(DogtagHostBoundary boundary)",
    "!ReferenceEquals(liveInventory, boundary.Inventory)",
    "!ReferenceEquals(liveInventory.Properties, boundary.InventoryProperties)",
    "!ReferenceEquals(liveInventory.Properties?.Slots, boundary.SlotsCollection)",
    "!ReferenceEquals(liveSlots[0], boundary.Slot)",
    "!ReferenceEquals(liveSlots[0].Properties, boundary.SlotProperties)",
    "!ReferenceEquals(liveSlots[0].Properties?.Filters, boundary.FiltersCollection)",
    "!ReferenceEquals(liveGroups[0], boundary.FilterGroup)",
    "!ReferenceEquals(liveGroups[0].Filter, boundary.Filter)",
    "private DogtagHostCommitReceipt CommitDogtagSlotExposure(DogtagHostBoundary boundary, CancellationToken cancellationToken)",
    "return new DogtagHostCommitReceipt(this, boundary, addedHere ? rollbackBaseline : null);",
    "HashSet<MongoId> filter = boundary.Filter;",
    "PersistentIdentityManifest.IsOwnedTemplate(templateId)",
    "!string.Equals(templateId, TemplateId, StringComparison.Ordinal)",
    "already contaminated by a different owned product template",
    "DogtagCaseHostContract.CaptureVanillaEntries(vanillaEntries);",
    "DogtagCaseHostContract.RequirePreserved(filter);",
    "DogtagCaseHostContract.RequireCommitted(filter);",
    "DogtagCaseHostContract.CaptureRollbackBaseline(filter)",
    "addedHere = filter.Add(DogtagCaseTpl);",
    "DogtagCaseHostContract.TryAbandonRollbackAuthority(boundary.Filter, rollbackBaseline)",
    "DogtagCaseHostContract.TryRollbackOwnedCaseAddition(boundary.Filter, rollbackBaseline)",
    "DogtagCaseHostContract.TryRollbackOwnedCaseAddition(filter, rollbackBaseline)",
    "ambiguous/foreign current host state is not blindly rewritten",
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
    "public static HashSet<MongoId> CaptureRollbackBaseline(HashSet<MongoId> currentFilter)",
    "public static bool TryRollbackOwnedCaseAddition(HashSet<MongoId> currentFilter, HashSet<MongoId> preCommitSnapshot)",
    "HashSet<MongoId> expectedCommitted = new(preCommitSnapshot) { caseTpl };",
    "if (!current.SetEquals(expectedCommitted))",
    "return after.SetEquals(preCommitSnapshot);",
])

if "RequirePreserved(currentFilter);\n\n        var caseTpl" in dogtag_snapshot:
    violations.append("Dogtag Case committed host verification must not re-read the live mutable filter after preservation proof")

prepare_call = dogtag_item.find("DogtagHostBoundary dogtagHost = PrepareDogtagSlotFilter();")
first_commit = dogtag_item.find("CommitDogtagSlotExposure(dogtagHost, cancellationToken);")
prepare_def = dogtag_item.find("private DogtagHostBoundary PrepareDogtagSlotFilter()")
host_capture = dogtag_item.find("HashSet<MongoId> hostFilter = groups[0].Filter;", prepare_def)
capture = dogtag_item.find("DogtagCaseHostContract.CaptureVanillaEntries(vanillaEntries);", prepare_def)
boundary_return = dogtag_item.find("return new DogtagHostBoundary(inventory, slots[0], groups[0], hostFilter)", prepare_def)
capture_inventory_props = dogtag_item.find("InventoryProperties = inventoryProperties", boundary_return)
capture_slots = dogtag_item.find("SlotsCollection = slotsCollection", capture_inventory_props)
capture_slot_props = dogtag_item.find("SlotProperties = slotProperties", capture_slots)
capture_filters = dogtag_item.find("FiltersCollection = filtersCollection", capture_slot_props)
if min(prepare_call, first_commit) < 0 or prepare_call > first_commit:
    violations.append("Dogtag Case must capture/snapshot its exact host boundary before the first cancellation-atomic exposure")
if min(prepare_def, host_capture, capture, boundary_return, capture_inventory_props, capture_slots, capture_slot_props, capture_filters) < 0 or not (
    prepare_def < host_capture < capture < boundary_return < capture_inventory_props < capture_slots < capture_slot_props < capture_filters
):
    violations.append("Dogtag Case host preparation must bind one validated filter, capture non-owned baseline entries, then pin inventory properties/slots/slot properties/filter collection with the exact inventory/slot/group/filter boundary")

commit_def = dogtag_item.find("private DogtagHostCommitReceipt CommitDogtagSlotExposure(DogtagHostBoundary boundary, CancellationToken cancellationToken)")
commit_end = dogtag_item.find("public static void RequireCanonicalRegisteredTemplate", commit_def)
commit_region = dogtag_item[commit_def:commit_end] if min(commit_def, commit_end) >= 0 else ""
first_identity = commit_region.find("RequireLiveDogtagHostIdentity(boundary);")
preserved = commit_region.find("DogtagCaseHostContract.RequirePreserved(filter);")
second_identity = commit_region.find("RequireLiveDogtagHostIdentity(boundary);", first_identity + 1)
rollback_baseline = commit_region.find("DogtagCaseHostContract.CaptureRollbackBaseline(filter)", second_identity + 1)
add = commit_region.find("addedHere = filter.Add(DogtagCaseTpl);", rollback_baseline + 1)
committed = commit_region.find("DogtagCaseHostContract.RequireCommitted(filter);", add + 1)
post_commit_identity = commit_region.find("RequireLiveDogtagHostIdentity(boundary);", committed + 1)
receipt_return = commit_region.find("return new DogtagHostCommitReceipt(this, boundary, addedHere ? rollbackBaseline : null);", post_commit_identity + 1)
rollback = commit_region.find("DogtagCaseHostContract.TryRollbackOwnedCaseAddition(filter, rollbackBaseline)", receipt_return + 1)
if min(first_identity, preserved, second_identity, rollback_baseline, add, committed, post_commit_identity, receipt_return, rollback) < 0 or not (
    first_identity < preserved < second_identity < rollback_baseline < add < committed < post_commit_identity < receipt_return < rollback
):
    violations.append("Dogtag Case exposure must remain live-identity -> preserved -> live-identity -> detached rollback baseline -> owned Add -> committed -> live-identity -> receipt handoff, with proven owned rollback on in-transaction failure")
if "filter.Remove(DogtagCaseTpl);" in commit_region:
    violations.append("Dogtag Case exposure must not use value-only rollback; ambiguous/foreign current host state must remain untouched")
if commit_region.count("cancellationToken.ThrowIfCancellationRequested();") < 4:
    violations.append("Dogtag Case exposure must observe cancellation before mutation and within the owned commit/rollback boundary")

receipt_def = dogtag_item.find("private sealed class DogtagHostCommitReceipt")
receipt_end = dogtag_item.find("public Task OnLoadAsync", receipt_def)
receipt_region = dogtag_item[receipt_def:receipt_end] if min(receipt_def, receipt_end) >= 0 else ""
accept_host = receipt_region.find("owner.RequireLiveDogtagHostIdentity(boundary);")
accept_committed = receipt_region.find("DogtagCaseHostContract.RequireCommitted(boundary.Filter);", accept_host + 1)
accept_abandon = receipt_region.find("DogtagCaseHostContract.TryAbandonRollbackAuthority(boundary.Filter, rollbackBaseline)", accept_committed + 1)
rollback_method = receipt_region.find("internal bool TryRollback()", accept_abandon + 1)
rollback_host = receipt_region.find("owner.RequireLiveDogtagHostIdentity(boundary);", rollback_method + 1)
rollback_owned = receipt_region.find("DogtagCaseHostContract.TryRollbackOwnedCaseAddition(boundary.Filter, rollbackBaseline)", rollback_host + 1)
if min(receipt_def, accept_host, accept_committed, accept_abandon, rollback_method, rollback_host, rollback_owned) < 0 or not (
    receipt_def < accept_host < accept_committed < accept_abandon < rollback_method < rollback_host < rollback_owned
):
    violations.append("Dogtag Case post-commit receipt must re-prove exact live host + committed shape before metadata-only acceptance and exact live host before owned rollback")

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
    "var inventoryProperties = inventory.Properties",
    "var slotsCollection = inventoryProperties.Slots",
    "var slotProperties = slot.Properties",
    "var filtersCollection = slotProperties.Filters",
    "!ReferenceEquals(liveInventory, inventory)",
    "!ReferenceEquals(liveInventory.Properties, inventoryProperties)",
    "!ReferenceEquals(liveInventory.Properties?.Slots, slotsCollection)",
    "var liveSlots = slotsCollection",
    "!ReferenceEquals(liveSlots[0], slot)",
    "!ReferenceEquals(liveSlots[0].Properties, slotProperties)",
    "!ReferenceEquals(liveSlots[0].Properties?.Filters, filtersCollection)",
    "var liveGroups = filtersCollection.Take(2).ToArray();",
    "!ReferenceEquals(liveGroups[0], groups[0])",
    "!ReferenceEquals(liveGroups[0].Filter, hostFilter)",
    "requested template identity is not the exact Dogtag Case product",
    "var assort = trader.Assort",
    "var items = assort.Items",
    "var barterScheme = assort.BarterScheme",
    "var loyalLevelItems = assort.LoyalLevelItems",
    "void RequireAssortWrapperIdentity()",
])

if "hostFilter.Contains(templateId)" in dogtag or "hostFilter.Any(x => !Equals(x, templateId))" in dogtag:
    violations.append("Dogtag Case offer must not re-read the mutable Dogtag host after centralized committed-snapshot verification")

publication_call = dogtag.find("RequirePublicationBoundary(templateTable, templateId);")
dogtag_trader = dogtag.find("tradersTable.GetValueOrDefault(")
dogtag_wrapper_capture = dogtag.find("var assort = trader.Assort", dogtag_trader)
dogtag_wrapper_proof = dogtag.find("RequireAssortWrapperIdentity();", dogtag_wrapper_capture)
dogtag_first_mutation = dogtag.find("items.Add(offer);", dogtag_wrapper_proof)
if min(publication_call, dogtag_trader, dogtag_wrapper_capture, dogtag_wrapper_proof, dogtag_first_mutation) < 0 or not (
    publication_call < dogtag_trader < dogtag_wrapper_capture < dogtag_wrapper_proof < dogtag_first_mutation
):
    violations.append("Dogtag Case offer must execute centralized canonical-template + committed-host validation, then capture/prove exact Ragman wrappers before mutation")

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
second_commit_verify = dogtag.find("DogtagCaseHostContract.RequireCommitted(hostFilter);", commit_verify + 1)
trader_inventory_identity = dogtag.find("!ReferenceEquals(liveInventory, inventory)", commit_verify)
trader_inventory_props_identity = dogtag.find("!ReferenceEquals(liveInventory.Properties, inventoryProperties)", trader_inventory_identity)
trader_slots_identity = dogtag.find("!ReferenceEquals(liveInventory.Properties?.Slots, slotsCollection)", trader_inventory_props_identity)
trader_slot_identity = dogtag.find("!ReferenceEquals(liveSlots[0], slot)", trader_slots_identity)
trader_slot_props_identity = dogtag.find("!ReferenceEquals(liveSlots[0].Properties, slotProperties)", trader_slot_identity)
trader_filters_identity = dogtag.find("!ReferenceEquals(liveSlots[0].Properties?.Filters, filtersCollection)", trader_slot_props_identity)
trader_group_identity = dogtag.find("!ReferenceEquals(liveGroups[0], groups[0])", trader_filters_identity)
trader_filter_identity = dogtag.find("!ReferenceEquals(liveGroups[0].Filter, hostFilter)", trader_group_identity)
if min(require_host_def, commit_verify, trader_inventory_identity, trader_inventory_props_identity, trader_slots_identity,
       trader_slot_identity, trader_slot_props_identity, trader_filters_identity, trader_group_identity,
       trader_filter_identity, second_commit_verify, host_method_end) < 0 or not (
    require_host_def < commit_verify < trader_inventory_identity < trader_inventory_props_identity < trader_slots_identity
    < trader_slot_identity < trader_slot_props_identity < trader_filters_identity < trader_group_identity
    < trader_filter_identity < second_commit_verify < host_method_end
):
    violations.append("Dogtag Case exact-host helper must bracket the complete DefaultInventory -> Properties -> Slots -> Dogtag slot -> Properties -> Filters -> group -> HashSet reference chain with committed-content proofs")

if dogtag.count("RequireAssortWrapperIdentity();") < 6:
    violations.append("Dogtag Case trader publication must re-prove the captured Ragman Assort/Items/BarterScheme/LoyalLevelItems wrapper chain through retained and new-offer paths")

if "filter.Add(" in contract or "slots.Add(" in contract:
    violations.append("offer-host validation must be read-only and must not repair equipment filters/slots during trader registration")
if "hostFilter.Add(" in dogtag or "groups[0].Filter.Add(" in dogtag or "slots.Add(" in dogtag:
    violations.append("Dogtag Case offer-host validation must remain read-only; host mutation belongs to DogtagCaseItem preload only")

if violations:
    raise SystemExit("B&A&HB offer-host gate failed:\n" + "\n".join(violations))

print("B&A&HB offer-host gate: OK (Ragman offers require exact live equipment hosts; Dogtag preload host rollback is exact pre-commit-snapshot proven and post-commit receipt authority survives through final canonical proof; trader host proof pins the complete DefaultInventory chain, and trader publication additionally transaction-pins the Ragman Assort/Items/BarterScheme/LoyalLevelItems wrapper chain with ownership-bounded rollback)")
