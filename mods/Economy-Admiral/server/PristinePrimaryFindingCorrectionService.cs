using System.Text.Json;
using System.Text.Json.Nodes;
using Path = System.IO.Path;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;

namespace SPTEconomy;

/// <summary>
/// Rebuilds quest reward findings only after the primary report has its final pristine benchmark.
/// This keeps displayed benchmark values and finding thresholds on exactly the same source data.
/// </summary>
[Injectable]
public sealed class PristinePrimaryFindingCorrectionService(
    EconomyRuntimeConfigService runtimeConfigService,
    ModHelper modHelper,
    ISptLogger<PristinePrimaryFindingCorrectionService> logger
)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly HashSet<string> RewardFindingCodes = new(StringComparer.Ordinal)
    {
        "QUEST_REWARD_UNPRICED_ITEMS",
        "QUEST_REWARD_VALUE_OUTLIER",
        "RESTARTABLE_REWARD_VALUE_OUTLIER",
        "QUEST_REWARD_BUDGET_OUTLIER",
        "RESTARTABLE_REWARD_BUDGET_OUTLIER",
    };

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var config = await runtimeConfigService.GetAsync(cancellationToken);
        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(PristinePrimaryFindingCorrectionService).Assembly);
        var reportPath = SafePath(modPath, config.ReportRelativePath);
        var root = JsonNode.Parse(await File.ReadAllTextAsync(reportPath, cancellationToken))?.AsObject()
            ?? throw new InvalidOperationException("Economy Admiral primary report could not be parsed for pristine finding correction.");

        if (!string.Equals(root["VanillaBenchmarkSource"]?.GetValue<string>(), "PristineStartupSnapshot", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Economy Admiral primary findings require the pristine benchmark to be applied first.");
        }

        var benchmark = root["VanillaQuestRewardBenchmark"]?.AsObject()
            ?? throw new InvalidOperationException("Economy Admiral primary report has no pristine VanillaQuestRewardBenchmark.");
        var audits = root["QuestRewardAudits"]?.AsArray()
            ?? throw new InvalidOperationException("Economy Admiral primary report has no QuestRewardAudits.");
        var policy = root["Policy"]?.AsObject();

        var retained = new List<JsonObject>();
        if (root["Findings"] is JsonArray existing)
        {
            retained.AddRange(existing.OfType<JsonObject>()
                .Where(row => !RewardFindingCodes.Contains(ReadString(row, "Code")))
                .Select(row => (JsonObject)row.DeepClone()));
        }

        var normalMedian = ReadDouble(benchmark, "VanillaMedianHandbookValue");
        var restartableMedian = ReadDouble(benchmark, "VanillaRestartableMedianHandbookValue");
        var normalNormalizedMedian = ReadDouble(benchmark, "VanillaMedianNormalizedHandbookValue");
        var restartableNormalizedMedian = ReadDouble(benchmark, "VanillaRestartableMedianNormalizedHandbookValue");

        var normalRawMultiple = ReadDouble(policy, "QuestRewardVsVanillaMedianWarnMultiple", 3.0);
        var restartableRawMultiple = ReadDouble(policy, "RestartableRewardVsVanillaMedianWarnMultiple", 1.5);
        var normalNormalizedMultiple = ReadDouble(policy, "NormalizedRewardVsVanillaMedianWarnMultiple", 2.5);
        var restartableNormalizedMultiple = ReadDouble(policy, "RestartableNormalizedRewardVsVanillaMedianWarnMultiple", 1.25);

        foreach (var audit in audits.OfType<JsonObject>())
        {
            var questId = ReadString(audit, "QuestId");
            var restartable = ReadBool(audit, "Restartable");
            var unknown = ReadInt(audit, "UnknownPriceItemRecords");
            var raw = ReadDouble(audit, "KnownHandbookValue");
            var normalized = ReadDouble(audit, "NormalizedHandbookValue");

            if (unknown > 0)
            {
                retained.Add(NewFinding(
                    "Info", "QUEST_REWARD_UNPRICED_ITEMS", questId,
                    $"Quest reward contains {unknown} item records without handbook prices.", unknown, null));
            }

            var rawBaseline = restartable && restartableMedian > 0 ? restartableMedian : normalMedian;
            var rawMultiple = restartable ? restartableRawMultiple : normalRawMultiple;
            var rawThreshold = rawBaseline * rawMultiple;
            if (rawBaseline > 0 && raw > rawThreshold)
            {
                retained.Add(NewFinding(
                    restartable ? "Error" : "Warning",
                    restartable ? "RESTARTABLE_REWARD_VALUE_OUTLIER" : "QUEST_REWARD_VALUE_OUTLIER",
                    questId,
                    $"Known handbook reward value {raw:0.##} exceeds the pristine vanilla median benchmark threshold.",
                    raw,
                    Math.Round(rawThreshold, 2)));
            }

            var normalizedBaseline = restartable && restartableNormalizedMedian > 0
                ? restartableNormalizedMedian
                : normalNormalizedMedian;
            var normalizedMultiple = restartable ? restartableNormalizedMultiple : normalNormalizedMultiple;
            var normalizedThreshold = normalizedBaseline * normalizedMultiple;
            if (normalizedBaseline > 0 && normalized > normalizedThreshold)
            {
                retained.Add(NewFinding(
                    restartable ? "Error" : "Warning",
                    restartable ? "RESTARTABLE_REWARD_BUDGET_OUTLIER" : "QUEST_REWARD_BUDGET_OUTLIER",
                    questId,
                    $"Progression-normalized reward value {normalized:0.##} exceeds the pristine vanilla normalized median threshold.",
                    normalized,
                    Math.Round(normalizedThreshold, 2)));
            }
        }

        var ordered = retained
            .OrderBy(row => ReadString(row, "Code"), StringComparer.Ordinal)
            .ThenBy(row => ReadString(row, "SubjectType"), StringComparer.Ordinal)
            .ThenBy(row => ReadString(row, "SubjectId"), StringComparer.Ordinal)
            .ToList();
        var output = new JsonArray();
        foreach (var finding in ordered) output.Add(finding);
        root["Findings"] = output;
        root["QuestRewardFindingBenchmarkSource"] = "PristineStartupSnapshot";

        await File.WriteAllTextAsync(reportPath, root.ToJsonString(JsonOptions), cancellationToken);
        logger.Info($"[Economy Admiral] primary quest reward findings rebuilt against pristine benchmark: rewardFindings={ordered.Count(row => RewardFindingCodes.Contains(ReadString(row, "Code")))}; report={reportPath}");
    }

    private static JsonObject NewFinding(string severity, string code, string questId, string detail, double? metric, double? threshold) => new()
    {
        ["Severity"] = severity,
        ["Code"] = code,
        ["SubjectType"] = "Quest",
        ["SubjectId"] = questId,
        ["Detail"] = detail,
        ["Metric"] = metric,
        ["Threshold"] = threshold,
    };

    private static string ReadString(JsonObject? row, string key) => row?[key]?.GetValue<string>() ?? string.Empty;
    private static bool ReadBool(JsonObject? row, string key) => row?[key]?.GetValue<bool>() ?? false;
    private static int ReadInt(JsonObject? row, string key) => row?[key]?.GetValue<int>() ?? 0;
    private static double ReadDouble(JsonObject? row, string key, double fallback = 0d) => row?[key]?.GetValue<double>() ?? fallback;

    private static string SafePath(string modPath, string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(modPath, relativePath));
        var root = Path.GetFullPath(modPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Economy Admiral pristine finding correction path must stay inside the mod directory.");
        return path;
    }
}
