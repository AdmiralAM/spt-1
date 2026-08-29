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

armband_offer = require(
    SERVER / "RuntimeCandidateOfferContract.cs",
    [
        "internal const int PriceRoubles = 25000;",
        "internal const int LoyaltyLevel = 1;",
    ],
    "Magazine Armband offer")

armband_item = require(
    SERVER / "RuntimeCandidateBeltItem.cs",
    [
        'NewItemName = "B&A&HB Magazine Armband"',
        'Name = "B&A&HB Magazine Armband"',
        'ShortName = "Mag Armband"',
        'Description = "Compact 1x2 magazine carrier worn in the ArmBand equipment location."',
        "CellsH = RuntimeIdentity.CandidateGridColumns",
        "CellsV = RuntimeIdentity.CandidateGridRows",
        "Filter = [BaseClasses.MAGAZINE]",
    ],
    "Magazine Armband item")
if "Runtime Candidate Magazine Belt" in armband_item:
    violations.append("Magazine Armband item: obsolete user-visible Runtime Candidate product name returned")

wrist = require(
    SERVER / "WristWalletAssort.cs",
    [
        "private const int PriceRoubles = 12500;",
        "private const int LoyaltyLevel = 1;",
    ],
    "Wrist Wallet offer")

dedicated = require(
    SERVER / "DedicatedWearableAssort.cs",
    [
        "private const int BeltLoyaltyLevel = 2;",
        "private const int HeadBandLoyaltyLevel = 1;",
        "private const int BeltPrice = 45000;",
        "private const int HeadBandPrice = 25000;",
    ],
    "Dedicated Belt/HeadBand offers")

headband_policy = require(
    SRC / "HeadBandUtilityPolicy.cs",
    [
        'internal const string VanillaWallet = "5783c43d2459774bbe137486";',
        'internal const string WzWallet = "60b0f6c058e0b0481a09ad11";',
        "VanillaWallet,",
        "WzWallet",
    ],
    "Utility HeadBand whitelist")

wearable_items = require(
    SERVER / "DedicatedWearableItems.cs",
    [
        '"B&A&HB Magazine Belt"',
        '"B&A&HB Utility HeadBand"',
        '"Death protection follows the B&A&HB F12 setting."',
        "RuntimeIdentity.DedicatedMagazineBeltGridColumns",
        "RuntimeIdentity.DedicatedMagazineBeltGridRows",
        "RuntimeIdentity.EmergencyHeadBandGridColumns",
        "RuntimeIdentity.EmergencyHeadBandGridRows",
    ],
    "Dedicated wearable product copy")
if "Protected 1x2" in wearable_items:
    violations.append("Utility HeadBand description must not claim unconditional death protection")

if violations:
    raise SystemExit("B&A&HB product-contract gate failed:\n" + "\n".join(violations))

print(
    "B&A&HB product-contract gate: OK "
    "(Wrist Wallet LL1/12.5k; Magazine Armband LL1/25k; "
    "Utility HeadBand LL1/25k; Magazine Belt LL2/45k; two-wallet HeadBand whitelist)"
)
