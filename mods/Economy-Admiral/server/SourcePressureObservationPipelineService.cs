using SPTarkov.DI.Annotations;

namespace SPTEconomy;

public sealed record SourcePressureObservationPipelineResult
{
    public required SourcePressureRuntimeReport SourcePressure { get; init; }
    public required AdmiralTraderRuntimeAdapterReport AdmiralTrader { get; init; }
}

[Injectable]
public sealed class SourcePressureObservationPipelineService(
    FinalDbSourceObservationService finalDbSourceObservationService,
    AdmiralTraderRuntimeAdapterService admiralTraderRuntimeAdapterService,
    SourcePressureRuntimeReportService sourcePressureRuntimeReportService)
{
    public async Task<SourcePressureObservationPipelineResult> RunAsync(
        EconomyConfig config,
        VanillaBaselineSnapshot baseline,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(baseline);
        var finalDb = finalDbSourceObservationService.Build(baseline, cancellationToken);
        var admiralTraderEvidence = await admiralTraderRuntimeAdapterService.RunAsync(config, cancellationToken);
        var sourcePressure = await sourcePressureRuntimeReportService.RunAsync(config, finalDb, admiralTraderEvidence, cancellationToken);
        return new SourcePressureObservationPipelineResult
        {
            SourcePressure = sourcePressure,
            AdmiralTrader = admiralTraderEvidence,
        };
    }
}
