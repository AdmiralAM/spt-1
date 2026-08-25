using System.Text.Json;
using System.Text.Json.Nodes;
using Path = System.IO.Path;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;

namespace SPTEconomy;

/// <summary>
/// Rewrites report-only vanilla provenance from the priority-1 pristine snapshot.
/// No TemplateTable/TradersTable mutation occurs here.
/// </summary>
[Injectable]
public sealed class PristineReportCorrectionService(
    EconomyRuntimeConfigService runtimeConfigService,
    ModHelper modHelper,
    ISptLogger<PristineReportCorrectionService> logger
)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public async Task CorrectPrimaryMembershipAsync(VanillaBaselineSnapshot baseline, CancellationToken cancellationToken)
    {
        var config = await runtimeConfigService.GetAsync(cancellationToken);
        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(PristineReportCorrectionService).Assembly);
        var path = SafePath(modPath, config.ReportRelativePath);
        var root = await ReadObjectAsync(path, cancellationToken);
        var rows = root["QuestRewardAudits"]?.AsArray()
            ?? throw new InvalidOperationException("Economy Admiral primary audit report has no QuestRewardAudits for provenance correction.");

        foreach (var row in rows.OfType<JsonObject>())
        {
            var questId = ReadString(row, "QuestId");
            row["IsVanillaTraderQuest"] = baseline.QuestIds.Contains(questId);
        }

        root["VanillaBenchmarkSource"] = "PristineStartupQuestIds";
        root["PristineQuestCount"] = baseline.QuestCount;
        await WriteAsync(path, root, cancellationToken);
        logger.Info($"[Economy Admiral] primary audit membership corrected from pristine quest IDs: pristineQuests={baseline.QuestCount}");
    }

    public async Task CorrectPrimaryBenchmarkAsync(VanillaBaselineSnapshot baseline, CancellationToken cancellationToken)
    {
        var config = await runtimeConfigService.GetAsync(cancellationToken);
        var policy = ResolvePolicy(config);
        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(PristineReportCorrectionService).Assembly);
        var path = SafePath(modPath, config.ReportRelativePath);
        var root = await ReadObjectAsync(path, cancellationToken);

        var normal = baseline.Quests.Where(row => !row.Restartable).ToList();
        var restartable = baseline.Quests.Where(row => row.Restartable).ToList();
        var values = Positive(normal.Select(row => row.AllRewardKnownHandbookValue));
        var normalized = Positive(normal.Select(row => Normalize(row.AllRewardKnownHandbookValue, row.RequiredLevel, row.ObjectiveConditionCount, policy)));
        var restartableValues = Positive(restartable.Select(row => row.AllRewardKnownHandbookValue));
        var restartableNormalized = Positive(restartable.Select(row => Normalize(row.AllRewardKnownHandbookValue, row.RequiredLevel, row.ObjectiveConditionCount, policy)));

        root["VanillaQuestRewardBenchmark"] = new JsonObject
        {
            ["VanillaQuestSamples"] = values.Count,
            ["VanillaMedianHandbookValue"] = Percentile(values, 0.50),
            ["VanillaP90HandbookValue"] = Percentile(values, 0.90),
            ["VanillaMedianNormalizedHandbookValue"] = Percentile(normalized, 0.50),
            ["VanillaP90NormalizedHandbookValue"] = Percentile(normalized, 0.90),
            ["VanillaRestartableSamples"] = restartableValues.Count,
            ["VanillaRestartableMedianHandbookValue"] = Percentile(restartableValues, 0.50),
            ["VanillaRestartableMedianNormalizedHandbookValue"] = Percentile(restartableNormalized, 0.50),
        };
        root["VanillaBenchmarkSource"] = "PristineStartupSnapshot";
        await WriteAsync(path, root, cancellationToken);
        logger.Info($"[Economy Admiral] primary reward benchmark corrected from pristine snapshot: pricedSamples={values.Count}, median={Percentile(values, 0.50):0.##}");
    }

    public async Task CorrectRewardUtilityAsync(VanillaBaselineSnapshot baseline, CancellationToken cancellationToken)
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(PristineReportCorrectionService).Assembly);
        var path = SafePath(modPath, "reports/economy-admiral-reward-utility.json");
        var root = await ReadObjectAsync(path, cancellationToken);
        var rows = root["Quests"]?.AsArray()
            ?? throw new InvalidOperationException("Economy Admiral reward utility report has no Quests array.");

        var normal = BuildUtilityBenchmark(baseline.Quests.Where(row => !row.Restartable).ToList());
        var restartable = BuildUtilityBenchmark(baseline.Quests.Where(row => row.Restartable).ToList());
        root["Vanilla"] = normal.Json;
        root["VanillaRestartable"] = restartable.Json;
        root["BenchmarkSource"] = "PristineStartupSnapshot";

        foreach (var row in rows.OfType<JsonObject>())
        {
            var questId = ReadString(row, "QuestId");
            var isVanilla = baseline.QuestIds.Contains(questId);
            row["IsVanillaTraderQuest"] = isVanilla;
            var use = ReadBool(row, "Restartable") && restartable.QuestSamples > 0 ? restartable : normal;
            row["XpVsVanillaMedian"] = RatioNode(ReadDouble(row, "Experience"), use.MedianXp);
            row["StandingVsVanillaMedian"] = RatioNode(Math.Abs(ReadDouble(row, "TraderStanding")), use.MedianStanding);
            row["TraderUnlocksVsVanillaMedian"] = RatioNode(ReadDouble(row, "TraderUnlocks"), use.MedianTraderUnlocks);
            row["AssortmentUnlocksVsVanillaMedian"] = RatioNode(ReadDouble(row, "AssortmentUnlocks"), use.MedianAssortmentUnlocks);
            row["ProductionUnlocksVsVanillaMedian"] = RatioNode(ReadDouble(row, "ProductionSchemeUnlocks"), use.MedianProductionUnlocks);
        }

        await WriteAsync(path, root, cancellationToken);
        logger.Info($"[Economy Admiral] reward utility benchmark corrected from pristine snapshot: quests={normal.QuestSamples}");
    }

    public async Task CorrectProgressionGraphAsync(VanillaBaselineSnapshot baseline, CancellationToken cancellationToken)
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(PristineReportCorrectionService).Assembly);
        var path = SafePath(modPath, "reports/economy-admiral-progression-graph.json");
        var root = await ReadObjectAsync(path, cancellationToken);
        var rows = root["Quests"]?.AsArray()
            ?? throw new InvalidOperationException("Economy Admiral progression graph report has no Quests array.");

        foreach (var row in rows.OfType<JsonObject>())
            row["IsVanillaTraderQuest"] = baseline.QuestIds.Contains(ReadString(row, "QuestId"));

        root["VanillaDepthBenchmark"] = BuildDepthBenchmark(baseline.Quests.Where(row => !row.Restartable && !row.IsPrerequisiteCycleMember).Select(row => (double)row.MaximumPrerequisiteDepth));
        root["VanillaRestartableDepthBenchmark"] = BuildDepthBenchmark(baseline.Quests.Where(row => row.Restartable && !row.IsPrerequisiteCycleMember).Select(row => (double)row.MaximumPrerequisiteDepth));
        root["BenchmarkSource"] = "PristineStartupSnapshot";
        await WriteAsync(path, root, cancellationToken);
        logger.Info($"[Economy Admiral] progression benchmark corrected from pristine snapshot: pristineQuests={baseline.QuestCount}");
    }

    public async Task CorrectConstraintsAsync(VanillaBaselineSnapshot baseline, CancellationToken cancellationToken)
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(PristineReportCorrectionService).Assembly);
        var path = SafePath(modPath, "reports/economy-admiral-quest-constraints.json");
        var root = await ReadObjectAsync(path, cancellationToken);
        var rows = root["Quests"]?.AsArray()
            ?? throw new InvalidOperationException("Economy Admiral quest constraint report has no Quests array.");

        foreach (var row in rows.OfType<JsonObject>())
            row["IsVanillaTraderQuest"] = baseline.QuestIds.Contains(ReadString(row, "QuestId"));

        root["Vanilla"] = BuildConstraintBenchmark(baseline.Quests.Where(row => !row.Restartable).ToList());
        root["VanillaRestartable"] = BuildConstraintBenchmark(baseline.Quests.Where(row => row.Restartable).ToList());
        root["BenchmarkSource"] = "PristineStartupSnapshot";
        await WriteAsync(path, root, cancellationToken);
        logger.Info($"[Economy Admiral] constraint benchmark corrected from pristine snapshot: pristineQuests={baseline.QuestCount}");
    }

    private static UtilityBenchmarkValues BuildUtilityBenchmark(IReadOnlyCollection<VanillaQuestBaselineRow> rows)
    {
        var xp = Positive(rows.Select(row => row.Experience));
        var standing = Positive(rows.Select(row => Math.Abs(row.TraderStanding)));
        var traderUnlocks = Positive(rows.Select(row => (double)row.TraderUnlocks));
        var assortmentUnlocks = Positive(rows.Select(row => (double)row.AssortmentUnlocks));
        var productionUnlocks = Positive(rows.Select(row => (double)row.ProductionSchemeUnlocks));
        var json = new JsonObject
        {
            ["QuestSamples"] = rows.Count,
            ["XpSamples"] = xp.Count,
            ["MedianXp"] = Percentile(xp, 0.50),
            ["P90Xp"] = Percentile(xp, 0.90),
            ["StandingSamples"] = standing.Count,
            ["MedianAbsoluteStanding"] = Percentile(standing, 0.50),
            ["P90AbsoluteStanding"] = Percentile(standing, 0.90),
            ["TraderUnlockQuestSamples"] = traderUnlocks.Count,
            ["MedianPositiveTraderUnlocks"] = Percentile(traderUnlocks, 0.50),
            ["P90PositiveTraderUnlocks"] = Percentile(traderUnlocks, 0.90),
            ["AssortmentUnlockQuestSamples"] = assortmentUnlocks.Count,
            ["MedianPositiveAssortmentUnlocks"] = Percentile(assortmentUnlocks, 0.50),
            ["P90PositiveAssortmentUnlocks"] = Percentile(assortmentUnlocks, 0.90),
            ["ProductionSchemeUnlockQuestSamples"] = productionUnlocks.Count,
            ["MedianPositiveProductionSchemeUnlocks"] = Percentile(productionUnlocks, 0.50),
            ["P90PositiveProductionSchemeUnlocks"] = Percentile(productionUnlocks, 0.90),
        };
        return new UtilityBenchmarkValues(rows.Count, Percentile(xp, 0.50), Percentile(standing, 0.50), Percentile(traderUnlocks, 0.50), Percentile(assortmentUnlocks, 0.50), Percentile(productionUnlocks, 0.50), json);
    }

    private static JsonObject BuildDepthBenchmark(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(value => value).ToList();
        return new JsonObject
        {
            ["QuestSamples"] = sorted.Count,
            ["MedianDepth"] = Percentile(sorted, 0.50),
            ["P90Depth"] = Percentile(sorted, 0.90),
            ["MaximumDepth"] = sorted.Count == 0 ? 0 : (int)sorted[^1],
        };
    }

    private static JsonObject BuildConstraintBenchmark(IReadOnlyCollection<VanillaQuestBaselineRow> rows)
    {
        var total = rows.Select(row => (double)row.StructuredConstraintCount).OrderBy(value => value).ToList();
        var timed = rows.Select(row => (double)row.TimedConditionCount).OrderBy(value => value).ToList();
        var oneSession = rows.Select(row => (double)row.OneSessionConditionCount).OrderBy(value => value).ToList();
        var fir = rows.Select(row => (double)row.FoundInRaidConditionCount).OrderBy(value => value).ToList();
        var times = Positive(rows.Select(row => row.StrictestCompletionTimeSeconds));
        var distances = Positive(rows.Select(row => row.LongestDistanceConstraint));
        return new JsonObject
        {
            ["QuestSamples"] = rows.Count,
            ["MedianStructuredConstraintCount"] = Percentile(total, 0.50),
            ["P90StructuredConstraintCount"] = Percentile(total, 0.90),
            ["MedianTimedConditionCount"] = Percentile(timed, 0.50),
            ["P90TimedConditionCount"] = Percentile(timed, 0.90),
            ["MedianOneSessionConditionCount"] = Percentile(oneSession, 0.50),
            ["P90OneSessionConditionCount"] = Percentile(oneSession, 0.90),
            ["MedianFoundInRaidConditionCount"] = Percentile(fir, 0.50),
            ["P90FoundInRaidConditionCount"] = Percentile(fir, 0.90),
            ["TimedQuestSamples"] = times.Count,
            ["MedianPositiveCompletionTimeSeconds"] = Percentile(times, 0.50),
            ["P90PositiveCompletionTimeSeconds"] = Percentile(times, 0.90),
            ["DistanceQuestSamples"] = distances.Count,
            ["MedianPositiveDistanceConstraint"] = Percentile(distances, 0.50),
            ["P90PositiveDistanceConstraint"] = Percentile(distances, 0.90),
        };
    }

    private static double Normalize(double value, int requiredLevel, int objectiveCount, AuditPolicy policy)
    {
        var levelGate = Math.Min(policy.MaxLevelGateContribution, Math.Max(0, requiredLevel - 1) * policy.LevelGateWeight);
        var objectives = Math.Min(policy.MaxObjectiveContribution, Math.Max(0, objectiveCount) * policy.ObjectiveConditionWeight);
        var score = 1d + levelGate + objectives;
        return score > 0 ? value / score : value;
    }

    private static AuditPolicy ResolvePolicy(EconomyConfig config) => config.Preset switch
    {
        EconomyPreset.Easy => new AuditPolicy
        {
            QuestRewardVsVanillaMedianWarnMultiple = 5.0,
            RestartableRewardVsVanillaMedianWarnMultiple = 2.5,
            NormalizedRewardVsVanillaMedianWarnMultiple = 4.0,
            RestartableNormalizedRewardVsVanillaMedianWarnMultiple = 2.0,
            DuplicateTraderSourcesWarnCount = 8,
            HighItemValueLowStructureWarnMultiple = 4.0,
            HighXpLowDepthWarnMultiple = 4.0,
            HighStandingLowDepthWarnMultiple = 4.0,
            RestartableHighItemValueWarnMultiple = 3.0,
            RestartableHighXpWarnMultiple = 3.0,
        },
        EconomyPreset.Hard => new AuditPolicy
        {
            QuestRewardVsVanillaMedianWarnMultiple = 2.0,
            RestartableRewardVsVanillaMedianWarnMultiple = 1.25,
            NormalizedRewardVsVanillaMedianWarnMultiple = 1.75,
            RestartableNormalizedRewardVsVanillaMedianWarnMultiple = 1.10,
            DuplicateTraderSourcesWarnCount = 4,
            HighItemValueLowStructureWarnMultiple = 2.0,
            HighXpLowDepthWarnMultiple = 2.0,
            HighStandingLowDepthWarnMultiple = 2.0,
            RestartableHighItemValueWarnMultiple = 1.5,
            RestartableHighXpWarnMultiple = 1.5,
            LowDepthMaxRelativeMultiple = 1.25,
            LowStructureMaxRelativeMultiple = 1.25,
        },
        EconomyPreset.Custom => config.CustomAuditPolicy,
        _ => new AuditPolicy(),
    };

    private static List<double> Positive(IEnumerable<double> values) => values.Where(value => value > 0).OrderBy(value => value).ToList();
    private static JsonNode? RatioNode(double value, double baseline) => value > 0 && baseline > 0 ? JsonValue.Create(Math.Round(value / baseline, 4)) : null;
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

    private static string ReadString(JsonObject row, string property) => row[property]?.GetValue<string>() ?? string.Empty;
    private static double ReadDouble(JsonObject row, string property) => row[property]?.GetValue<double>() ?? 0d;
    private static bool ReadBool(JsonObject row, string property) => row[property]?.GetValue<bool>() ?? false;

    private static async Task<JsonObject> ReadObjectAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) throw new InvalidOperationException($"Economy Admiral report missing before pristine correction: {Path.GetFileName(path)}");
        return JsonNode.Parse(await File.ReadAllTextAsync(path, cancellationToken))?.AsObject()
            ?? throw new InvalidOperationException($"Economy Admiral report could not be parsed before pristine correction: {Path.GetFileName(path)}");
    }

    private static Task WriteAsync(string path, JsonObject root, CancellationToken cancellationToken) => File.WriteAllTextAsync(path, root.ToJsonString(JsonOptions), cancellationToken);

    private static string SafePath(string modPath, string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(modPath, relativePath));
        var root = Path.GetFullPath(modPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Economy Admiral pristine correction path must stay inside the mod directory.");
        return fullPath;
    }

    private sealed record UtilityBenchmarkValues(int QuestSamples, double MedianXp, double MedianStanding, double MedianTraderUnlocks, double MedianAssortmentUnlocks, double MedianProductionUnlocks, JsonObject Json);
}
