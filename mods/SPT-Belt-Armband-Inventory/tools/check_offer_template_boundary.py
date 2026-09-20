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
        "if ((!companionMode && !templateTable.Items.ContainsKey(beltTemplateId)) || !templateTable.Items.ContainsKey(headBandTemplateId))",
        "if (companionMode && templateId == beltTemplateId) return null;",
        "both exact product templates must be registered before Ragman assort mutation",
        "beltTemplateId,",
        "headBandTemplateId,",
    ],
}

for filename, required in contracts.items():
    text = (SERVER / filename).read_text(encoding="utf-8-sig")
    for token in required:
        if token not in text:
            violations.append(f"{filename}: missing exact-template offer boundary token {token!r}")
    template_check = text.find("templateTable.Items.ContainsKey")
    trader_lookup = text.find("tradersTable.GetValueOrDefault")
    first_item_add = text.find("trader.Assort.Items.Add(")
    if min(template_check, trader_lookup, first_item_add) < 0 or not (template_check < trader_lookup < first_item_add):
        violations.append(f"{filename}: exact template existence must be proven before trader lookup and any assort mutation")

# Dogtag Case no longer has an active trader publication path. Its persistent
# template remains available for profiles and HeadBand, while any historical
# offer tuple is removed together before the callback returns.
dogtag = (SERVER / "DogtagCaseAssort.cs").read_text(encoding="utf-8-sig")
for token in [
    "new MongoId(RuntimeIdentity.DogtagCaseAssortId)",
    "cleanupAssort.Items.RemoveAll(x => x.Id == offerId);",
    "cleanupAssort.BarterScheme?.Remove(offerId);",
    "cleanupAssort.LoyalLevelItems?.Remove(offerId);",
    "Dogtag Case trader offer is withdrawn",
]:
    if token not in dogtag:
        violations.append(f"DogtagCaseAssort.cs: missing withdrawn-offer boundary token {token!r}")

start = dogtag.find("public Task OnLoadAsync")
dead = dogtag.find("#pragma warning disable CS0162", start)
active = dogtag[start:dead] if min(start, dead) >= 0 else ""
remove_item = active.find("cleanupAssort.Items.RemoveAll(x => x.Id == offerId);")
remove_barter = active.find("cleanupAssort.BarterScheme?.Remove(offerId);")
remove_loyalty = active.find("cleanupAssort.LoyalLevelItems?.Remove(offerId);")
returned = active.find("return Task.CompletedTask;", remove_loyalty)
if min(remove_item, remove_barter, remove_loyalty, returned) < 0 or not (
    remove_item < remove_barter < remove_loyalty < returned
):
    violations.append("DogtagCaseAssort.cs: active path must remove the complete legacy tuple before returning")
for forbidden in ("items.Add(offer);", "RequirePublicationBoundary(", "Template = templateId"):
    if forbidden in active:
        violations.append(f"DogtagCaseAssort.cs: active withdrawn-offer path must not publish: {forbidden}")

if violations:
    raise SystemExit("B&A&HB offer-template boundary gate failed:\n" + "\n".join(violations))

print("B&A&HB offer-template boundary gate: OK (active Ragman products prove exact templates before mutation; Dogtag Case preserves its profile template while atomically withdrawing the complete legacy offer tuple)")
