using System.Text.Json;
using Path = System.IO.Path;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;

namespace SPTEconomy;

[Injectable]
public sealed class RewardUtilityAuditService(
    ModHelper modHelper,
    ISptLogger<RewardUtilityAuditService> logger
)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task RunAsync(
        QuestAnalysisReport analysis,
        VanillaBaselineSnapshot baselineSnapshot,
        CancellationToken cancellationToken)
    {
        if (baselineSnapshot.QuestCount <= 0)
            throw new InvalidOperationException("Economy Admiral reward utility requires a non-empty pristine startup snapshot.");

        var vanillaBenchmark = BuildBenchmark(baselineSnapshot.Quests.Where(row => !row.Restartable).ToList());
        var vanillaRestartableBenchmark = BuildBenchmark(baselineSnapshot.Quests.Where(row => row.Restartable).ToList());

        var rows = analysis.Quests
            .OrderBy(row => row.QuestId, StringComparer.Ordinal)
            .Select(row => BuildRow(
                row,
                row.Restartable && vanillaRestartableBenchmark.QuestSamples > 0
                    ? vanillaRestartableBenchmark
                    : vanillaBenchmark))
            .ToList();

        var report = new RewardUtilityAuditReport
        {
            SchemaVersion = 3,
            UtilityScoringApplied = false,
            BenchmarkSource = "PristineStartupSnapshot",
            SourceAnalysisSchemaVersion = analysis.SchemaVersion,
            Note = $"Projection of the unified typed final-quest analysis against pristine startup reward benchmarks captured at priority {baselineSnapshot.CapturePriority}. No second TemplateTable quest scan, correction overlay, composite score, or ruble conversion is applied.",
            Vanilla = vanillaBenchmark,
            VanillaRestartable = vanillaRestartableBenchmark,
            Quests = rows,
        };

        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(RewardUtilityAuditService).Assembly);
        var reportPath = Path.GetFullPath(Path.Combine(modPath, "reports", "economy-admiral-reward-utility.json"));
        var modRoot = Path.GetFullPath(modPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!reportPath.StartsWith(modRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Economy Admiral reward utility report path must stay inside the mod directory.");

        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        logger.Info($"[Economy Admiral] reward utility projection complete: finalQuests={rows.Count}, pristineQuests={baselineSnapshot.QuestCount}; report={reportPath}");
    }

    private static QuestRewardUtilityRow BuildRow(QuestAnalysisRow row, RewardUtilityBenchmark benchmark) => new()
    {
        QuestId = row.QuestId,
        QuestName = row.QuestName,
        TraderId = row.TraderId,
        IsVanillaTraderQuest = row.IsVanillaTraderQuest,
        Restartable = row.Restartable,
        Experience = row.Experience,
        TraderStanding = row.TraderStanding,
        TraderUnlocks = row.TraderUnlocks,
        AssortmentUnlocks = row.AssortmentUnlocks,
        ProductionSchemeUnlocks = row.ProductionSchemeUnlocks,
        XpVsVanillaMedian = RatioOrNull(row.Experience, benchmark.MedianXp),
        StandingVsVanillaMedian = RatioOrNull(Math.Abs(row.TraderStanding), benchmark.MedianAbsoluteStanding),
        TraderUnlocksVsVanillaMedian = RatioOrNull(row.TraderUnlocks, benchmark.MedianPositiveTraderUnlocks),
        AssortmentUnlocksVsVanillaMedian = RatioOrNull(row.AssortmentUnlocks, benchmark.MedianPositiveAssortmentUnlocks),
        ProductionUnlocksVsVanillaMedian = RatioOrNull(row.ProductionSchemeUnlocks, benchmark.MedianPositiveProductionSchemeUnlocks),
    };

    private static double? RatioOrNull(double value, double baseline) => value > 0 && baseline > 0
        ? Math.Round(value / baseline, 4)
        : null;

    private static RewardUtilityBenchmark BuildBenchmark(IReadOnlyCollection<VanillaQuestBaselineRow> rows)
    {
        var xp = Positive(rows.Select(row => row.Experience));
        var standing = Positive(rows.Select(row => Math.Abs(row.TraderStanding)));
        var traderUnlocks = Positive(rows.Select(row => (double)row.TraderUnlocks));
        var assortmentUnlocks = Positive(rows.Select(row => (double)row.AssortmentUnlocks));
        var productionUnlocks = Positive(rows.Select(row => (double)row.ProductionSchemeUnlocks));

        return new RewardUtilityBenchmark
        {
            QuestSamples = rows.Count,
            XpSamples = xp.Count,
            MedianXp = Percentile(xp, 0.50),
            P90Xp = Percentile(xp, 0.90),
            StandingSamples = standing.Count,
            MedianAbsoluteStanding = Percentile(standing, 0.50),
            P90AbsoluteStanding = Percentile(standing, 0.90),
            TraderUnlockQuestSamples = traderUnlocks.Count,
            MedianPositiveTraderUnlocks = Percentile(traderUnlocks, 0.50),
            P90PositiveTraderUnlocks = Percentile(traderUnlocks, 0.90),
            AssortmentUnlockQuestSamples = assortmentUnlocks.Count,
            MedianPositiveAssortmentUnlocks = Percentile(assortmentUnlocks, 0.50),
            P90PositiveAssortmentUnlocks = Percentile(assortmentUnlocks, 0.90),
            ProductionSchemeUnlockQuestSamples = productionUnlocks.Count,
            MedianPositiveProductionSchemeUnlocks = Percentile(productionUnlocks, 0.50),
            P90PositiveProductionSchemeUnlocks = Percentile(productionUnlocks, 0.90),
        };
    }

    private static List<double> Positive(IEnumerable<double> values) => values.Where(value => value > 0).OrderBy(value => value).ToList();

    private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0) return 0;
        if (sortedValues.Count == 1) return Math.Round(sortedValues[0], 4);
        var position = (sortedValues.Count - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper) return Math.Round(sortedValues[lower], 4);
        var fraction = position - lower;
        return Math.Round(sortedValues[lower] + ((sortedValues[upper] - sortedValues[lower]) * fraction), 4);
    }
}

