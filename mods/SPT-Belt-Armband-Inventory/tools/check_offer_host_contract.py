from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SERVER = ROOT / "server"
violations = []

contract = (SERVER / "WearableOfferHostContract.cs").read_text(encoding="utf-8-sig")
armband_item = (SERVER / "WristWalletItem.cs").read_text(encoding="utf-8-sig")
armband = (SERVER / "RuntimeCandidateAssort.cs").read_text(encoding="utf-8-sig")
wallet = (SERVER / "WristWalletAssort.cs").read_text(encoding="utf-8-sig")
dedicated = (SERVER / "DedicatedWearableAssort.cs").read_text(encoding="utf-8-sig")
dogtag_item = (SERVER / "DogtagCaseItem.cs").read_text(encoding="utf-8-sig")
dogtag_assort = (SERVER / "DogtagCaseAssort.cs").read_text(encoding="utf-8-sig")
compatibility = (SERVER / "SecureContainerCompatibility.cs").read_text(encoding="utf-8-sig")


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
    "accepted.Count != allowedTemplates.Count || allowedTemplates.Any(template => !accepted.Contains(template))",
])

require_tokens("ArmBand registration exact cross-host isolation", armband_item, [
    "DedicatedMagazineBeltTpl = new(RuntimeIdentity.DedicatedMagazineBeltItemId)",
    "UtilityHeadBandTpl = new(RuntimeIdentity.EmergencyHeadBandItemId)",
    "filter.Contains(DedicatedMagazineBeltTpl) || filter.Contains(UtilityHeadBandTpl)",
    "refusing Belt/HeadBand host overlap",
])

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

# v0.3 keeps the persistent item and exact canonical clone, while HeadBand owns
# exposure. Historical Dogtag-host helpers remain dormant for profile continuity.
require_tokens("Dogtag Case HeadBand-only item contract", dogtag_item, [
    "public const string TemplateId = RuntimeIdentity.DogtagCaseItemId;",
    "public const string GridId = RuntimeIdentity.DogtagCaseGridId;",
    'new("5c093e3486f77430cb02e593")',
    "sourceGrids == null || sourceGrids.Length != 1",
    "DogtagCaseCanonicalIdentityLease.IsSourceGridParent(sourceGrid.Parent)",
    "new HashSet<MongoId>(filter.Filter!)",
    "customItemService.CreateItemFromClone(details)",
    "RequireCanonicalRegisteredTemplate(templateTable);",
    "without vanilla Dogtag-slot exposure",
])

item_start = dogtag_item.find("public Task OnLoadAsync")
item_end = dogtag_item.find("private DogtagHostBoundary PrepareDogtagSlotFilter", item_start)
item_active = dogtag_item[item_start:item_end] if min(item_start, item_end) >= 0 else ""
if not item_active:
    violations.append("Dogtag Case active preload boundary could not be isolated")
for forbidden in ("PrepareDogtagSlotFilter(", "CommitDogtagSlotExposure("):
    if forbidden in item_active:
        violations.append(f"Dogtag Case active preload must not expose the item through vanilla Dogtag host: {forbidden}")

require_tokens("Dogtag Case withdrawn offer contract", dogtag_assort, [
    "new MongoId(RuntimeIdentity.DogtagCaseAssortId)",
    "cleanupAssort.Items.RemoveAll(x => x.Id == offerId);",
    "cleanupAssort.BarterScheme?.Remove(offerId);",
    "cleanupAssort.LoyalLevelItems?.Remove(offerId);",
    "Dogtag Case trader offer is withdrawn",
    "return Task.CompletedTask;",
])

assort_start = dogtag_assort.find("public Task OnLoadAsync")
assort_dead = dogtag_assort.find("#pragma warning disable CS0162", assort_start)
assort_active = dogtag_assort[assort_start:assort_dead] if min(assort_start, assort_dead) >= 0 else ""
withdraw = assort_active.find("cleanupAssort.Items.RemoveAll(x => x.Id == offerId);")
returned = assort_active.find("return Task.CompletedTask;", withdraw)
if min(withdraw, returned) < 0 or withdraw > returned:
    violations.append("Dogtag Case active trader path must withdraw the legacy offer before returning")
for forbidden in ("RequirePublicationBoundary(", "items.Add(offer)", "CommitDogtagSlotExposure("):
    if forbidden in assort_active:
        violations.append(f"Dogtag Case active trader path must not publish or expose the withdrawn product: {forbidden}")

require_tokens("Dogtag Case HeadBand admission", compatibility, [
    "HashSet<MongoId> dogtagCases = FindDogtagCases();",
    "ExtendHeadBand(DedicatedWearableItems.HeadBandDogtagCaseGridName, dogtagCases)",
    "private HashSet<MongoId> FindDogtagCases()",
    "RuntimeIdentity.DogtagCaseItemId",
])

if "filter.Add(" in contract or "slots.Add(" in contract:
    violations.append("offer-host validation must be read-only and must not repair equipment filters/slots during trader registration")

if violations:
    raise SystemExit("B&A&HB offer-host gate failed:\n" + "\n".join(violations))

print("B&A&HB offer-host gate: OK (exact wearable hosts retained; Dogtag Case keeps its persistent canonical item, is admitted only by HeadBand, and its legacy Ragman offer is withdrawn)")
