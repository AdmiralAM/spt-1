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
        "DogtagCaseItem.RequireCanonicalRegisteredTemplate(templateTable);",
        "RequireExactDogtagHost(templateTable, templateId);",
        "Template = templateId",
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
        template_check = text.find("DogtagCaseItem.RequireCanonicalRegisteredTemplate(templateTable);")
        host_check = text.find("RequireExactDogtagHost(templateTable, templateId);")
        if min(template_check, host_check, trader_lookup, first_item_add) < 0 or not (
            template_check < host_check < trader_lookup < first_item_add
        ):
            violations.append(
                f"{filename}: canonical live template and committed Dogtag host must both be proven before trader lookup and any assort mutation"
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

print("B&A&HB offer-template boundary gate: OK (all five Ragman products prove exact registered templates before assort mutation; Dogtag Case additionally revalidates canonical live geometry/filter plus committed vanilla Dogtag host before trader lookup; dangling/corrupted offers forbidden)")
