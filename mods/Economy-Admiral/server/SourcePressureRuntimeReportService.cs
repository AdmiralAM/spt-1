using System.Reflection;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;

namespace SPTEconomy;

[Injectable]
public sealed class SourcePressureRuntimeReportService(ModHelper modHelper)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<SourcePressureRuntimeReport> RunAsync(
        EconomyConfig config,
        FinalDbSourceObservation finalDb,
        AdmiralTraderRuntimeAdapterReport admiralTrader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(finalDb);
        ArgumentNullException.ThrowIfNull(admiralTrader);
        cancellationToken.ThrowIfCancellationRequested();

        var report = SourcePressureRuntimeReportBuilder.Build(finalDb, admiralTrader);
        var economyModPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var reportDirectory = Path.GetDirectoryName(Path.Combine(economyModPath, config.ReportRelativePath))
            ?? Path.Combine(economyModPath, "reports");
        Directory.CreateDirectory(reportDirectory);
        var reportPath = Path.Combine(reportDirectory, "economy-admiral-source-pressure.json");
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        return report;
    }
}
