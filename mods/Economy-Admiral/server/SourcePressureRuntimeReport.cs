namespace SPTEconomy;

public sealed record SourcePressureRuntimeReport
{
    public int SchemaVersion { get; init; } = 1;
    public string EvidenceCoverage { get; init; } = "ExplicitAdaptersOnly";
    public required int LoadedAdapterCount { get; init; }
    public required int SourceCount { get; init; }
    public required int CapacityEvidenceCount { get; init; }
    public required IReadOnlyList<string> LoadedAdapters { get; init; }
    public required IReadOnlyList<ItemSourcePressureEvidence> Items { get; init; }
    public required IReadOnlyList<ItemBoundedSupplyEvidence> Capacity { get; init; }
}

public static class SourcePressureRuntimeReportBuilder
{
    public static SourcePressureRuntimeReport Build(AdmiralTraderRuntimeAdapterReport admiralTrader)
    {
        ArgumentNullException.ThrowIfNull(admiralTrader);
        ArgumentNullException.ThrowIfNull(admiralTrader.Offers);

        if (string.IsNullOrWhiteSpace(admiralTrader.ModGuid))
        {
            throw new InvalidOperationException("Economy Admiral source pressure: adapter modGuid must not be empty.");
        }
        if (!admiralTrader.Installed && (admiralTrader.Offers.Count != 0 || admiralTrader.OfferCount != 0 || admiralTrader.BoundedRenewableOfferCount != 0))
        {
            throw new InvalidOperationException("Economy Admiral source pressure: a not-installed adapter cannot carry offer evidence.");
        }
        if (admiralTrader.Installed && admiralTrader.OfferCount != admiralTrader.Offers.Count)
        {
            throw new InvalidOperationException("Economy Admiral source pressure: adapter OfferCount does not match supplied offers.");
        }

        var boundedOfferCount = admiralTrader.Offers.Count(offer => offer.Capacity.SupplyBound == RenewableSupplyBound.Bounded);
        if (admiralTrader.Installed && admiralTrader.BoundedRenewableOfferCount != boundedOfferCount)
        {
            throw new InvalidOperationException("Economy Admiral source pressure: adapter bounded-offer count does not match supplied capacity evidence.");
        }

        var sources = admiralTrader.Installed
            ? admiralTrader.Offers.Select(offer => offer.Source).ToList()
            : new List<AcquisitionSourceEvidence>();
        var capacities = admiralTrader.Installed
            ? admiralTrader.Offers.Select(offer => offer.Capacity).ToList()
            : new List<RenewableSupplyCapacityEvidence>();
        var loadedAdapters = admiralTrader.Installed
            ? new[] { admiralTrader.ModGuid.Trim() }
            : Array.Empty<string>();

        return new SourcePressureRuntimeReport
        {
            LoadedAdapterCount = loadedAdapters.Length,
            SourceCount = sources.Count,
            CapacityEvidenceCount = capacities.Count,
            LoadedAdapters = loadedAdapters.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            Items = SourcePressureEvidenceAnalyzer.Analyze(sources)
                .OrderBy(item => item.ItemTemplateId, StringComparer.Ordinal)
                .ToArray(),
            Capacity = BoundedSupplyEvidenceAnalyzer.Analyze(sources, capacities)
                .OrderBy(item => item.ItemTemplateId, StringComparer.Ordinal)
                .ToArray(),
        };
    }
}
