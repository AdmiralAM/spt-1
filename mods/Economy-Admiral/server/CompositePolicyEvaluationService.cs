using System.Text.Json;
using Path = System.IO.Path;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;

namespace SPTEconomy;

[Injectable]
public sealed class CompositePolicyEvaluationService(
    QuestAnalysisService questAnalysisService,
    ModHelper modHelper,
    ISptLogger<CompositePolicyEvaluationService> logger
)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var analysis = questAnalysisService.GetSnapshot();
        var rows = analysis.Quests
            .OrderBy(row => row.QuestId, StringComparer.Ordinal)
            .Select(BuildRow)
            .ToList();

        var vanilla = rows.Where(row => row.IsVanillaTraderQuest && !row.Restartable).ToList();
        var vanillaRestartable = rows.Where(row => row.IsVanillaTraderQuest && row.Restartable).ToList();

        var report = new CompositePolicyEvaluationReport
        {
            SchemaVersion = 1,
            SelectedCandidate = null,
            AffectsRewardAllowance = false,
            AffectsEnforcement = false,
            Note = "Candidate dimensionless metrics only. No candidate is selected as policy and no score changes reward allowance or enforcement behavior.",
            Candidates =
            [
                new CompositeCandidateDefinition
                {
                    Id = "RewardPeak",
                    Formula = "max(HandbookValueVsVanillaMedian, XpVsVanillaMedian, StandingVsVanillaMedian)",
                    Intent = "Expose the strongest observed reward-dimension deviation without averaging it away.",
                },
                new CompositeCandidateDefinition
                {
                    Id = "RewardMean",
                    Formula = "mean(available positive reward-dimension vanilla-relative ratios)",
                    Intent = "Measure broad reward inflation across available handbook/XP/standing dimensions.",
                },
                new CompositeCandidateDefinition
                {
                    Id = "StructureAdjustedPeak",
                    Formula = "RewardPeak / max(1, mean(available positive prerequisite-depth and structured-constraint ratios))",
                    Intent = "Explore reward intensity relative to measured structural support; observational only.",
                },
            ],
            Vanilla = BuildBenchmark(vanilla),
            VanillaRestartable = BuildBenchmark(vanillaRestartable),
            Quests = rows,
        };

        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(CompositePolicyEvaluationService).Assembly);
        var reportPath = SafePath(modPath, "reports/economy-admiral-composite-candidates.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        logger.Info($"[Economy Admiral] composite candidate evaluation complete: {rows.Count} quests; no candidate selected; report={reportPath}");
    }

    private static CompositePolicyQuestRow BuildRow(QuestAnalysisRow row)
    {
        var rewardRatios = Positive(row.HandbookValueVsVanillaMedian, row.XpVsVanillaMedian, row.StandingVsVanillaMedian);
        var structureRatios = Positive(row.PrerequisiteDepthVsVanillaMedian, row.StructuredConstraintsVsVanillaMedian);

        double? rewardPeak = rewardRatios.Count == 0 ? null : Math.Round(rewardRatios.Max(), 4);
        double? rewardMean = rewardRatios.Count == 0 ? null : Math.Round(rewardRatios.Average(), 4);
        var structureSupport = structureRatios.Count == 0 ? 1d : Math.Max(1d, structureRatios.Average());
        double? structureAdjustedPeak = rewardPeak.HasValue ? Math.Round(rewardPeak.Value / structureSupport, 4) : null;

        return new CompositePolicyQuestRow
        {
            QuestId = row.QuestId,
            QuestName = row.QuestName,
            TraderId = row.TraderId,
            IsVanillaTraderQuest = row.IsVanillaTraderQuest,
            Restartable = row.Restartable,
            RewardPeak = rewardPeak,
            RewardMean = rewardMean,
            StructureSupport = Math.Round(structureSupport, 4),
            StructureAdjustedPeak = structureAdjustedPeak,
            SourceObservationalFlags = row.ObservationalFlags,
        };
    }

    private static CompositeCandidateBenchmark BuildBenchmark(IReadOnlyCollection<CompositePolicyQuestRow> rows)
    {
        return new CompositeCandidateBenchmark
        {
            QuestSamples = rows.Count,
            RewardPeakSamples = rows.Count(row => row.RewardPeak.HasValue),
            MedianRewardPeak = Median(rows.Select(row => row.RewardPeak)),
            P90RewardPeak = Percentile(Values(rows.Select(row => row.RewardPeak)), 0.90),
            RewardMeanSamples = rows.Count(row => row.RewardMean.HasValue),
            MedianRewardMean = Median(rows.Select(row => row.RewardMean)),
            P90RewardMean = Percentile(Values(rows.Select(row => row.RewardMean)), 0.90),
            StructureAdjustedSamples = rows.Count(row => row.StructureAdjustedPeak.HasValue),
            MedianStructureAdjustedPeak = Median(rows.Select(row => row.StructureAdjustedPeak)),
            P90StructureAdjustedPeak = Percentile(Values(rows.Select(row => row.StructureAdjustedPeak)), 0.90),
        };
    }

    private static List<double> Positive(params double?[] values) => values
        .Where(value => value is > 0)
        .Select(value => value!.Value)
        .ToList();

    private static List<double> Values(IEnumerable<double?> values) => values
        .Where(value => value.HasValue)
        .Select(value => value!.Value)
        .OrderBy(value => value)
        .ToList();

    private static double Median(IEnumerable<double?> values) => Percentile(Values(values), 0.50);

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0) return 0;
        if (values.Count == 1) return Math.Round(values[0], 4);
        var position = (values.Count - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper) return Math.Round(values[lower], 4);
        return Math.Round(values[lower] + ((values[upper] - values[lower]) * (position - lower)), 4);
    }

    private static string SafePath(string modPath, string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(modPath, relativePath));
        var root = Path.GetFullPath(modPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Economy Admiral report path must stay inside the mod directory.");
        }
        return path;
    }
}