public sealed record RewardUtilityAuditReport
{
    public required int SchemaVersion { get; init; }
    public required bool UtilityScoringApplied { get; init; }
    public required string BenchmarkSource { get; init; }
    public required int SourceAnalysisSchemaVersion { get; init; }
    public required string Note { get; init; }
    public required RewardUtilityBenchmark Vanilla { get; init; }
    public required RewardUtilityBenchmark VanillaRestartable { get; init; }
    public required List<QuestRewardUtilityRow> Quests { get; init; }
}

public sealed record RewardUtilityBenchmark
{
    public required int QuestSamples { get; init; }
    public required int XpSamples { get; init; }
    public required double MedianXp { get; init; }
    public required double P90Xp { get; init; }
    public required int StandingSamples { get; init; }
    public required double MedianAbsoluteStanding { get; init; }
    public required double P90AbsoluteStanding { get; init; }
    public required int TraderUnlockQuestSamples { get; init; }
    public required double MedianPositiveTraderUnlocks { get; init; }
    public required double P90PositiveTraderUnlocks { get; init; }
    public required int AssortmentUnlockQuestSamples { get; init; }
    public required double MedianPositiveAssortmentUnlocks { get; init; }
    public required double P90PositiveAssortmentUnlocks { get; init; }
    public required int ProductionSchemeUnlockQuestSamples { get; init; }
    public required double MedianPositiveProductionSchemeUnlocks { get; init; }
    public required double P90PositiveProductionSchemeUnlocks { get; init; }
}

public sealed record QuestRewardUtilityRow
{
    public required string QuestId { get; init; }
    public required string QuestName { get; init; }
    public required string TraderId { get; init; }
    public required bool IsVanillaTraderQuest { get; init; }
    public required bool Restartable { get; init; }
    public required double Experience { get; init; }
    public required double TraderStanding { get; init; }
    public required int TraderUnlocks { get; init; }
    public required int AssortmentUnlocks { get; init; }
    public required int ProductionSchemeUnlocks { get; init; }
    public required double? XpVsVanillaMedian { get; init; }
    public required double? StandingVsVanillaMedian { get; init; }
    public required double? TraderUnlocksVsVanillaMedian { get; init; }
    public required double? AssortmentUnlocksVsVanillaMedian { get; init; }
    public required double? ProductionUnlocksVsVanillaMedian { get; init; }
}
