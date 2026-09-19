from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SERVER = ROOT / "server"
SRC = ROOT / "src"
violations = []


def require(path: Path, tokens, label: str):
    if not path.exists():
        violations.append(f"{label}: missing file {path.name}")
        return ""
    text = path.read_text(encoding="utf-8-sig")
    for token in tokens:
        if token not in text:
            violations.append(f"{label}: missing contract token {token!r}")
    return text


require(SERVER / "RuntimeCandidateOfferContract.cs",
        ["internal const int PriceRoubles = 25000;", "internal const int LoyaltyLevel = 1;"],
        "Magazine Armband offer")
require(SERVER / "RuntimeCandidateBeltItem.cs", [
    'NewItemName = "B&A&HB Magazine Armband"',
    "CellsH = RuntimeIdentity.CandidateGridColumns",
    "CellsV = RuntimeIdentity.CandidateGridRows",
    "Filter = [BaseClasses.MAGAZINE]",
], "Magazine Armband item")
require(SERVER / "WristWalletAssort.cs", [
    "private const int PriceRoubles = 12500;",
    "private const int LoyaltyLevel = 1;",
], "Wrist Wallet offer")
require(SERVER / "WristWalletItem.cs", [
    'Name = "B&A&HB Wrist Wallet"',
    "Filter = [Money.ROUBLES, Money.DOLLARS, Money.EUROS]",
    "PrepareArmBandExactProductFilter",
    "CommitArmBandExactProducts",
], "Wrist Wallet item")

dedicated = require(SERVER / "DedicatedWearableAssort.cs", [
    "private const int BeltLoyaltyLevel = 2;",
    "private const int HeadBandLoyaltyLevel = 1;",
    "private const int BeltPrice = 45000;",
    "private const int HeadBandPrice = 25000;",
    "OfferPlan? beltPlan = PrepareOffer(",
    "OfferPlan? headBandPlan = PrepareOffer(",
    "if (beltPlan != null) CommitOffer(beltPlan);",
    "if (headBandPlan != null) CommitOffer(headBandPlan);",
], "Dedicated Belt/HeadBand offers")
belt_prepare = dedicated.find("OfferPlan? beltPlan = PrepareOffer(")
head_prepare = dedicated.find("OfferPlan? headBandPlan = PrepareOffer(")
first_commit = dedicated.find("CommitOffer(")
if min(belt_prepare, head_prepare, first_commit) < 0 or not (belt_prepare < first_commit and head_prepare < first_commit):
    violations.append("both dedicated persistent offers must be prepared before first mutation")

# Dogtag Case is now a HeadBand utility container. Preserve exact source-grid
# parity and persistent identity, and keep vanilla Dogtag exposure unreachable.
dogtag_item = require(SERVER / "DogtagCaseItem.cs", [
    'NewItemName = "B&A&HB Dogtag Case"',
    'new("5c093e3486f77430cb02e593")',
    "Filters = copiedFilters",
    "if (!DogtagCaseCanonicalIdentityLease.IsSourceGridParent(sourceGrid.Parent))",
    "if (!templateTable.Items.TryGetValue(DogtagCaseTpl, out var created))",
    "ValidateExisting(created, source);",
    "RequireCanonicalRegisteredTemplate(templateTable);",
    "without vanilla Dogtag-slot exposure",
    "!string.Equals(grid.Name, sourceGrid.Name, StringComparison.Ordinal)",
    "!Equals(grid.Prototype, sourceGrid.Prototype)",
    "actual.MinCount != expected.MinCount",
    "actual.MaxCount != expected.MaxCount",
    "actual.MaxWeight != expected.MaxWeight",
    "actual.IsSortingTable != expected.IsSortingTable",
], "Dogtag Case item")
if "BaseClasses.DOGTAG" in dogtag_item:
    violations.append("Dogtag Case must copy canonical filter groups rather than broaden to BaseClasses.DOGTAG")
