using System.Reflection;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;

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

[Injectable]
public sealed class SourcePressureRuntimeReportService(ModHelper modHelper)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<SourcePressureRuntimeReport> RunAsync(
        EconomyConfig config,
        AdmiralTraderRuntimeAdapterReport admiralTrader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(admiralTrader);
        cancellationToken.ThrowIfCancellationRequested();

        var sources = admiralTrader.Installed
            ? admiralTrader.Offers.Select(offer => offer.Source).ToList()
            : new List<AcquisitionSourceEvidence>();
        var capacities = admiralTrader.Installed
            ? admiralTrader.Offers.Select(offer => offer.Capacity).ToList()
            : new List<RenewableSupplyCapacityEvidence>();

        var items = SourcePressureEvidenceAnalyzer.Analyze(sources);
        var bounded = BoundedSupplyEvidenceAnalyzer.Analyze(sources, capacities);
        var loadedAdapters = admiralTrader.Installed
            ? new[] { admiralTrader.ModGuid }
            : Array.Empty<string>();

        var report = new SourcePressureRuntimeReport
        {
            LoadedAdapterCount = loadedAdapters.Length,
            SourceCount = sources.Count,
            CapacityEvidenceCount = capacities.Count,
            LoadedAdapters = loadedAdapters.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            Items = items.OrderBy(item => item.ItemTemplateId, StringComparer.Ordinal).ToArray(),
            Capacity = bounded.OrderBy(item => item.ItemTemplateId, StringComparer.Ordinal).ToArray(),
        };

        var economyModPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var reportDirectory = Path.GetDirectoryName(Path.Combine(economyModPath, config.ReportRelativePath))
            ?? Path.Combine(economyModPath, "reports");
        Directory.CreateDirectory(reportDirectory);
        var reportPath = Path.Combine(reportDirectory, "economy-admiral-source-pressure.json");
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        return report;
    }
}
