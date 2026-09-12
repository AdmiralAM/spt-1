namespace SPTEconomy;

public sealed record TraderOwnershipEnforcementGateResult
{
    public required string State { get; init; }
    public required bool AutomaticRewardMutationAllowed { get; init; }
    public required string Reason { get; init; }
}

public static class TraderOwnershipEnforcementGate
{
    public static TraderOwnershipEnforcementGateResult Evaluate(
        string traderId,
        AdmiralTraderRuntimeAdapterReport? admiralTrader)
    {
        if (!string.Equals(traderId, AdmiralTraderGameplayAlphaAdapter.ExpectedTraderId, StringComparison.Ordinal))
        {
            return new TraderOwnershipEnforcementGateResult
            {
                State = "NotAdmiralTrader",
                AutomaticRewardMutationAllowed = true,
                Reason = "Quest is not owned by Admiral Trader; existing provenance/dimension safety remains authoritative.",
            };
        }

        if (admiralTrader is null)
            return Block("AdapterEvidenceMissing", "Admiral Trader quest ownership requires the maintained explicit adapter before automatic reward normalization.");
        if (!admiralTrader.Installed)
            return Block("TraderNotInstalled", "Admiral Trader quest identity is present but the maintained Trader installation was not resolved.");
        if (!admiralTrader.ContractAvailable)
            return Block(admiralTrader.ContractState, "Admiral Trader contract is absent or incompatible; automatic reward normalization fails closed while explicit manual targets remain provenance-gated.");
        if (!string.Equals(admiralTrader.ProductName, AdmiralTraderGameplayAlphaAdapter.ExpectedProductName, StringComparison.Ordinal)
            || !string.Equals(admiralTrader.ModGuid, AdmiralTraderGameplayAlphaAdapter.ExpectedModGuid, StringComparison.Ordinal)
            || !string.Equals(admiralTrader.TraderId, AdmiralTraderGameplayAlphaAdapter.ExpectedTraderId, StringComparison.Ordinal))
            return Block("IdentityMismatch", "Admiral Trader explicit adapter identity does not match the frozen product/modGuid/trader contract.");
        var supportedSchema = admiralTrader.GameplayPolicySchemaVersion == 4 && string.Equals(admiralTrader.ContractState, "LoadedGameplayAlphaV4", StringComparison.Ordinal)
            || admiralTrader.GameplayPolicySchemaVersion == 5 && string.Equals(admiralTrader.ContractState, "LoadedActiveCampaignV5", StringComparison.Ordinal);
        if (!supportedSchema)
            return Block("UnsupportedOwnershipSchema", "Automatic Admiral Trader normalization requires a maintained v4 or v5 ownership contract.");
        if (admiralTrader.OfferCount != admiralTrader.BaselineOfferCount + admiralTrader.RelationshipOfferCount + admiralTrader.CoreOfferCount + admiralTrader.MilestoneOfferCount)
            return Block("UnclassifiedPermanentOffer", "Admiral Trader permanent offers are not completely classified as Baseline/Relationship/Core/Milestone.");
        if (admiralTrader.OfferCount != admiralTrader.BoundedRenewableOfferCount
            || admiralTrader.Offers.Any(offer => offer.Capacity.SupplyBound != RenewableSupplyBound.Bounded))
            return Block("UnboundedPermanentOffer", "Admiral Trader maintained permanent offers must remain finite before ownership-dependent automatic normalization is allowed.");
        if (admiralTrader.Offers.Any(offer => !string.Equals(offer.Source.ProvenanceClass, AdmiralTraderAdapterEvidence.AttributionConfidence, StringComparison.Ordinal)))
            return Block("AttributionDrift", "Admiral Trader ownership evidence is no longer ExplicitAdapter provenance.");
        if (admiralTrader.GameplayPolicySchemaVersion == 4 && (admiralTrader.SpecialWeaponsPermanentOfferAllowed || !admiralTrader.SpecialWeaponsSampleOnly))
            return Block("SampleOnlyContractDrift", "Special-weapons sample-only semantics drifted from the maintained Gameplay Alpha contract.");

        return new TraderOwnershipEnforcementGateResult
        {
            State = "ExplicitGameplayAlphaOwnershipProven",
            AutomaticRewardMutationAllowed = true,
            Reason = "Admiral Trader product, trader identity, maintained stock classes, finite supply and ExplicitAdapter provenance are all proven.",
        };
    }

    private static TraderOwnershipEnforcementGateResult Block(string state, string reason) => new()
    {
        State = state,
        AutomaticRewardMutationAllowed = false,
        Reason = reason,
    };
}
