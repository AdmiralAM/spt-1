using SPTarkov.DI.Annotations;

namespace SPTEconomy;

[Injectable]
public sealed class SourcePressureObservationPipelineService(
    FinalDbSourceObservationService finalDbSourceObservationService,
    AdmiralTraderRuntimeAdapterService admiralTraderRuntimeAdapterService,
    SourcePressureRuntimeReportService sourcePressureRuntimeReportService)
{
    public async Task<SourcePressureRuntimeReport> RunAsync(
        EconomyConfig config,
        VanillaBaselineSnapshot baseline,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(baseline);
        var finalDb = finalDbSourceObservationService.Build(baseline, cancellationToken);
        var admiralTraderEvidence = await admiralTraderRuntimeAdapterService.RunAsync(config, cancellationToken);
        return await sourcePressureRuntimeReportService.RunAsync(config, finalDb, admiralTraderEvidence, cancellationToken);
    }
}
