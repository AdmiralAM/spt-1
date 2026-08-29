from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SERVER = ROOT / "server"
violations = []

contract = (SERVER / "WearableOfferHostContract.cs").read_text(encoding="utf-8-sig")
armband = (SERVER / "RuntimeCandidateAssort.cs").read_text(encoding="utf-8-sig")
wallet = (SERVER / "WristWalletAssort.cs").read_text(encoding="utf-8-sig")
dedicated = (SERVER / "DedicatedWearableAssort.cs").read_text(encoding="utf-8-sig")

for token in [
    "internal static void RequireArmBandProduct(TemplateTable templateTable, MongoId productTemplate)",
    'RequireSingleSlot(templateTable, "ArmBand")',
    "filter.Contains(BroadBeltParentTpl)",
    "!filter.Contains(productTemplate)",
    "internal static void RequireDedicatedProducts(TemplateTable templateTable)",
    "RuntimeIdentity.DedicatedBeltWireSlotId",
    "RuntimeIdentity.DedicatedHeadBandWireSlotId",
    "BeltSlotMongoId",
    "HeadBandSlotMongoId",
    "RuntimeIdentity.DedicatedMagazineBeltItemId",
    "RuntimeIdentity.EmergencyHeadBandItemId",
    ".Take(2)",
    "matches.Length != 1",
    "accepted.Count != 1 || !accepted.Contains(allowedTemplate)",
]:
    if token not in contract:
        violations.append(f"offer-host contract missing token {token!r}")

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

if "filter.Add(" in contract or "slots.Add(" in contract:
    violations.append("offer-host validation must be read-only and must not repair equipment filters/slots during trader registration")

if violations:
    raise SystemExit("B&A&HB offer-host gate failed:\n" + "\n".join(violations))

print("B&A&HB offer-host gate: OK (Ragman offers require exact live equipment hosts; ArmBand rejects broad Belt parent; slot15/slot16 require unique exact product contracts; validation is read-only)")
