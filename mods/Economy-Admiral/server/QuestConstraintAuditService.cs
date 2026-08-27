using System.Text.Json;
using Path = System.IO.Path;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;

namespace SPTEconomy;

[Injectable]
public sealed class QuestConstraintAuditService(
    ModHelper modHelper,
    ISptLogger<QuestConstraintAuditService> logger
)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task RunAsync(
        QuestAnalysisReport analysis,
        VanillaBaselineSnapshot baselineSnapshot,
        CancellationToken cancellationToken)
    {
        if (baselineSnapshot.QuestCount <= 0)
            throw new InvalidOperationException("Economy Admiral quest constraint audit requires a non-empty pristine startup snapshot.");

        var rows = analysis.Quests
            .OrderBy(row => row.QuestId, StringComparer.Ordinal)
            .Select(row => new QuestConstraintRow
            {
                QuestId = row.QuestId,
                QuestName = row.QuestName,
                TraderId = row.TraderId,
                IsVanillaTraderQuest = row.IsVanillaTraderQuest,
                Restartable = row.Restartable,
                ObjectiveConditionCount = row.ObjectiveConditionCount,
                TimedConditionCount = row.TimedConditionCount,
                OneSessionConditionCount = row.OneSessionConditionCount,
                FoundInRaidConditionCount = row.FoundInRaidConditionCount,
                PlantConditionCount = row.PlantConditionCount,
                DistanceConstraintCount = row.DistanceConstraintCount,
                DaytimeConstraintCount = row.DaytimeConstraintCount,
                StructuredConstraintCount = row.StructuredConstraintCount,
            })
            .ToList();

        var report = new QuestConstraintAuditReport
        {
            SchemaVersion = 2,
            ConstraintsAffectRewardAllowance = false,
            BenchmarkSource = "PristineStartupSnapshot",
            SourceAnalysisSchemaVersion = analysis.SchemaVersion,
            Note = $"Projection of structured constraint counts already captured by unified final-quest analysis, measured against pristine startup benchmarks captured at priority {baselineSnapshot.CapturePriority}. No second TemplateTable quest scan or correction overlay is applied.",
            Vanilla = BuildBenchmark(baselineSnapshot.Quests.Where(row => !row.Restartable).ToList()),
            VanillaRestartable = BuildBenchmark(baselineSnapshot.Quests.Where(row => row.Restartable).ToList()),
            Quests = rows,
        };

        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(QuestConstraintAuditService).Assembly);
        var reportPath = Path.GetFullPath(Path.Combine(modPath, "reports", "economy-admiral-quest-constraints.json"));
        var modRoot = Path.GetFullPath(modPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!reportPath.StartsWith(modRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Economy Admiral quest constraint report path must stay inside the mod directory.");

        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        logger.Info($"[Economy Admiral] quest constraint projection complete: finalQuests={rows.Count}, pristineQuests={baselineSnapshot.QuestCount}; report={reportPath}");
    }

    private static QuestConstraintBenchmark BuildBenchmark(IReadOnlyCollection<VanillaQuestBaselineRow> rows)
    {
        var constraintCounts = rows.Select(row => (double)row.StructuredConstraintCount).OrderBy(value => value).ToList();
        var timedCounts = rows.Select(row => (double)row.TimedConditionCount).OrderBy(value => value).ToList();
        var oneSessionCounts = rows.Select(row => (double)row.OneSessionConditionCount).OrderBy(value => value).ToList();
        var firCounts = rows.Select(row => (double)row.FoundInRaidConditionCount).OrderBy(value => value).ToList();
        var positiveTimes = rows.Where(row => row.StrictestCompletionTimeSeconds > 0).Select(row => row.StrictestCompletionTimeSeconds).OrderBy(value => value).ToList();
        var positiveDistances = rows.Where(row => row.LongestDistanceConstraint > 0).Select(row => row.LongestDistanceConstraint).OrderBy(value => value).ToList();

        return new QuestConstraintBenchmark
        {
            QuestSamples = rows.Count,
            MedianStructuredConstraintCount = Percentile(constraintCounts, 0.50),
            P90StructuredConstraintCount = Percentile(constraintCounts, 0.90),
            MedianTimedConditionCount = Percentile(timedCounts, 0.50),
            P90TimedConditionCount = Percentile(timedCounts, 0.90),
            MedianOneSessionConditionCount = Percentile(oneSessionCounts, 0.50),
            P90OneSessionConditionCount = Percentile(oneSessionCounts, 0.90),
            MedianFoundInRaidConditionCount = Percentile(firCounts, 0.50),
            P90FoundInRaidConditionCount = Percentile(firCounts, 0.90),
            TimedQuestSamples = positiveTimes.Count,
            MedianPositiveCompletionTimeSeconds = Percentile(positiveTimes, 0.50),
            P90PositiveCompletionTimeSeconds = Percentile(positiveTimes, 0.90),
            DistanceQuestSamples = positiveDistances.Count,
            MedianPositiveDistanceConstraint = Percentile(positiveDistances, 0.50),
            P90PositiveDistanceConstraint = Percentile(positiveDistances, 0.90),
        };
    }

    private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0) return 0;
        if (sortedValues.Count == 1) return Math.Round(sortedValues[0], 2);
        var position = (sortedValues.Count - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper) return Math.Round(sortedValues[lower], 2);
        var fraction = position - lower;
        return Math.Round(sortedValues[lower] + ((sortedValues[upper] - sortedValues[lower]) * fraction), 2);
    }
}

public sealed record QuestConstraintAuditReport
{
    public required int SchemaVersion { get; init; }
    public required bool ConstraintsAffectRewardAllowance { get; init; }
    public required string BenchmarkSource { get; init; }
    public required int SourceAnalysisSchemaVersion { get; init; }
    public required string Note { get; init; }
    public required QuestConstraintBenchmark Vanilla { get; init; }
    public required QuestConstraintBenchmark VanillaRestartable { get; init; }
    public required List<QuestConstraintRow> Quests { get; init; }
}

public sealed record QuestConstraintBenchmark
{
    public required int QuestSamples { get; init; }
    public required double MedianStructuredConstraintCount { get; init; }
    public required double P90StructuredConstraintCount { get; init; }
    public required double MedianTimedConditionCount { get; init; }
    public required double P90TimedConditionCount { get; init; }
    public required double MedianOneSessionConditionCount { get; init; }
    public required double P90OneSessionConditionCount { get; init; }
    public required double MedianFoundInRaidConditionCount { get; init; }
    public required double P90FoundInRaidConditionCount { get; init; }
    public required int TimedQuestSamples { get; init; }
    public required double MedianPositiveCompletionTimeSeconds { get; init; }
    public required double P90PositiveCompletionTimeSeconds { get; init; }
    public required int DistanceQuestSamples { get; init; }
    public required double MedianPositiveDistanceConstraint { get; init; }
    public required double P90PositiveDistanceConstraint { get; init; }
}

public sealed record QuestConstraintRow
{
    public required string QuestId { get; init; }
    public required string QuestName { get; init; }
    public required string TraderId { get; init; }
    public required bool IsVanillaTraderQuest { get; init; }
    public required bool Restartable { get; init; }
    public required int ObjectiveConditionCount { get; init; }
    public required int TimedConditionCount { get; init; }
    public required int OneSessionConditionCount { get; init; }
    public required int FoundInRaidConditionCount { get; init; }
    public required int PlantConditionCount { get; init; }
    public required int DistanceConstraintCount { get; init; }
    public required int DaytimeConstraintCount { get; init; }
    public required int StructuredConstraintCount { get; init; }
}
