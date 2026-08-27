using SPTarkov.DI.Annotations;

namespace SPTEconomy;

[Injectable]
public sealed class SourcePressureObservationPipelineService(
    AdmiralTraderRuntimeAdapterService admiralTraderRuntimeAdapterService,
    SourcePressureRuntimeReportService sourcePressureRuntimeReportService)
{
    public async Task RunAsync(EconomyConfig config, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        var admiralTraderEvidence = await admiralTraderRuntimeAdapterService.RunAsync(config, cancellationToken);
        await sourcePressureRuntimeReportService.RunAsync(config, admiralTraderEvidence, cancellationToken);
    }
}
