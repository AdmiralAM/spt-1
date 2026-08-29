namespace SPTEconomy;

public sealed record AdmiralTraderRuntimeAdapterReport
{
    public int SchemaVersion { get; init; } = 3;
    public required bool Installed { get; init; }
    public required bool ContractAvailable { get; init; }
    public required string ContractState { get; init; }
    public string? ContractDiagnostic { get; init; }
    public required string ProductName { get; init; }
    public required string ModGuid { get; init; }
    public required string TraderId { get; init; }
    public int? GameplayPolicySchemaVersion { get; init; }
    public required string AttributionConfidence { get; init; }
    public int OfferCount { get; init; }
    public int BaselineOfferCount { get; init; }
    public int RelationshipOfferCount { get; init; }
    public int MilestoneOfferCount { get; init; }
    public int BoundedRenewableOfferCount { get; init; }
    public bool RelationshipStockAllowed { get; init; }
    public bool SpecialWeaponsPermanentOfferAllowed { get; init; }
    public bool SpecialWeaponsSampleOnly { get; init; }
    public int? MinimumEffectiveProgressionLevel { get; init; }
    public int? MaximumEffectiveProgressionLevel { get; init; }
    public required IReadOnlyList<AdmiralTraderOfferAdapterEvidence> Offers { get; init; }
}
