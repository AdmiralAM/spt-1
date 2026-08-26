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

        var sources = admiralTrader.Installed
            ? admiralTrader.Offers.Select(offer => offer.Source).ToList()
            : new List<AcquisitionSourceEvidence>();
        var capacities = admiralTrader.Installed
            ? admiralTrader.Offers.Select(offer => offer.Capacity).ToList()
            : new List<RenewableSupplyCapacityEvidence>();
        var loadedAdapters = admiralTrader.Installed
            ? new[] { admiralTrader.ModGuid }
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
