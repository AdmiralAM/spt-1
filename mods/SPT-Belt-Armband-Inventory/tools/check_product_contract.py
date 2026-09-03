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

armband_item = require(SERVER / "RuntimeCandidateBeltItem.cs", [
    'NewItemName = "B&A&HB Magazine Armband"',
    'Name = "B&A&HB Magazine Armband"',
    'Name = "Повязка под магазины B&A&HB"',
    "CellsH = RuntimeIdentity.CandidateGridColumns",
    "CellsV = RuntimeIdentity.CandidateGridRows",
    "Filter = [BaseClasses.MAGAZINE]",
], "Magazine Armband item")
if "Runtime Candidate Magazine Belt" in armband_item:
    violations.append("Magazine Armband item: obsolete Runtime Candidate product name returned")


def require_collision_safe_assort(path, label, captured_wrappers=False):
    if captured_wrappers:
        tokens = [
            "var matches = items.Where(x => x.Id == id).Take(2).ToArray();",
            "if (matches.Length > 1)",
            "barterScheme.ContainsKey(id)",
            "loyalLevelItems.ContainsKey(id)",
            "barterScheme.Add(id,",
            "loyalLevelItems.Add(id,",
            "var assort = trader.Assort",
            "var items = assort.Items",
            "var barterScheme = assort.BarterScheme",
            "var loyalLevelItems = assort.LoyalLevelItems",
            "bool IsAssortWrapperIdentityCurrent()",
            "ReferenceEquals(trader.Assort, assort)",
            "ReferenceEquals(trader.Assort?.Items, items)",
            "ReferenceEquals(trader.Assort?.BarterScheme, barterScheme)",
            "ReferenceEquals(trader.Assort?.LoyalLevelItems, loyalLevelItems)",
            "void RequireAssortWrapperIdentity()",
            "if (!IsAssortWrapperIdentityCurrent())",
        ]
    else:
        tokens = [
            "trader.Assort.Items.Where(x => x.Id == id).Take(2).ToArray();",
            "if (matches.Length > 1)",
            "trader.Assort.BarterScheme.ContainsKey(id)",
            "trader.Assort.LoyalLevelItems.ContainsKey(id)",
            "trader.Assort.BarterScheme.Add(id,",
            "trader.Assort.LoyalLevelItems.Add(id,",
        ]
    text = require(path, tokens, label)
    if "trader.Assort.Items.FirstOrDefault(x => x.Id == id)" in text or "items.FirstOrDefault(x => x.Id == id)" in text:
        violations.append(f"{label} must reject duplicate persistent item entries")
    unsafe_indexers = (
        "trader.Assort.BarterScheme[id] =" in text
        or "trader.Assort.LoyalLevelItems[id] =" in text
        or (captured_wrappers and ("barterScheme[id] =" in text or "loyalLevelItems[id] =" in text))
    )
    if unsafe_indexers:
        violations.append(f"{label} must not overwrite persistent metadata with dictionary indexers")
    if captured_wrappers and text.count("RequireAssortWrapperIdentity();") < 6:
        violations.append(f"{label} must re-prove captured Ragman wrapper identity throughout publication")
    if captured_wrappers:
        rollback_tokens = [
            "if (!IsAssortWrapperIdentityCurrent()) throw;\n                loyalLevelItems.Remove(id);",
            "if (!IsAssortWrapperIdentityCurrent()) throw;\n                barterScheme.Remove(id);",
            "if (!IsAssortWrapperIdentityCurrent()) throw;\n                items.RemoveAt(ownedItemIndex);",
        ]
        for token in rollback_tokens:
            if token not in text:
                violations.append(f"{label} must re-prove exact live Ragman wrapper authority immediately before rollback mutation")
    return text


