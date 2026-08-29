using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class TraderOwnershipEnforcementGateSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var offer = new AdmiralTraderOfferAdapterEvidence
        {
            OfferId = "mile1",
            ItemTemplateId = "tpl-mile",
            StockClass = "Milestone",
            GateKind = "Quest",
            QuestGateId = "q1",
            LoyaltyLevel = 1,
            StockPerReset = 10,
            BuyRestrictionPerReset = 2,
            Source = new AcquisitionSourceEvidence
            {
                ItemTemplateId = "tpl-mile",
                SourceId = "admiral-trader:mile1",
                Channel = AcquisitionChannel.TraderPurchase,
                Renewable = true,
                EarliestProgressionLevel = 12,
                ProvenanceClass = "ExplicitAdapter",
            },
            Capacity = new RenewableSupplyCapacityEvidence
            {
                ItemTemplateId = "tpl-mile",
                SourceId = "admiral-trader:mile1",
                Channel = AcquisitionChannel.TraderPurchase,
                SupplyBound = RenewableSupplyBound.Bounded,
                MaxUnitsPerReset = 10,
                MaxAcquisitionsPerReset = 2,
            },
        };

        var valid = new AdmiralTraderRuntimeAdapterReport
        {
            Installed = true,
            ContractAvailable = true,
            ContractState = "LoadedGameplayAlphaV4",
            ProductName = "Admiral Trader",
            ModGuid = "com.admiralam.spt.admiraltrader",
            TraderId = "d5c27bb3169f8dfbc13f6b69",
            GameplayPolicySchemaVersion = 4,
            AttributionConfidence = "ExplicitAdapter",
            OfferCount = 1,
            BaselineOfferCount = 0,
            RelationshipOfferCount = 0,
            MilestoneOfferCount = 1,
            BoundedRenewableOfferCount = 1,
            RelationshipStockAllowed = true,
            SpecialWeaponsPermanentOfferAllowed = false,
            SpecialWeaponsSampleOnly = true,
            MinimumEffectiveProgressionLevel = 12,
            MaximumEffectiveProgressionLevel = 12,
            Offers = [offer],
        };

        var nonAdmiral = TraderOwnershipEnforcementGate.Evaluate("other-trader", null);
        Require(nonAdmiral.AutomaticRewardMutationAllowed && nonAdmiral.State == "NotAdmiralTrader", "non-Admiral ownership must not widen or block existing policy");

        var accepted = TraderOwnershipEnforcementGate.Evaluate(AdmiralTraderGameplayAlphaAdapter.ExpectedTraderId, valid);
        Require(accepted.AutomaticRewardMutationAllowed && accepted.State == "ExplicitGameplayAlphaOwnershipProven", "valid explicit Gameplay Alpha ownership should permit existing automatic reward policy");

        Blocked("missing evidence", TraderOwnershipEnforcementGate.Evaluate(AdmiralTraderGameplayAlphaAdapter.ExpectedTraderId, null));
        Blocked("contract unavailable", TraderOwnershipEnforcementGate.Evaluate(AdmiralTraderGameplayAlphaAdapter.ExpectedTraderId, valid with { ContractAvailable = false, ContractState = "ContractUnavailable", OfferCount = 0, MilestoneOfferCount = 0, BoundedRenewableOfferCount = 0, Offers = [] }));
        Blocked("legacy schema", TraderOwnershipEnforcementGate.Evaluate(AdmiralTraderGameplayAlphaAdapter.ExpectedTraderId, valid with { GameplayPolicySchemaVersion = 3, ContractState = "LoadedPrototypeV3" }));
        Blocked("identity mismatch", TraderOwnershipEnforcementGate.Evaluate(AdmiralTraderGameplayAlphaAdapter.ExpectedTraderId, valid with { ProductName = "Other Trader" }));
        Blocked("class mismatch", TraderOwnershipEnforcementGate.Evaluate(AdmiralTraderGameplayAlphaAdapter.ExpectedTraderId, valid with { MilestoneOfferCount = 0 }));
        Blocked("unbounded offer", TraderOwnershipEnforcementGate.Evaluate(AdmiralTraderGameplayAlphaAdapter.ExpectedTraderId, valid with
        {
            BoundedRenewableOfferCount = 0,
            Offers = [offer with { Capacity = offer.Capacity with { SupplyBound = RenewableSupplyBound.Unbounded, MaxUnitsPerReset = null, MaxAcquisitionsPerReset = null } }],
        }));
        Blocked("provenance drift", TraderOwnershipEnforcementGate.Evaluate(AdmiralTraderGameplayAlphaAdapter.ExpectedTraderId, valid with
        {
            Offers = [offer with { Source = offer.Source with { ProvenanceClass = "Heuristic" } }],
        }));
        Blocked("sample-only drift", TraderOwnershipEnforcementGate.Evaluate(AdmiralTraderGameplayAlphaAdapter.ExpectedTraderId, valid with { SpecialWeaponsPermanentOfferAllowed = true }));

        Console.WriteLine("Economy Admiral Trader ownership enforcement gate smoke PASS");
    }

    private static void Blocked(string name, TraderOwnershipEnforcementGateResult result)
    {
        Require(!result.AutomaticRewardMutationAllowed, $"{name} must fail closed");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Economy Admiral Trader ownership gate smoke: {message}");
    }
}
