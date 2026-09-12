from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src"
SERVER = ROOT / "server"
violations = []
compat = (SRC / "PackNStrapCompatibility.cs").read_text(encoding="utf-8-sig")
client = (SRC / "Plugin.cs").read_text(encoding="utf-8-sig")

for token in [
    'PluginGuid = "com.wtt.packnstrap"',
    '"WTT-PackNStrapServer"',
    "PackNStrapCompatibility.IsClientPresent(Chainloader.PluginInfos.Keys)",
    "if (legacyBeltSlotDetected && !packNStrapDetected)",
    "accepts Pack 'n' Strap's required Trenchfoot-BeltSlot owner",
    "runtimeTypePatches.TryInstall(!packNStrapDetected)",
    "new DedicatedEquipmentSlotPatches(Logger.LogInfo, Logger.LogWarning, !packNStrapDetected)",
    "protectionSyncPump = StartCoroutine(SyncProtectionSettingsBounded());",
]:
    if token not in compat + client:
        violations.append(f"missing companion contract token: {token!r}")

companion_gate = client.find("if (packNStrapDetected)", client.find("gridWindowSizingPatches ="))
for owner in [
    "lootPatches = new LootPriorityPatches", "unloadPatches = new UnloadPriorityPatches",
    "scavPatches = new ScavBeltPatches", "fastAccessSlotPatches = new FastAccessSlotPatches",
    "slotMergePatches = new SlotMergePatches", "pickupPatches = new PickupSlotPatches",
    "paymentPatches = new PaymentSlotPatches", "buildValidationPatches = new EquipmentBuildValidationPatches",
]:
    if companion_gate < 0 or client.find(owner) < companion_gate:
        violations.append(f"companion gate must return before {owner}")

for filename in ["RuntimeCandidateAssort.cs", "WristWalletAssort.cs"]:
    text = (SERVER / filename).read_text(encoding="utf-8-sig")
    if "if (PackNStrapCompatibility.IsServerPresentNow())" not in text or "return Task.CompletedTask;" not in text:
        violations.append(f"{filename} does not suppress its standard owner")

for filename, item_token in [
    ("RuntimeCandidateBeltItem.cs", "legacy Magazine Armband template"),
    ("WristWalletItem.cs", "legacy Wrist Wallet template"),
]:
    text = (SERVER / filename).read_text(encoding="utf-8-sig")
    if "return Task.CompletedTask;" in text[text.find("public Task OnLoadAsync"):text.find("if (!templateTable", text.find("public Task OnLoadAsync"))]:
        violations.append(f"{filename} drops a published persistent template in companion mode")
    if item_token not in text or "if (!companionMode) CommitArmBandExactProducts" not in text and filename == "WristWalletItem.cs":
        violations.append(f"{filename} does not retain its legacy template without foreign filter publication")

for filename in ["DedicatedWearableItems.cs", "DedicatedEquipmentSlotRegistration.cs", "DedicatedWearableAssort.cs", "WearableTaxonomyRegistration.cs"]:
    text = (SERVER / filename).read_text(encoding="utf-8-sig")
    if "PackNStrapCompatibility.IsServerPresentNow()" not in text or "companionMode" not in text:
        violations.append(f"{filename} does not split Belt from HeadBand")

wearable_items = (SERVER / "DedicatedWearableItems.cs").read_text(encoding="utf-8-sig")
if "if (!companionMode) EnsureSingleGridItem(" in wearable_items:
    violations.append("companion mode drops the persistent B&A Magazine Belt template and can invalidate existing profiles")
for token in [
    "legacy Magazine Belt template retained for profile safety without a B&A slot or offer",
    "EnsureSingleGridItem(\n            RuntimeIdentity.DedicatedMagazineBeltItemId",
]:
    if token not in wearable_items:
        violations.append(f"missing legacy Magazine Belt profile-safety contract: {token!r}")

taxonomy = (SERVER / "WearableTaxonomyRegistration.cs").read_text(encoding="utf-8-sig")
if 'PrepareNode(BeltParentTpl, "BAndHBCustomBeltItem", SearchableParentTpl, companionMode)' in taxonomy:
    violations.append("companion mode drops the persistent parent of legacy B&A item templates")

if violations:
    raise SystemExit("B&A&HB Pack 'n' Strap companion gate failed:\n" + "\n".join(violations))
print("B&A&HB Pack 'n' Strap companion gate: OK (standard Belt/ArmBand owners suppressed; HeadBand/Dogtag/protection retained)")