require_collision_safe_assort(SERVER / "RuntimeCandidateAssort.cs", "Magazine Armband assort")
wallet_assort = require_collision_safe_assort(SERVER / "WristWalletAssort.cs", "Wrist Wallet assort")
require(SERVER / "WristWalletAssort.cs", ["private const int PriceRoubles = 12500;", "private const int LoyaltyLevel = 1;"], "Wrist Wallet offer")
require(SERVER / "WristWalletItem.cs", [
    'Name = "B&A&HB Wrist Wallet"',
    'Name = "Наручный кошелёк B&A&HB"',
    "Filter = [Money.ROUBLES, Money.DOLLARS, Money.EUROS]",
    "PrepareArmBandExactProductFilter",
    "CommitArmBandExactProducts",
    "BroadBeltParentTpl",
], "Wrist Wallet item")

dedicated_assort = require(SERVER / "DedicatedWearableAssort.cs", [
    "private const int BeltLoyaltyLevel = 2;",
    "private const int HeadBandLoyaltyLevel = 1;",
    "private const int BeltPrice = 45000;",
    "private const int HeadBandPrice = 25000;",
    "OfferPlan? beltPlan = PrepareOffer(",
    "OfferPlan? headBandPlan = PrepareOffer(",
    "if (beltPlan != null) CommitOffer(beltPlan);",
    "if (headBandPlan != null) CommitOffer(headBandPlan);",
    "trader.Assort.BarterScheme.ContainsKey(assortId)",
    "trader.Assort.LoyalLevelItems.ContainsKey(assortId)",
    "trader.Assort.BarterScheme.Add(plan.AssortId,",
    "trader.Assort.LoyalLevelItems.Add(plan.AssortId,",
], "Dedicated Belt/HeadBand offers")
if "trader.Assort.BarterScheme[assortId] =" in dedicated_assort or "trader.Assort.LoyalLevelItems[assortId] =" in dedicated_assort:
    violations.append("Dedicated wearable assort must not overwrite persistent metadata")
belt_prepare = dedicated_assort.find("OfferPlan? beltPlan = PrepareOffer(")
head_prepare = dedicated_assort.find("OfferPlan? headBandPlan = PrepareOffer(")
first_commit = dedicated_assort.find("CommitOffer(")
if min(belt_prepare, head_prepare, first_commit) < 0 or not (belt_prepare < first_commit and head_prepare < first_commit):
    violations.append("both dedicated persistent offers must be prepared/validated before first mutation")

# Dogtag item: preserve canonical container parity and enforce the exact
# canonical source-grid ownership plus DefaultInventory -> Dogtag slot -> sole
# filter group -> HashSet publication boundary.
dogtag_item = require(SERVER / "DogtagCaseItem.cs", [
    'NewItemName = "B&A&HB Dogtag Case"',
    'Name = "B&A&HB Dogtag Case"',
    'Name = "Жетонница B&A&HB"',
    'new("5c093e3486f77430cb02e593")',
    "Filters = copiedFilters",
    "if (!Equals(sourceGrid.Parent, SourceDogtagCaseTpl))",
    "|| !Equals(sourceGrid.Parent, SourceDogtagCaseTpl)",
    "if (!templateTable.Items.TryGetValue(DogtagCaseTpl, out var created))",
    "ValidateExisting(created, source);",
    "DogtagHostBoundary dogtagHost = PrepareDogtagSlotFilter();",
    "private sealed class DogtagHostBoundary(object inventory, object slot, object filterGroup, HashSet<MongoId> filter)",
    "private void RequireLiveDogtagHostIdentity(DogtagHostBoundary boundary)",
    "!ReferenceEquals(liveInventory, boundary.Inventory)",
    "!ReferenceEquals(liveSlots[0], boundary.Slot)",
    "!ReferenceEquals(liveGroups[0], boundary.FilterGroup)",
    "!ReferenceEquals(liveGroups[0].Filter, boundary.Filter)",
    "CommitDogtagSlotExposure(dogtagHost, CancellationToken.None);",
    "DogtagCaseHostContract.RequirePreserved(filter);",
    "DogtagCaseHostContract.CaptureRollbackBaseline(filter)",
    "addedHere = filter.Add(DogtagCaseTpl);",
    "DogtagCaseHostContract.RequireCommitted(filter);",
    "DogtagCaseHostContract.TryRollbackOwnedCaseAddition(filter, rollbackBaseline)",
    "ambiguous/foreign current host state is not blindly rewritten",
    "!string.Equals(grid.Name, sourceGrid.Name, StringComparison.Ordinal)",
    "!Equals(grid.Prototype, sourceGrid.Prototype)",
    "actual.MinCount != expected.MinCount",
    "actual.MaxCount != expected.MaxCount",
    "actual.MaxWeight != expected.MaxWeight",
    "actual.IsSortingTable != expected.IsSortingTable",
], "Dogtag Case item")
if "BaseClasses.DOGTAG" in dogtag_item:
    violations.append("Dogtag Case must copy canonical filter groups rather than broaden to BaseClasses.DOGTAG")
