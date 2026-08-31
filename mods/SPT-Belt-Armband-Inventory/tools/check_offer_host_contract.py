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
    "DogtagCaseHostContract.CaptureVanillaEntries(vanillaEntries);",
]:
    if token not in dogtag_item:
        violations.append(f"Dogtag Case preload missing exact B&A&HB cross-host/preservation token {token!r}")

for token in [
    "private static HashSet<MongoId>? capturedVanillaEntries;",
    "var snapshot = acceptedTemplates.ToHashSet();",
    "capturedVanillaEntries.SetEquals(snapshot)",
    "foreach (MongoId entry in capturedVanillaEntries)",
    "if (!currentFilter.Contains(entry))",
]:
    if token not in dogtag_snapshot:
        violations.append(f"Dogtag Case host snapshot contract missing token {token!r}")

capture = dogtag_item.find("DogtagCaseHostContract.CaptureVanillaEntries(vanillaEntries);")
commit = dogtag_item.find("CommitDogtagSlotExposure(dogtagSlotFilter);")
if min(capture, commit) < 0 or capture > commit:
    violations.append("Dogtag Case must snapshot every pre-mutation non-owned host entry before exposing the container")

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
    "RequireExactDogtagHost(templateTable, templateId);",
    'string.Equals(x.Name, DogtagSlotName, StringComparison.Ordinal)',
    "groups.Length != 1",
    "groups[0].Filter.Count < 2",
    "!groups[0].Filter.Contains(templateId)",
    "DogtagCaseHostContract.RequirePreserved(groups[0].Filter);",
    "!groups[0].Filter.Any(x => !Equals(x, templateId))",
    "PersistentIdentityManifest.IsOwnedTemplate(acceptedId)",
    "!string.Equals(acceptedId, RuntimeIdentity.DogtagCaseItemId, StringComparison.Ordinal)",
    "vanilla Dogtag host is contaminated by another owned product template",
]:
    if token not in dogtag:
        violations.append(f"Dogtag Case offer missing exact host/preservation/isolation token {token!r}")

dogtag_host = dogtag.find("RequireExactDogtagHost(templateTable, templateId);")
dogtag_trader = dogtag.find("tradersTable.GetValueOrDefault(")
dogtag_first_mutation = dogtag.find("trader.Assort.Items.Add(")
dogtag_snapshot_verify = dogtag.find("DogtagCaseHostContract.RequirePreserved(groups[0].Filter);")
if min(dogtag_host, dogtag_trader, dogtag_first_mutation) < 0 or not (
    dogtag_host < dogtag_trader < dogtag_first_mutation
):
    violations.append("Dogtag Case offer must prove exact vanilla Dogtag host and preserved vanilla acceptance before resolving/mutating Ragman assort")
if dogtag_snapshot_verify < 0 or dogtag_snapshot_verify > dogtag_trader:
    violations.append("Dogtag Case must prove the complete pre-mutation host snapshot before Ragman is resolved")

if "filter.Add(" in contract or "slots.Add(" in contract:
    violations.append("offer-host validation must be read-only and must not repair equipment filters/slots during trader registration")
if "groups[0].Filter.Add(" in dogtag or "slots.Add(" in dogtag:
    violations.append("Dogtag Case offer-host validation must remain read-only; host mutation belongs to DogtagCaseItem preload only")

if violations:
    raise SystemExit("B&A&HB offer-host gate failed:\n" + "\n".join(violations))

print("B&A&HB offer-host gate: OK (Ragman offers require exact live equipment hosts; ArmBand rejects broad-parent and exact Belt/HeadBand cross-host contamination; slot15/slot16 require unique exact product contracts; Dogtag Case snapshots every pre-mutation non-owned Dogtag host entry and trader registration proves the complete snapshot still survives while rejecting owned cross-host contamination; validation is read-only)")