item_start = dogtag_item.find("public Task OnLoadAsync")
item_end = dogtag_item.find("private DogtagHostBoundary PrepareDogtagSlotFilter", item_start)
item_active = dogtag_item[item_start:item_end] if min(item_start, item_end) >= 0 else ""
for forbidden in ("PrepareDogtagSlotFilter(", "CommitDogtagSlotExposure("):
    if forbidden in item_active:
        violations.append(f"Dogtag Case active preload must remain HeadBand-only: {forbidden}")

dogtag_assort = require(SERVER / "DogtagCaseAssort.cs", [
    "new MongoId(RuntimeIdentity.DogtagCaseAssortId)",
    "cleanupAssort.Items.RemoveAll(x => x.Id == offerId);",
    "cleanupAssort.BarterScheme?.Remove(offerId);",
    "cleanupAssort.LoyalLevelItems?.Remove(offerId);",
    "Dogtag Case trader offer is withdrawn",
], "Dogtag Case withdrawn offer")
assort_start = dogtag_assort.find("public Task OnLoadAsync")
assort_dead = dogtag_assort.find("#pragma warning disable CS0162", assort_start)
if "items.Add(offer);" in dogtag_assort[assort_start:assort_dead]:
    violations.append("Dogtag Case active trader path must not republish the withdrawn offer")

require(SRC / "RuntimeIdentity.cs", [
    'EmergencyHeadBandGridId = "68ac00000000000000000010"',
    'EmergencyHeadBandCigarettesGridId = "68ac00000000000000000012"',
    'DogtagCaseItemId = "68ac00000000000000000013"',
    'DogtagCaseGridId = "68ac00000000000000000014"',
    'DogtagCaseAssortId = "68ac00000000000000000015"',
    "EmergencyHeadBandSplitGridColumns = 1",
    "EmergencyHeadBandSplitGridRows = 1",
], "Utility HeadBand + Dogtag Case persistent identities")

require(SRC / "HeadBandUtilityPolicy.cs", [
    'internal const string VanillaWallet = "5783c43d2459774bbe137486";',
    'internal const string WzWallet = "60b0f6c058e0b0481a09ad11";',
    "CurrencyWalletTemplateIds",
    "DogtagCase = RuntimeIdentity.DogtagCaseItemId",
    "IsCurrencyOrWallet",
    "IsDogtagCase",
    "IsCigarette",
], "Utility HeadBand whitelist")

wearable = require(SERVER / "DedicatedWearableItems.cs", [
    '"B&A&HB Magazine Belt"',
    '"B&A&HB Utility HeadBand"',
    "separate currency/wallet and Dogtag Case pockets",
    'HeadBandCurrencyGridName = "main"',
    'HeadBandDogtagCaseGridName = "cigarettes"',
    "RuntimeIdentity.EmergencyHeadBandGridId",
    "RuntimeIdentity.EmergencyHeadBandCigarettesGridId",
    "HeadBandDogtagCaseWhitelist",
    "BuildCurrencyWalletWhitelist()",
], "Dedicated wearable split product")
if "Protected 1x2" in wearable:
    violations.append("Utility HeadBand description must not claim unconditional death protection")

migration = require(SERVER / "HeadBandSplitGridProfileMigration.cs", [
    'MigrationName => "BAndHBHeadBandSplitGridV1"',
    "DedicatedWearableItems.HeadBandCurrencyGridName",
    "DedicatedWearableItems.HeadBandCigarettesGridName",
    "Unknown and overflow children stay attached",
    'item["slotId"] = slotId;',
    '["x"] = 0',
    '["y"] = 0',
    '["r"] = "Horizontal"',
], "HeadBand profile migration")
if 'item.Remove("location")' in migration:
    violations.append("HeadBand V1 migration must not eject unknown or overflow contents")

if violations:
    raise SystemExit("B&A&HB product-contract gate failed:\n" + "\n".join(violations))

print("B&A&HB product-contract gate: OK (wearable pricing/identity/filter contracts retained; Dogtag Case is a canonical persistent HeadBand container with withdrawn legacy offer; split HeadBand migration preserves unknown and overflow children)")
