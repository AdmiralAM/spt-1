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


require(
    SERVER / "RuntimeCandidateOfferContract.cs",
    ["internal const int PriceRoubles = 25000;", "internal const int LoyaltyLevel = 1;"],
    "Magazine Armband offer")

armband_item = require(
    SERVER / "RuntimeCandidateBeltItem.cs",
    [
        'NewItemName = "B&A&HB Magazine Armband"',
        '["en"] = new LocaleDetails',
        'Name = "B&A&HB Magazine Armband"',
        'ShortName = "Mag Armband"',
        'Description = "Compact 1x2 magazine carrier worn in the ArmBand equipment location."',
        '["ru"] = new LocaleDetails',
        'Name = "Повязка под магазины B&A&HB"',
        'ShortName = "Маг. повязка"',
        "CellsH = RuntimeIdentity.CandidateGridColumns",
        "CellsV = RuntimeIdentity.CandidateGridRows",
        "Filter = [BaseClasses.MAGAZINE]",
    ],
    "Magazine Armband item")
if "Runtime Candidate Magazine Belt" in armband_item:
    violations.append("Magazine Armband item: obsolete user-visible Runtime Candidate product name returned")

armband_assort = require(
    SERVER / "RuntimeCandidateAssort.cs",
    [
        "EnsureNoPartialAssortCollision();",
        "trader.Assort.BarterScheme.ContainsKey(id)",
        "trader.Assort.LoyalLevelItems.ContainsKey(id)",
        "trader.Assort.BarterScheme.Add(id,",
        "trader.Assort.LoyalLevelItems.Add(id,",
    ],
    "Magazine Armband persistent assort collision safety")
if "trader.Assort.BarterScheme[id] =" in armband_assort or "trader.Assort.LoyalLevelItems[id] =" in armband_assort:
    violations.append("Magazine Armband assort must not overwrite persistent metadata with dictionary indexers")

wallet_assort = require(
    SERVER / "WristWalletAssort.cs",
    [
        "private const int PriceRoubles = 12500;",
        "private const int LoyaltyLevel = 1;",
        "trader.Assort.BarterScheme.ContainsKey(id)",
        "trader.Assort.LoyalLevelItems.ContainsKey(id)",
        "trader.Assort.BarterScheme.Add(id,",
        "trader.Assort.LoyalLevelItems.Add(id,",
    ],
    "Wrist Wallet offer")
if "trader.Assort.BarterScheme[id] =" in wallet_assort or "trader.Assort.LoyalLevelItems[id] =" in wallet_assort:
    violations.append("Wrist Wallet assort must not overwrite persistent metadata with dictionary indexers")

require(
    SERVER / "WristWalletItem.cs",
    [
        '["en"] = new LocaleDetails',
        'Name = "B&A&HB Wrist Wallet"',
        '["ru"] = new LocaleDetails',
        'Name = "Наручный кошелёк B&A&HB"',
        'ShortName = "Наруч. кошелёк"',
        "Filter = [Money.ROUBLES, Money.DOLLARS, Money.EUROS]",
        "PrepareArmBandExactProductFilter",
        "CommitArmBandExactProducts",
        "BroadBeltParentTpl",
    ],
    "Wrist Wallet item")

dedicated_assort = require(
    SERVER / "DedicatedWearableAssort.cs",
    [
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
        "duplicate item entries own the persistent assort ID",
    ],
    "Dedicated Belt/HeadBand offers")
if "trader.Assort.BarterScheme[assortId] =" in dedicated_assort or "trader.Assort.LoyalLevelItems[assortId] =" in dedicated_assort:
    violations.append("Dedicated wearable assort must not overwrite persistent metadata with dictionary indexers")

belt_prepare = dedicated_assort.find("OfferPlan? beltPlan = PrepareOffer(")
head_prepare = dedicated_assort.find("OfferPlan? headBandPlan = PrepareOffer(")
first_commit = dedicated_assort.find("CommitOffer(")
if min(belt_prepare, head_prepare, first_commit) < 0 or not (belt_prepare < first_commit and head_prepare < first_commit):
    violations.append("both dedicated persistent offers must be prepared/validated before the first assort mutation")

require(
    SRC / "RuntimeIdentity.cs",
    [
        'EmergencyHeadBandGridId = "68ac00000000000000000010"',
        'EmergencyHeadBandCigarettesGridId = "68ac00000000000000000012"',
        "EmergencyHeadBandSplitGridColumns = 1",
        "EmergencyHeadBandSplitGridRows = 1",
    ],
    "Utility HeadBand split identities")

require(
    SRC / "HeadBandUtilityPolicy.cs",
    [
        'internal const string VanillaWallet = "5783c43d2459774bbe137486";',
        'internal const string WzWallet = "60b0f6c058e0b0481a09ad11";',
        "CurrencyWalletTemplateIds",
        "CigaretteTemplateIds",
        "IsCurrencyOrWallet",
        "IsCigarette",
    ],
    "Utility HeadBand split whitelist")

wearable_items = require(
    SERVER / "DedicatedWearableItems.cs",
    [
        '"B&A&HB Magazine Belt"',
        '"B&A&HB Utility HeadBand"',
        "separate currency/wallet and cigarette pockets",
        '"Пояс под магазины B&A&HB"',
        'Name = "Утилитарная налобная повязка B&A&HB"',
        'HeadBandCurrencyGridName = "main"',
        'HeadBandCigarettesGridName = "cigarettes"',
        "RuntimeIdentity.EmergencyHeadBandGridId",
        "RuntimeIdentity.EmergencyHeadBandCigarettesGridId",
        "HeadBandCurrencyWalletWhitelist",
        "HeadBandCigaretteWhitelist",
        "RuntimeIdentity.EmergencyHeadBandSplitGridColumns",
        "RuntimeIdentity.EmergencyHeadBandSplitGridRows",
    ],
    "Dedicated wearable split product")
if "Protected 1x2" in wearable_items:
    violations.append("Utility HeadBand description must not claim unconditional death protection")

migration = require(
    SERVER / "HeadBandSplitGridProfileMigration.cs",
    [
        'MigrationName => "BAndHBHeadBandSplitGridV1"',
        "AbstractProfileMigration",
        "DedicatedWearableItems.HeadBandCurrencyGridName",
        "DedicatedWearableItems.HeadBandCigarettesGridName",
        'item["slotId"] = "hideout"',
        'item.Remove("location")',
        '["x"] = 0',
        '["y"] = 0',
        '["r"] = "Horizontal"',
    ],
    "HeadBand profile migration")
if ".Remove(" in migration and 'item.Remove("location")' not in migration:
    violations.append("HeadBand profile migration must not delete inventory items")

if violations:
    raise SystemExit("B&A&HB product-contract gate failed:\n" + "\n".join(violations))

print(
    "B&A&HB product-contract gate: OK "
    "(EN/RU localized roster; exact pricing/progression and host isolation; split HeadBand pockets; persistent assort IDs reject partial collisions; dedicated pair prepares before commit)"
)
