from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SERVER = ROOT / "server"
violations = []

contracts = {
    "RuntimeCandidateAssort.cs": [
        "TemplateTable templateTable",
        "var templateId = new MongoId(RuntimeCandidateBeltItem.RuntimeCandidateTpl);",
        "if (!templateTable.Items.ContainsKey(templateId))",
        "Magazine Armband offer refused: exact product template is not registered",
        "Template = templateId",
    ],
    "WristWalletAssort.cs": [
        "TemplateTable templateTable",
        "var templateId = new MongoId(RuntimeIdentity.WristWalletItemId);",
        "if (!templateTable.Items.ContainsKey(templateId))",
        "Wrist Wallet offer refused: exact product template is not registered",
        "Template = templateId",
    ],
    "DedicatedWearableAssort.cs": [
        "TemplateTable templateTable",
        "var beltTemplateId = new MongoId(RuntimeIdentity.DedicatedMagazineBeltItemId);",
        "var headBandTemplateId = new MongoId(RuntimeIdentity.EmergencyHeadBandItemId);",
        "if (!templateTable.Items.ContainsKey(beltTemplateId) || !templateTable.Items.ContainsKey(headBandTemplateId))",
        "both exact product templates must be registered before Ragman assort mutation",
        "beltTemplateId,",
        "headBandTemplateId,",
    ],
    "DogtagCaseAssort.cs": [
        "TemplateTable templateTable",
        "var templateId = new MongoId(RuntimeIdentity.DogtagCaseItemId);",
        "RequirePublicationBoundary(templateTable, templateId);",
        "DogtagCaseItem.RequireCanonicalRegisteredTemplate(templateTable);",
        "RequireExactDogtagHost(templateTable, templateId);",
        "Template = templateId",
        "RollbackOwnedAssortTuple(trader, id, offer, barter, itemAdded, barterAdded, loyaltyAdded);",
        "ReferenceEquals(currentBarter, barter)",
        "ReferenceEquals(trader.Assort.Items[i], offer)",
        "bool ownsItem = ownedItemIndex >= 0;",
        "bool ownsBarter = barterAdded",
        "if (loyaltyAdded && ownsItem && ownsBarter",
    ],
}

for filename, required in contracts.items():
    text = (SERVER / filename).read_text(encoding="utf-8-sig")
    for token in required:
        if token not in text:
            violations.append(f"{filename}: missing exact-template offer boundary token {token!r}")

    trader_lookup = text.find("tradersTable.GetValueOrDefault")
    first_item_add = text.find("trader.Assort.Items.Add(")
    if filename == "DogtagCaseAssort.cs":
        publication_call = text.find("RequirePublicationBoundary(templateTable, templateId);")
        boundary_def = text.find("private static void RequirePublicationBoundary")
        template_check = text.find("DogtagCaseItem.RequireCanonicalRegisteredTemplate(templateTable);", boundary_def)
        host_check = text.find("RequireExactDogtagHost(templateTable, templateId);", boundary_def)
        host_def = text.find("internal static void RequireExactDogtagHost", boundary_def)
        rollback_def = text.find("private static void RollbackOwnedAssortTuple")
        if min(publication_call, trader_lookup, first_item_add) < 0 or not (
            publication_call < trader_lookup < first_item_add
        ):
            violations.append(
                f"{filename}: centralized publication boundary must run before trader lookup and any assort mutation"
            )
        if min(boundary_def, template_check, host_check, host_def) < 0 or not (
            boundary_def < template_check < host_check < host_def
        ):
            violations.append(
                f"{filename}: publication boundary must prove canonical live template then committed Dogtag host"
            )
        if rollback_def < 0:
            violations.append(f"{filename}: owned assort rollback helper is missing")
        if "if (barterAdded) trader.Assort.BarterScheme.Remove(id);" in text:
            violations.append(
                f"{filename}: Dogtag rollback must not delete barter state without proving current reference ownership"
            )
        if "if (itemAdded) trader.Assort.Items.Remove(offer);" in text:
            violations.append(
                f"{filename}: Dogtag rollback must not rely on value equality when exact offer reference ownership is required"
            )
        if "if (loyaltyAdded) trader.Assort.LoyalLevelItems.Remove(id);" in text:
            violations.append(
                f"{filename}: Dogtag loyalty rollback must not delete value-only metadata without reference-owned tuple proof"
            )
        if "templateTable.Items.ContainsKey(templateId)" in text:
            violations.append(
                f"{filename}: existence-only template gating must not replace canonical Dogtag template revalidation"
            )
    else:
        template_check = text.find("templateTable.Items.ContainsKey")
        if min(template_check, trader_lookup, first_item_add) < 0 or not (template_check < trader_lookup < first_item_add):
            violations.append(f"{filename}: exact template existence must be proven before trader lookup and any assort mutation")

if violations:
    raise SystemExit("B&A&HB offer-template boundary gate failed:\n" + "\n".join(violations))

print("B&A&HB offer-template boundary gate: OK (all five Ragman products prove exact registered templates before assort mutation; Dogtag Case uses one centralized canonical live-template + committed-host publication boundary before trader lookup and replacement-safe ownership-bounded rollback for its item/barter/loyalty tuple; dangling/corrupted offers forbidden)")