if "filter.Remove(DogtagCaseTpl);" in dogtag_item:
    violations.append("Dogtag host exposure must not use unconditional value-only rollback")

create_call = dogtag_item.find("customItemService.CreateItemFromClone(details)")
pre_create_cancel = dogtag_item.rfind("cancellationToken.ThrowIfCancellationRequested();", 0, create_call)
post_create_validation = dogtag_item.find("ValidateExisting(created, source);", create_call)
post_create_canonical = dogtag_item.find("RequireCanonicalRegisteredTemplate(templateTable);", post_create_validation)
post_create_exposure = dogtag_item.find("CommitDogtagSlotExposure(dogtagHost, CancellationToken.None);", post_create_canonical)
if min(create_call, pre_create_cancel, post_create_validation, post_create_canonical, post_create_exposure) < 0 or not (
    pre_create_cancel < create_call < post_create_validation < post_create_canonical < post_create_exposure
):
    violations.append("Dogtag Case must cancel before irreversible clone, then value-revalidate, canonical-reference-reprove and finish exact host publication")

existing_check = dogtag_item.find("if (templateTable.Items.TryGetValue(DogtagCaseTpl, out var existing))")
existing_validate = dogtag_item.find("ValidateExisting(existing, source);", existing_check)
existing_canonical = dogtag_item.find("RequireCanonicalRegisteredTemplate(templateTable);", existing_validate)
existing_commit = dogtag_item.find("CommitDogtagSlotExposure(dogtagHost, cancellationToken);", existing_canonical)
if min(existing_check, existing_validate, existing_canonical, existing_commit) < 0 or not (
    existing_check < existing_validate < existing_canonical < existing_commit
):
    violations.append("pre-existing Dogtag Case path must value-revalidate, canonical-reference-reprove and retain cancellation-aware exact-host exposure")

commit_def = dogtag_item.find("private void CommitDogtagSlotExposure(DogtagHostBoundary boundary, CancellationToken cancellationToken)")
commit_end = dogtag_item.find("public static void RequireCanonicalRegisteredTemplate", commit_def)
commit_region = dogtag_item[commit_def:commit_end] if min(commit_def, commit_end) >= 0 else ""
first_identity = commit_region.find("RequireLiveDogtagHostIdentity(boundary);")
preserved = commit_region.find("DogtagCaseHostContract.RequirePreserved(filter);")
second_identity = commit_region.find("RequireLiveDogtagHostIdentity(boundary);", first_identity + 1)
rollback_baseline = commit_region.find("DogtagCaseHostContract.CaptureRollbackBaseline(filter)", second_identity + 1)
owned_add = commit_region.find("addedHere = filter.Add(DogtagCaseTpl);", rollback_baseline + 1)
committed = commit_region.find("DogtagCaseHostContract.RequireCommitted(filter);", owned_add + 1)
final_identity = commit_region.find("RequireLiveDogtagHostIdentity(boundary);", committed + 1)
rollback = commit_region.find("DogtagCaseHostContract.TryRollbackOwnedCaseAddition(filter, rollbackBaseline)", final_identity + 1)
if min(first_identity, preserved, second_identity, rollback_baseline, owned_add, committed, final_identity, rollback) < 0 or not (
    first_identity < preserved < second_identity < rollback_baseline < owned_add < committed < final_identity < rollback
):
    violations.append("Dogtag exact-host exposure ordering drifted from live/preserved/live -> detached rollback baseline -> owned Add -> committed/live -> proven owned rollback")
