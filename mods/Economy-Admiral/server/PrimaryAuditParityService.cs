using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTEconomy;

public sealed record PrimaryAuditParityMismatch
{
    public required string Scope { get; init; }
    public required string SubjectId { get; init; }
    public required string Field { get; init; }
    public required string Expected { get; init; }
    public required string Actual { get; init; }
}

public sealed record PrimaryAuditParityReport
{
    public int SchemaVersion { get; init; } = 1;
    public string ExpectedSource { get; init; } = "TypedFinalDbPlusPristineStartupSnapshot";
    public required int FinalQuestCount { get; init; }
    public required int PristineQuestCount { get; init; }
    public required int ComparedQuestRows { get; init; }
    public required int ExpectedQuestRewardSourceEdges { get; init; }
    public required int ReportedQuestRewardSourceEdges { get; init; }
    public required bool BenchmarkMatches { get; init; }
    public required bool AcquisitionMatches { get; init; }
    public required bool QuestRowsMatch { get; init; }
    public required bool AllMatched { get; init; }
    public required IReadOnlyList<PrimaryAuditParityMismatch> Mismatches { get; init; }
}

/// <summary>
/// Shadow verifier for #139. Recomputes primary reward/provenance facts directly from typed final
/// SPT DB records plus the pristine startup snapshot, then compares them with the corrected primary
/// report. This is observational only and does not replace the correction pipeline until runtime
/// parity has been demonstrated.
/// </summary>
[Injectable]
public sealed class PrimaryAuditParityService(
    TemplateTable templates,
    VanillaBaselineService vanillaBaselineService,
    EconomyRuntimeConfigService runtimeConfigService,
    ModHelper modHelper,
    ISptLogger<PrimaryAuditParityService> logger
)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<PrimaryAuditParityReport> RunAsync(CancellationToken cancellationToken)
    {
        var config = await runtimeConfigService.GetAsync(cancellationToken);
        var baseline = vanillaBaselineService.GetSnapshot();
        var policy = ResolvePolicy(config);
        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var primaryPath = SafePath(modPath, config.ReportRelativePath);
        if (!File.Exists(primaryPath))
            throw new InvalidOperationException("Economy Admiral primary parity: corrected primary audit report is missing.");

        var root = JsonNode.Parse(await File.ReadAllTextAsync(primaryPath, cancellationToken))?.AsObject()
            ?? throw new InvalidOperationException("Economy Admiral primary parity: primary audit report could not be parsed.");
        var reportRows = (root["QuestRewardAudits"]?.AsArray()
            ?? throw new InvalidOperationException("Economy Admiral primary parity: QuestRewardAudits is missing."))
            .OfType<JsonObject>()
            .ToDictionary(row => ReadString(row, "QuestId"), row => row, StringComparer.Ordinal);

        var handbookPrices = templates.Handbook.Items
            .Where(item => item.Price is > 0)
            .GroupBy(item => item.Id.ToString(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Price!.Value, StringComparer.Ordinal);
        var finalQuestIds = templates.Quests.Keys.Select(key => key.ToString()).ToHashSet(StringComparer.Ordinal);
        var mismatches = new List<PrimaryAuditParityMismatch>();
        var expectedRewardSources = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var compared = 0;

        foreach (var pair in templates.Quests.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var questId = pair.Key.ToString();
            if (!reportRows.TryGetValue(questId, out var reported))
            {
                AddMismatch(mismatches, "Quest", questId, "RowPresent", "true", "false");
                continue;
            }

            compared++;
            var rewards = EnumerateRewards(pair.Value).ToList();
            var items = rewards.Where(reward => reward.Items is not null).SelectMany(reward => reward.Items!).ToList();
            var distinctTemplates = new HashSet<string>(StringComparer.Ordinal);
            var knownValue = 0d;
            var unknown = 0;

            foreach (var item in items)
            {
                var templateId = item.Template.ToString();
                if (string.IsNullOrWhiteSpace(templateId)) continue;
                distinctTemplates.Add(templateId);
                if (!expectedRewardSources.TryGetValue(templateId, out var quests))
                {
                    quests = new HashSet<string>(StringComparer.Ordinal);
                    expectedRewardSources.Add(templateId, quests);
                }
                quests.Add(questId);

                var count = Math.Max(1d, item.Upd?.StackObjectsCount ?? 1d);
                if (handbookPrices.TryGetValue(templateId, out var price)) knownValue += price * count;
                else unknown++;
            }

            var requiredLevel = ExtractRequiredLevel(pair.Value.Conditions.AvailableForStart);
            var objectiveCount = (pair.Value.Conditions.AvailableForFinish?.Count ?? 0) + (pair.Value.Conditions.Success?.Count ?? 0);
            var progressionScore = CalculateProgressionScore(requiredLevel, objectiveCount, policy);
            var normalized = progressionScore > 0 ? knownValue / progressionScore : knownValue;

            CompareQuest(mismatches, questId, "IsVanillaTraderQuest", baseline.QuestIds.Contains(questId), ReadBool(reported, "IsVanillaTraderQuest"));
            CompareQuest(mismatches, questId, "Restartable", pair.Value.Restartable, ReadBool(reported, "Restartable"));
            CompareQuest(mismatches, questId, "RequiredLevel", requiredLevel, ReadInt(reported, "RequiredLevel"));
            CompareQuest(mismatches, questId, "ObjectiveConditionCount", objectiveCount, ReadInt(reported, "ObjectiveConditionCount"));
            CompareQuest(mismatches, questId, "RewardItemRecords", items.Count, ReadInt(reported, "RewardItemRecords"));
            CompareQuest(mismatches, questId, "DistinctRewardTemplates", distinctTemplates.Count, ReadInt(reported, "DistinctRewardTemplates"));
            CompareQuest(mismatches, questId, "UnknownPriceItemRecords", unknown, ReadInt(reported, "UnknownPriceItemRecords"));
            CompareQuestDouble(mismatches, questId, "ProgressionScore", Math.Round(progressionScore, 4), ReadDouble(reported, "ProgressionScore"));
            CompareQuestDouble(mismatches, questId, "KnownHandbookValue", Math.Round(knownValue, 2), ReadDouble(reported, "KnownHandbookValue"));
            CompareQuestDouble(mismatches, questId, "NormalizedHandbookValue", Math.Round(normalized, 2), ReadDouble(reported, "NormalizedHandbookValue"));
        }

        foreach (var reportQuestId in reportRows.Keys.Where(id => !finalQuestIds.Contains(id)))
            AddMismatch(mismatches, "Quest", reportQuestId, "UnexpectedReportRow", "false", "true");

        var expectedQuestEdges = expectedRewardSources.Values.Sum(quests => quests.Count);
        var acquisition = root["Acquisition"]?.AsObject()
            ?? throw new InvalidOperationException("Economy Admiral primary parity: Acquisition is missing.");
        var reportedQuestEdges = ReadInt(acquisition, "QuestRewardSourceEdges");
        if (expectedQuestEdges != reportedQuestEdges)
            AddMismatch(mismatches, "Acquisition", "global", "QuestRewardSourceEdges", expectedQuestEdges.ToString(), reportedQuestEdges.ToString());

        var expectedBenchmark = BuildBenchmark(baseline, policy);
        var reportedBenchmark = root["VanillaQuestRewardBenchmark"]?.AsObject()
            ?? throw new InvalidOperationException("Economy Admiral primary parity: VanillaQuestRewardBenchmark is missing.");
        CompareBenchmark(mismatches, expectedBenchmark, reportedBenchmark);

        if (!string.Equals(root["VanillaBenchmarkSource"]?.GetValue<string>(), "PristineStartupSnapshot", StringComparison.Ordinal))
            AddMismatch(mismatches, "Benchmark", "global", "VanillaBenchmarkSource", "PristineStartupSnapshot", root["VanillaBenchmarkSource"]?.ToJsonString() ?? "null");

        var report = new PrimaryAuditParityReport
        {
            FinalQuestCount = templates.Quests.Count,
            PristineQuestCount = baseline.QuestCount,
            ComparedQuestRows = compared,
            ExpectedQuestRewardSourceEdges = expectedQuestEdges,
            ReportedQuestRewardSourceEdges = reportedQuestEdges,
            BenchmarkMatches = mismatches.All(mismatch => mismatch.Scope != "Benchmark"),
            AcquisitionMatches = mismatches.All(mismatch => mismatch.Scope != "Acquisition"),
            QuestRowsMatch = mismatches.All(mismatch => mismatch.Scope != "Quest"),
            AllMatched = mismatches.Count == 0,
            Mismatches = mismatches
                .OrderBy(mismatch => mismatch.Scope, StringComparer.Ordinal)
                .ThenBy(mismatch => mismatch.SubjectId, StringComparer.Ordinal)
                .ThenBy(mismatch => mismatch.Field, StringComparer.Ordinal)
                .Take(250)
                .ToArray(),
        };

        var reportPath = SafePath(modPath, "reports/economy-admiral-primary-parity.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions), cancellationToken);

        if (report.AllMatched)
            logger.Info($"[Economy Admiral] source-correct primary audit parity PASS: quests={compared}, questRewardEdges={expectedQuestEdges}; report={reportPath}");
        else
            logger.Warning($"[Economy Admiral] source-correct primary audit parity MISMATCH: total={mismatches.Count}, retained={report.Mismatches.Count}; report={reportPath}");

        return report;
    }

    private static IEnumerable<Reward> EnumerateRewards(Quest quest)
    {
        if (quest.Rewards is null) yield break;
        foreach (var pair in quest.Rewards)
            foreach (var reward in pair.Value)
                yield return reward;
    }

    private static int ExtractRequiredLevel(IEnumerable<QuestCondition>? conditions)
    {
        if (conditions is null) return 1;
        var levels = conditions
            .Where(condition => string.Equals(condition.ConditionType, "Level", StringComparison.OrdinalIgnoreCase) && condition.Value.HasValue)
            .Select(condition => Math.Max(1, (int)Math.Ceiling(condition.Value!.Value)))
            .ToList();
        return levels.Count == 0 ? 1 : levels.Max();
    }

    private static double CalculateProgressionScore(int requiredLevel, int objectiveCount, AuditPolicy policy)
    {
        var levelContribution = Math.Min(Math.Max(0, requiredLevel - 1) * Math.Max(0, policy.LevelGateWeight), Math.Max(0, policy.MaxLevelGateContribution));
        var objectiveContribution = Math.Min(Math.Max(1, objectiveCount) * Math.Max(0, policy.ObjectiveConditionWeight), Math.Max(0, policy.MaxObjectiveContribution));
        return 1d + levelContribution + objectiveContribution;
    }

    private static QuestRewardBenchmark BuildBenchmark(VanillaBaselineSnapshot baseline, AuditPolicy policy)
    {
        var normal = baseline.Quests.Where(row => !row.Restartable).ToList();
        var restartable = baseline.Quests.Where(row => row.Restartable).ToList();
        var values = Positive(normal.Select(row => row.AllRewardKnownHandbookValue));
        var normalized = Positive(normal.Select(row => Normalize(row.AllRewardKnownHandbookValue, row.RequiredLevel, row.ObjectiveConditionCount, policy)));
        var restartableValues = Positive(restartable.Select(row => row.AllRewardKnownHandbookValue));
        var restartableNormalized = Positive(restartable.Select(row => Normalize(row.AllRewardKnownHandbookValue, row.RequiredLevel, row.ObjectiveConditionCount, policy)));
        return new QuestRewardBenchmark
        {
            VanillaQuestSamples = values.Count,
            VanillaMedianHandbookValue = Percentile(values, 0.50),
            VanillaP90HandbookValue = Percentile(values, 0.90),
            VanillaMedianNormalizedHandbookValue = Percentile(normalized, 0.50),
            VanillaP90NormalizedHandbookValue = Percentile(normalized, 0.90),
            VanillaRestartableSamples = restartableValues.Count,
            VanillaRestartableMedianHandbookValue = Percentile(restartableValues, 0.50),
            VanillaRestartableMedianNormalizedHandbookValue = Percentile(restartableNormalized, 0.50),
        };
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
        },
        EconomyPreset.Hard => new AuditPolicy
        {
            QuestRewardVsVanillaMedianWarnMultiple = 2.0,
            RestartableRewardVsVanillaMedianWarnMultiple = 1.25,
            NormalizedRewardVsVanillaMedianWarnMultiple = 1.75,
            RestartableNormalizedRewardVsVanillaMedianWarnMultiple = 1.10,
            DuplicateTraderSourcesWarnCount = 4,
        },
        EconomyPreset.Custom => config.CustomAuditPolicy,
        _ => new AuditPolicy(),
    };

    private static double Normalize(double value, int requiredLevel, int objectiveCount, AuditPolicy policy)
    {
        var levelGate = Math.Min(policy.MaxLevelGateContribution, Math.Max(0, requiredLevel - 1) * policy.LevelGateWeight);
        var objectives = Math.Min(policy.MaxObjectiveContribution, Math.Max(0, objectiveCount) * policy.ObjectiveConditionWeight);
        var score = 1d + levelGate + objectives;
        return score > 0 ? value / score : value;
    }

    private static List<double> Positive(IEnumerable<double> values) => values.Where(value => value > 0).OrderBy(value => value).ToList();

    private static double Percentile(IReadOnlyList<double> sorted, double percentile)
    {
        if (sorted.Count == 0) return 0;
        if (sorted.Count == 1) return Math.Round(sorted[0], 2);
        var position = (sorted.Count - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper) return Math.Round(sorted[lower], 2);
        var fraction = position - lower;
        return Math.Round(sorted[lower] + ((sorted[upper] - sorted[lower]) * fraction), 2);
    }

    private static void CompareBenchmark(List<PrimaryAuditParityMismatch> mismatches, QuestRewardBenchmark expected, JsonObject actual)
    {
        CompareBenchmarkInt(mismatches, "VanillaQuestSamples", expected.VanillaQuestSamples, ReadInt(actual, "VanillaQuestSamples"));
        CompareBenchmarkInt(mismatches, "VanillaRestartableSamples", expected.VanillaRestartableSamples, ReadInt(actual, "VanillaRestartableSamples"));
        CompareBenchmarkDouble(mismatches, "VanillaMedianHandbookValue", expected.VanillaMedianHandbookValue, ReadDouble(actual, "VanillaMedianHandbookValue"));
        CompareBenchmarkDouble(mismatches, "VanillaP90HandbookValue", expected.VanillaP90HandbookValue, ReadDouble(actual, "VanillaP90HandbookValue"));
        CompareBenchmarkDouble(mismatches, "VanillaMedianNormalizedHandbookValue", expected.VanillaMedianNormalizedHandbookValue, ReadDouble(actual, "VanillaMedianNormalizedHandbookValue"));
        CompareBenchmarkDouble(mismatches, "VanillaP90NormalizedHandbookValue", expected.VanillaP90NormalizedHandbookValue, ReadDouble(actual, "VanillaP90NormalizedHandbookValue"));
        CompareBenchmarkDouble(mismatches, "VanillaRestartableMedianHandbookValue", expected.VanillaRestartableMedianHandbookValue, ReadDouble(actual, "VanillaRestartableMedianHandbookValue"));
        CompareBenchmarkDouble(mismatches, "VanillaRestartableMedianNormalizedHandbookValue", expected.VanillaRestartableMedianNormalizedHandbookValue, ReadDouble(actual, "VanillaRestartableMedianNormalizedHandbookValue"));
    }

    private static void CompareQuest(List<PrimaryAuditParityMismatch> mismatches, string questId, string field, bool expected, bool actual)
    {
        if (expected != actual) AddMismatch(mismatches, "Quest", questId, field, expected.ToString(), actual.ToString());
    }

    private static void CompareQuest(List<PrimaryAuditParityMismatch> mismatches, string questId, string field, int expected, int actual)
    {
        if (expected != actual) AddMismatch(mismatches, "Quest", questId, field, expected.ToString(), actual.ToString());
    }

    private static void CompareQuestDouble(List<PrimaryAuditParityMismatch> mismatches, string questId, string field, double expected, double actual)
    {
        if (Math.Abs(expected - actual) > 0.005d) AddMismatch(mismatches, "Quest", questId, field, expected.ToString("0.####"), actual.ToString("0.####"));
    }

    private static void CompareBenchmarkInt(List<PrimaryAuditParityMismatch> mismatches, string field, int expected, int actual)
    {
        if (expected != actual) AddMismatch(mismatches, "Benchmark", "global", field, expected.ToString(), actual.ToString());
    }

    private static void CompareBenchmarkDouble(List<PrimaryAuditParityMismatch> mismatches, string field, double expected, double actual)
    {
        if (Math.Abs(expected - actual) > 0.005d) AddMismatch(mismatches, "Benchmark", "global", field, expected.ToString("0.####"), actual.ToString("0.####"));
    }

    private static void AddMismatch(List<PrimaryAuditParityMismatch> mismatches, string scope, string subjectId, string field, string expected, string actual) => mismatches.Add(new PrimaryAuditParityMismatch
    {
        Scope = scope,
        SubjectId = subjectId,
        Field = field,
        Expected = expected,
        Actual = actual,
    });

    private static string ReadString(JsonObject row, string key) => row[key]?.GetValue<string>() ?? string.Empty;
    private static bool ReadBool(JsonObject row, string key) => row[key]?.GetValue<bool>() ?? false;
    private static int ReadInt(JsonObject row, string key) => row[key]?.GetValue<int>() ?? 0;
    private static double ReadDouble(JsonObject row, string key) => row[key]?.GetValue<double>() ?? 0d;

    private static string SafePath(string modPath, string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(modPath, relativePath));
        var root = Path.GetFullPath(modPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Economy Admiral primary parity path must stay inside the mod directory.");
        return path;
    }
}
