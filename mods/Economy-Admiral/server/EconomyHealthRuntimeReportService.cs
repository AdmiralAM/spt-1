using System.Reflection;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;

namespace SPTEconomy;

[Injectable]
public sealed class EconomyHealthRuntimeReportService(ModHelper modHelper)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<EconomyHealthRuntimeReport> RunAsync(EconomyConfig config, SourcePressureRuntimeReport sourcePressure, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(sourcePressure);
        cancellationToken.ThrowIfCancellationRequested();
        var report = EconomyHealthRuntimeReportBuilder.Build(sourcePressure);
        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var reportDirectory = Path.GetDirectoryName(Path.Combine(modPath, config.ReportRelativePath)) ?? Path.Combine(modPath, "reports");
        Directory.CreateDirectory(reportDirectory);
        await File.WriteAllTextAsync(Path.Combine(reportDirectory, "economy-admiral-health.json"), JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        return report;
    }
}
