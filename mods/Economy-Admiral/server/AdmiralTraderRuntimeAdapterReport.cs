namespace SPTEconomy;

public sealed record AdmiralTraderRuntimeAdapterReport
{
    public int SchemaVersion { get; init; } = 1;
    public required bool Installed { get; init; }
    public required string ModGuid { get; init; }
    public required string AttributionConfidence { get; init; }
    public int OfferCount { get; init; }
    public int BoundedRenewableOfferCount { get; init; }
    public int? MinimumEffectiveProgressionLevel { get; init; }
    public int? MaximumEffectiveProgressionLevel { get; init; }
    public required IReadOnlyList<AdmiralTraderOfferAdapterEvidence> Offers { get; init; }
}
