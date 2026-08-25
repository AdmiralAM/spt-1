using System.Text.Json;
using Path = System.IO.Path;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;

namespace SPTEconomy;

[Injectable]
public sealed class BaselineProvenanceCorrectionService(
    ModHelper modHelper,
    ISptLogger<BaselineProvenanceCorrectionService> logger
)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<QuestAnalysisReport> ApplyToUnifiedAnalysisAsync(
        QuestAnalysisReport analysis,
        VanillaBaselineSnapshot baselineSnapshot,
        CancellationToken cancellationToken
    )
    {
        var vanilla = BuildBaseline(baselineSnapshot.Quests.Where(row => !row.Restartable).ToList());
        var vanillaRestartable = BuildBaseline(baselineSnapshot.Quests.Where(row => row.Restartable).ToList());

        var rows = analysis.Quests
            .OrderBy(row => row.QuestId, StringComparer.Ordinal)
            .Select(row =>
            {
                var isVanilla = baselineSnapshot.QuestIds.Contains(row.QuestId);
                var baseline = row.Restartable && vanillaRestartable.QuestSamples > 0 ? vanillaRestartable : vanilla;
                var reclassified = row with
                {
                    IsVanillaTraderQuest = isVanilla,
                    HandbookValueVsVanillaMedian = Ratio(row.SuccessKnownHandbookValue, baseline.MedianSuccessHandbookValue),
                    XpVsVanillaMedian = Ratio(row.Experience, baseline.MedianXp),
                    StandingVsVanillaMedian = Ratio(Math.Abs(row.TraderStanding), baseline.MedianAbsoluteStanding),
                    PrerequisiteDepthVsVanillaMedian = Ratio(row.MaximumPrerequisiteDepth, baseline.MedianPrerequisiteDepth),
                    StructuredConstraintsVsVanillaMedian = Ratio(row.StructuredConstraintCount, baseline.MedianStructuredConstraintCount),
                    ObservationalFlags = [],
                };
                return ApplyFlags(reclassified, analysis.Policy);
            })
            .ToList();

        var flagCounts = rows
            .SelectMany(row => row.ObservationalFlags)
            .GroupBy(flag => flag, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var corrected = analysis with
        {
            Vanilla = vanilla,
            VanillaRestartable = vanillaRestartable,
            FlagCounts = flagCounts,
            Note = analysis.Note + $" Vanilla-relative policy metrics use pristine startup baseline captured at priority {baselineSnapshot.CapturePriority} before normal mod callbacks; vanilla membership is quest-ID provenance, not trader-ID inference.",
            Quests = rows,
        };

        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(BaselineProvenanceCorrectionService).Assembly);
        var reportPath = SafePath(modPath, "reports/economy-admiral-quest-analysis.json");
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(corrected, JsonOptions), cancellationToken);

        if (baselineSnapshot.QuestCount <= 0 || corrected.Vanilla.QuestSamples <= 0)
        {
            throw new InvalidOperationException("Economy Admiral pristine vanilla provenance gate failed: no baseline quests were captured.");
        }

        logger.Info(
            $"[Economy Admiral] pristine vanilla provenance applied: baselineQuests={baselineSnapshot.QuestCount}, " +
            $"finalQuests={rows.Count}, modAddedQuests={rows.Count(row => !row.IsVanillaTraderQuest)}, flags={flagCounts.Values.Sum()}; report={reportPath}"
        );
        return corrected;
    }

    private static QuestAnalysisBaseline BuildBaseline(IReadOnlyCollection<VanillaQuestBaselineRow> rows) => new()
    {
        QuestSamples = rows.Count,
        MedianSuccessHandbookValue = MedianPositive(rows.Select(row => row.SuccessKnownHandbookValue)),
        MedianXp = MedianPositive(rows.Select(row => row.Experience)),
        MedianAbsoluteStanding = MedianPositive(rows.Select(row => Math.Abs(row.TraderStanding))),
        MedianPrerequisiteDepth = MedianPositive(rows.Where(row => !row.IsPrerequisiteCycleMember).Select(row => (double)row.MaximumPrerequisiteDepth)),
        MedianStructuredConstraintCount = MedianPositive(rows.Select(row => (double)row.StructuredConstraintCount)),
    };

    private static QuestAnalysisRow ApplyFlags(QuestAnalysisRow row, AuditPolicy policy)
    {
        var flags = new List<string>();
        var lowDepth = row.PrerequisiteDepthVsVanillaMedian is null || row.PrerequisiteDepthVsVanillaMedian <= policy.LowDepthMaxRelativeMultiple;
        var lowStructure = row.StructuredConstraintsVsVanillaMedian is null || row.StructuredConstraintsVsVanillaMedian <= policy.LowStructureMaxRelativeMultiple;

        if (row.HandbookValueVsVanillaMedian >= policy.HighItemValueLowStructureWarnMultiple && lowDepth && lowStructure)
            flags.Add("HIGH_ITEM_VALUE_LOW_STRUCTURE");
        if (row.XpVsVanillaMedian >= policy.HighXpLowDepthWarnMultiple && lowDepth)
            flags.Add("HIGH_XP_LOW_DEPTH");
        if (row.StandingVsVanillaMedian >= policy.HighStandingLowDepthWarnMultiple && lowDepth)
            flags.Add("HIGH_STANDING_LOW_DEPTH");
        if (row.Restartable && row.HandbookValueVsVanillaMedian >= policy.RestartableHighItemValueWarnMultiple)
            flags.Add("RESTARTABLE_HIGH_ITEM_VALUE");
        if (row.Restartable && row.XpVsVanillaMedian >= policy.RestartableHighXpWarnMultiple)
            flags.Add("RESTARTABLE_HIGH_XP");
        if (row.IsPrerequisiteCycleMember)
            flags.Add("PREREQUISITE_CYCLE");

        return row with { ObservationalFlags = flags };
    }

    private static double MedianPositive(IEnumerable<double> values)
    {
        var sorted = values.Where(value => value > 0).OrderBy(value => value).ToList();
        if (sorted.Count == 0) return 0;
        if (sorted.Count == 1) return Math.Round(sorted[0], 4);
        var position = (sorted.Count - 1) * 0.5;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper) return Math.Round(sorted[lower], 4);
        return Math.Round(sorted[lower] + ((sorted[upper] - sorted[lower]) * (position - lower)), 4);
    }

    private static double? Ratio(double value, double baseline) => value > 0 && baseline > 0
        ? Math.Round(value / baseline, 4)
        : null;

    private static string SafePath(string modPath, string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(modPath, relativePath));
        var root = Path.GetFullPath(modPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Economy Admiral provenance report path must stay inside the mod directory.");
        }
        return fullPath;
    }
}