public sealed record CompositePolicyEvaluationReport
{
    public required int SchemaVersion { get; init; }
    public required string? SelectedCandidate { get; init; }
    public required bool AffectsRewardAllowance { get; init; }
    public required bool AffectsEnforcement { get; init; }
    public required string Note { get; init; }
    public required List<CompositeCandidateDefinition> Candidates { get; init; }
    public required CompositeCandidateBenchmark Vanilla { get; init; }
    public required CompositeCandidateBenchmark VanillaRestartable { get; init; }
    public required List<CompositePolicyQuestRow> Quests { get; init; }
}

public sealed record CompositeCandidateDefinition
{
    public required string Id { get; init; }
    public required string Formula { get; init; }
    public required string Intent { get; init; }
}

public sealed record CompositeCandidateBenchmark
{
    public required int QuestSamples { get; init; }
    public required int RewardPeakSamples { get; init; }
    public required double MedianRewardPeak { get; init; }
    public required double P90RewardPeak { get; init; }
    public required int RewardMeanSamples { get; init; }
    public required double MedianRewardMean { get; init; }
    public required double P90RewardMean { get; init; }
    public required int StructureAdjustedSamples { get; init; }
    public required double MedianStructureAdjustedPeak { get; init; }
    public required double P90StructureAdjustedPeak { get; init; }
}

public sealed record CompositePolicyQuestRow
{
    public required string QuestId { get; init; }
    public required string QuestName { get; init; }
    public required string TraderId { get; init; }
    public required bool IsVanillaTraderQuest { get; init; }
    public required bool Restartable { get; init; }
    public required double? RewardPeak { get; init; }
    public required double? RewardMean { get; init; }
    public required double StructureSupport { get; init; }
    public required double? StructureAdjustedPeak { get; init; }
    public required List<string> SourceObservationalFlags { get; init; }
}
