using SPTEconomy;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void MustFail(string name, Action action)
{
    try { action(); }
    catch (InvalidOperationException) { Console.WriteLine($"PASS {name}"); return; }
    throw new InvalidOperationException($"Expected '{name}' to fail.");
}

const string maintainedPolicy = """
{
  "schemaVersion": 3,
  "productRole": "capability-broker",
  "logistics": {
    "expectedPermanentOfferCount": 7,
    "expectedAmmoPermanentOfferCount": 6,
    "maximumPermanentOfferStockPerReset": 80,
    "maximumAmmoUnitsAcrossPermanentOffersPerReset": 400,
    "maximumAmmoFullResetSpendRub": 300000,
    "maximumReferencePriceMultiplier": 1.3,
    "offersMustBeQuestGated": true,
    "offersMustBeFinite": true,
    "questUnlockLoyaltyLevel": 1,
    "specialWeaponsPermanentOfferAllowed": false,
    "specialWeaponsSampleOnly": true
  },
  "loyalty": {
    "role": "relationship-status-only",
    "capabilityAuthority": false,
    "standingMayBypassQuestGates": false,
    "salesSumMayGateProgression": false
  }
}
""";

var contract = AdmiralTraderAdapterEvidence.ParseGameplayPolicy(maintainedPolicy);
Require(contract.SchemaVersion == 3, "schema mismatch");
Require(contract.ProductRole == "capability-broker", "product role mismatch");
Require(contract.ExpectedPermanentOfferCount == 7, "permanent offer count mismatch");
Require(contract.ExpectedAmmoPermanentOfferCount == 6, "ammo offer count mismatch");
Require(contract.MaximumPermanentOfferStockPerReset == 80, "stock cap mismatch");
Require(contract.MaximumAmmoUnitsAcrossPermanentOffersPerReset == 400, "ammo reset cap mismatch");
Require(contract.MaximumAmmoFullResetSpendRub == 300000, "reset spend cap mismatch");
Require(contract.MaximumReferencePriceMultiplier == 1.3, "price multiplier mismatch");
Require(contract.OffersMustBeQuestGated && contract.OffersMustBeFinite, "offer gating contract mismatch");
Require(contract.QuestUnlockLoyaltyLevel == 1, "quest unlock loyalty mismatch");
Require(contract.SpecialWeaponsSampleOnly && !contract.SpecialWeaponsPermanentOfferAllowed, "special weapons contract mismatch");
Require(contract.LoyaltyRole == "relationship-status-only", "loyalty role mismatch");
Require(!contract.CapabilityAuthority && !contract.StandingMayBypassQuestGates && !contract.SalesSumMayGateProgression, "loyalty must not become capability authority");
Require(AdmiralTraderAdapterEvidence.Owner == "Admiral Trader", "adapter owner mismatch");
Require(AdmiralTraderAdapterEvidence.AttributionConfidence == "ExplicitAdapter", "adapter confidence mismatch");

MustFail("empty policy", () => AdmiralTraderAdapterEvidence.ParseGameplayPolicy(" "));
MustFail("old schema", () => AdmiralTraderAdapterEvidence.ParseGameplayPolicy(maintainedPolicy.Replace("\"schemaVersion\": 3", "\"schemaVersion\": 2")));
MustFail("role drift", () => AdmiralTraderAdapterEvidence.ParseGameplayPolicy(maintainedPolicy.Replace("capability-broker", "general-trader")));
MustFail("quest gate drift", () => AdmiralTraderAdapterEvidence.ParseGameplayPolicy(maintainedPolicy.Replace("\"offersMustBeQuestGated\": true", "\"offersMustBeQuestGated\": false")));
MustFail("finite supply drift", () => AdmiralTraderAdapterEvidence.ParseGameplayPolicy(maintainedPolicy.Replace("\"offersMustBeFinite\": true", "\"offersMustBeFinite\": false")));
MustFail("loyalty authority drift", () => AdmiralTraderAdapterEvidence.ParseGameplayPolicy(maintainedPolicy.Replace("\"capabilityAuthority\": false", "\"capabilityAuthority\": true")));
MustFail("sales sum progression drift", () => AdmiralTraderAdapterEvidence.ParseGameplayPolicy(maintainedPolicy.Replace("\"salesSumMayGateProgression\": false", "\"salesSumMayGateProgression\": true")));
MustFail("special weapons faucet drift", () => AdmiralTraderAdapterEvidence.ParseGameplayPolicy(maintainedPolicy.Replace("\"specialWeaponsPermanentOfferAllowed\": false", "\"specialWeaponsPermanentOfferAllowed\": true")));
MustFail("special weapons sample drift", () => AdmiralTraderAdapterEvidence.ParseGameplayPolicy(maintainedPolicy.Replace("\"specialWeaponsSampleOnly\": true", "\"specialWeaponsSampleOnly\": false")));
MustFail("invalid logistics cap", () => AdmiralTraderAdapterEvidence.ParseGameplayPolicy(maintainedPolicy.Replace("\"maximumPermanentOfferStockPerReset\": 80", "\"maximumPermanentOfferStockPerReset\": 0")));

Console.WriteLine("Economy Admiral Admiral Trader adapter smoke PASS");
