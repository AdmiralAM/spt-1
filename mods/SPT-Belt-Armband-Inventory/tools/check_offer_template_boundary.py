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
        "var assort = trader.Assort",
        "var items = assort.Items",
        "var barterScheme = assort.BarterScheme",
        "var loyalLevelItems = assort.LoyalLevelItems",
        "void RequireAssortWrapperIdentity()",
        "!ReferenceEquals(trader.Assort, assort)",
        "!ReferenceEquals(trader.Assort?.Items, items)",
        "!ReferenceEquals(trader.Assort?.BarterScheme, barterScheme)",
        "!ReferenceEquals(trader.Assort?.LoyalLevelItems, loyalLevelItems)",
        "items.Add(offer);",
        "barterScheme.Add(id, barter);",
        "loyalLevelItems.Add(id, LoyaltyLevel);",
        "ReferenceEquals(currentBarter, barter)",
        "ReferenceEquals(items[i], offer)",
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
    first_item_add = text.find("items.Add(offer);") if filename == "DogtagCaseAssort.cs" else text.find("trader.Assort.Items.Add(")
    if filename == "DogtagCaseAssort.cs":
        publication_call = text.find("RequirePublicationBoundary(templateTable, templateId);")
        boundary_def = text.find("private static void RequirePublicationBoundary")
        template_check = text.find("DogtagCaseItem.RequireCanonicalRegisteredTemplate(templateTable);", boundary_def)
        host_check = text.find("RequireExactDogtagHost(templateTable, templateId);", boundary_def)
        host_def = text.find("internal static void RequireExactDogtagHost", boundary_def)
        wrapper_capture = text.find("var assort = trader.Assort", trader_lookup)
        wrapper_proof = text.find("RequireAssortWrapperIdentity();", wrapper_capture)
        if min(publication_call, trader_lookup, wrapper_capture, wrapper_proof, first_item_add) < 0 or not (
            publication_call < trader_lookup < wrapper_capture < wrapper_proof < first_item_add
        ):
            violations.append(
                f"{filename}: centralized publication boundary and exact Ragman wrapper capture/proof must run before any assort mutation"
            )
        if min(boundary_def, template_check, host_check, host_def) < 0 or not (
            boundary_def < template_check < host_check < host_def
        ):
            violations.append(
                f"{filename}: publication boundary must prove canonical live template then committed Dogtag host"
            )
        if text.count("RequireAssortWrapperIdentity();") < 6:
            violations.append(
                f"{filename}: Ragman assort wrapper chain must be re-proven throughout retained and new-offer publication"
            )
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

print("B&A&HB offer-template boundary gate: OK (all five Ragman products prove exact registered templates before assort mutation; Dogtag Case additionally transaction-pins the Ragman Assort/Items/BarterScheme/LoyalLevelItems wrapper chain and uses replacement-safe ownership-bounded rollback for its item/barter/loyalty tuple)")
