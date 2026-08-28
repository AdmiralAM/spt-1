namespace SPTEconomy;

public sealed record SourcePressureRuntimeReport
{
    public int SchemaVersion { get; init; } = 2;
    public string EvidenceCoverage { get; init; } = "FinalDbCore+ExplicitAdaptersWithExplicitUnknownChannels";
    public required int LoadedAdapterCount { get; init; }
    public required int SourceCount { get; init; }
    public required int CapacityEvidenceCount { get; init; }
    public required IReadOnlyList<string> LoadedAdapters { get; init; }
    public required IReadOnlyList<ChannelObservationCoverage> ChannelCoverage { get; init; }
    public required IReadOnlyList<ItemSourcePressureEvidence> Items { get; init; }
    public required IReadOnlyList<ItemBoundedSupplyEvidence> Capacity { get; init; }
    public required EffectiveAcquisitionGraphResult AcquisitionGraph { get; init; }
    public required double StartupMilliseconds { get; init; }
}

public static class SourcePressureRuntimeReportBuilder
{
    public static SourcePressureRuntimeReport Build(FinalDbSourceObservation finalDb, AdmiralTraderRuntimeAdapterReport admiralTrader)
    {
        ArgumentNullException.ThrowIfNull(finalDb);
        ArgumentNullException.ThrowIfNull(admiralTrader);
        ArgumentNullException.ThrowIfNull(admiralTrader.Offers);

        ValidateAdapter(admiralTrader);
        var evidenceUsable = admiralTrader.Installed && admiralTrader.ContractAvailable;
        var adapterSources = evidenceUsable ? admiralTrader.Offers.Select(offer => offer.Source).ToArray() : Array.Empty<AcquisitionSourceEvidence>();
        var capacities = evidenceUsable ? admiralTrader.Offers.Select(offer => offer.Capacity).ToArray() : Array.Empty<RenewableSupplyCapacityEvidence>();
        var loadedAdapters = evidenceUsable ? new[] { admiralTrader.ModGuid.Trim() } : Array.Empty<string>();

        var sources = finalDb.Sources.Concat(adapterSources)
            .OrderBy(source => source.ItemTemplateId, StringComparer.Ordinal)
            .ThenBy(source => source.Channel)
            .ThenBy(source => source.SourceId, StringComparer.Ordinal)
            .ToArray();
        var coverage = finalDb.ChannelCoverage.Select(row => row.Channel == AcquisitionChannel.Other
                ? row with { ObservedSourceCount = adapterSources.Length, State = evidenceUsable ? "ExplicitAdapterObserved" : "ExplicitAdaptersNoneLoaded" }
                : row)
            .OrderBy(row => row.Channel).ToArray();

        return new SourcePressureRuntimeReport
        {
            LoadedAdapterCount = loadedAdapters.Length,
            SourceCount = sources.Length,
            CapacityEvidenceCount = capacities.Length,
            LoadedAdapters = loadedAdapters.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            ChannelCoverage = coverage,
            Items = SourcePressureEvidenceAnalyzer.Analyze(sources).OrderBy(item => item.ItemTemplateId, StringComparer.Ordinal).ToArray(),
            Capacity = BoundedSupplyEvidenceAnalyzer.Analyze(sources, capacities).OrderBy(item => item.ItemTemplateId, StringComparer.Ordinal).ToArray(),
            AcquisitionGraph = finalDb.AcquisitionGraph,
            StartupMilliseconds = finalDb.StartupMilliseconds,
        };
    }

    private static void ValidateAdapter(AdmiralTraderRuntimeAdapterReport admiralTrader)
    {
        if (string.IsNullOrWhiteSpace(admiralTrader.ModGuid))
            throw new InvalidOperationException("Economy Admiral source pressure: adapter modGuid must not be empty.");
        if (!admiralTrader.Installed && admiralTrader.ContractAvailable)
            throw new InvalidOperationException("Economy Admiral source pressure: a not-installed adapter cannot have an available contract.");
        if (!admiralTrader.ContractAvailable && (admiralTrader.Offers.Count != 0 || admiralTrader.OfferCount != 0 || admiralTrader.BoundedRenewableOfferCount != 0))
            throw new InvalidOperationException("Economy Admiral source pressure: unavailable adapter contract cannot carry offer evidence.");
        if (admiralTrader.ContractAvailable && admiralTrader.OfferCount != admiralTrader.Offers.Count)
            throw new InvalidOperationException("Economy Admiral source pressure: adapter OfferCount does not match supplied offers.");
        var boundedOfferCount = admiralTrader.Offers.Count(offer => offer.Capacity.SupplyBound == RenewableSupplyBound.Bounded);
        if (admiralTrader.ContractAvailable && admiralTrader.BoundedRenewableOfferCount != boundedOfferCount)
            throw new InvalidOperationException("Economy Admiral source pressure: adapter bounded-offer count does not match supplied capacity evidence.");
    }
}