if "filter.Remove(DogtagCaseTpl);" in commit_region:
    violations.append("Dogtag exact-host exposure must refuse value-only rollback when foreign/current host authority is ambiguous")
if commit_region.count("cancellationToken.ThrowIfCancellationRequested();") < 4:
    violations.append("Dogtag exact-host exposure must keep cancellation checks around owned mutation/commit boundary")

dogtag_assort = require_collision_safe_assort(SERVER / "DogtagCaseAssort.cs", "Dogtag Case assort", captured_wrappers=True)
require(SERVER / "DogtagCaseAssort.cs", [
    "private const int PriceRoubles = 50000;",
    "private const int LoyaltyLevel = 2;",
    "new MongoId(RuntimeIdentity.DogtagCaseItemId)",
    "new MongoId(RuntimeIdentity.DogtagCaseAssortId)",
    "RequireExactDogtagHost(templateTable, templateId);",
    "var hostFilter = groups[0].Filter;",
    "hostFilter == null || hostFilter.Count < 2",
    "DogtagCaseHostContract.RequireCommitted(hostFilter);",
    "requested template identity is not the exact Dogtag Case product",
    "ReferenceEquals(items[i], offer)",
    "ReferenceEquals(currentBarter, barter)",
    "loyalLevelItems.Remove(id);",
    "barterScheme.Remove(id);",
    "items.RemoveAt(ownedItemIndex);",
], "Dogtag Case offer")
if "hostFilter.Contains(templateId)" in dogtag_assort or "hostFilter.Any(x => !Equals(x, templateId))" in dogtag_assort:
    violations.append("Dogtag trader publication must consume centralized one-point-in-time committed host proof")

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
    "CurrencyWalletTemplateIds", "CigaretteTemplateIds", "IsCurrencyOrWallet", "IsCigarette",
], "Utility HeadBand split whitelist")

wearable_items = require(SERVER / "DedicatedWearableItems.cs", [
    '"B&A&HB Magazine Belt"', '"B&A&HB Utility HeadBand"',
    "separate currency/wallet and cigarette pockets",
    '"Пояс под магазины B&A&HB"',
    'Name = "Утилитарная налобная повязка B&A&HB"',
    'HeadBandCurrencyGridName = "main"', 'HeadBandCigarettesGridName = "cigarettes"',
    "RuntimeIdentity.EmergencyHeadBandGridId", "RuntimeIdentity.EmergencyHeadBandCigarettesGridId",
    "HeadBandCurrencyWalletWhitelist", "HeadBandCigaretteWhitelist",
], "Dedicated wearable split product")
if "Protected 1x2" in wearable_items:
    violations.append("Utility HeadBand description must not claim unconditional death protection")

migration = require(SERVER / "HeadBandSplitGridProfileMigration.cs", [
    'MigrationName => "BAndHBHeadBandSplitGridV1"', "AbstractProfileMigration",
    "DedicatedWearableItems.HeadBandCurrencyGridName", "DedicatedWearableItems.HeadBandCigarettesGridName",
    'item["slotId"] = "hideout"', 'item.Remove("location")', '["x"] = 0', '["y"] = 0', '["r"] = "Horizontal"',
], "HeadBand profile migration")
if ".Remove(" in migration and 'item.Remove("location")' not in migration:
    violations.append("HeadBand profile migration must not delete inventory items")

if violations:
    raise SystemExit("B&A&HB product-contract gate failed:\n" + "\n".join(violations))

print("B&A&HB product-contract gate: OK (five-product pricing/identity/filter contracts; collision-safe assorts; Dogtag preload uses exact pre-commit-snapshot proven rollback; Dogtag Ragman wrappers transaction-pinned with live-wrapper-authority ownership-bounded rollback; split HeadBand; canonical Dogtag clone/host parity retained)")