using System.Text.Json;
using System.Text.Json.Nodes;
using Path = System.IO.Path;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTEconomy;

/// <summary>
/// Runtime-proven correction layer for quest item rewards.
///
/// SPT 4.1 exposes quest reward items as typed Reward.Items records. The original MVP
/// recursively serialized rewards to JsonElement and required _tpl to serialize as a JSON
/// string. MongoId does not satisfy that assumption, which produced a silent zero-valued
/// item-reward benchmark at runtime. This service uses the typed Reward.Items contract only.
///
/// It intentionally runs as an overlay while the first MVP is still a draft. It repairs the
/// generated audit report, returns a corrected in-memory unified analysis to all downstream
/// policy services, and never mutates TemplateTable/TradersTable.
/// </summary>
[Injectable]
public sealed class TypedQuestItemAccountingService(
    TemplateTable templates,
    EconomyRuntimeConfigService runtimeConfigService,
    ModHelper modHelper,
    ISptLogger<TypedQuestItemAccountingService> logger
)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public async Task RepairPrimaryAuditReportAsync(CancellationToken cancellationToken)
    {
        var config = await runtimeConfigService.GetAsync(cancellationToken);
        if (config.Mode == EconomyMode.Off)
        {
            return;
        }

        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(TypedQuestItemAccountingService).Assembly);
        var reportPath = SafePath(modPath, config.ReportRelativePath);
        if (!File.Exists(reportPath))
        {
            throw new InvalidOperationException("Economy Admiral primary audit report is missing before typed quest-item accounting.");
        }

        var root = JsonNode.Parse(await File.ReadAllTextAsync(reportPath, cancellationToken))?.AsObject()
            ?? throw new InvalidOperationException("Economy Admiral primary audit report could not be parsed.");

        var handbookPrices = BuildHandbookPrices();
        var allRewardMetrics = BuildQuestMetrics(successOnly: false, handbookPrices);
        var questRewardSources = BuildQuestRewardSources();

        var questAudits = root["QuestRewardAudits"]?.AsArray()
            ?? throw new InvalidOperationException("Economy Admiral primary audit report has no QuestRewardAudits array.");

        foreach (var node in questAudits)
        {
            if (node is not JsonObject row)
            {
                continue;
            }

            var questId = ReadString(row, "QuestId");
            if (!allRewardMetrics.TryGetValue(questId, out var metric))
            {
                continue;
            }

            var progressionScore = ReadDouble(row, "ProgressionScore");
            var normalized = progressionScore > 0 ? metric.KnownHandbookValue / progressionScore : metric.KnownHandbookValue;

            row["RewardItemRecords"] = metric.ItemRecordCount;
            row["DistinctRewardTemplates"] = metric.DistinctTemplateCount;
            row["KnownHandbookValue"] = Math.Round(metric.KnownHandbookValue, 2);
            row["NormalizedHandbookValue"] = Math.Round(normalized, 2);
            row["UnknownPriceItemRecords"] = metric.UnknownPriceItemRecords;
        }

        var benchmark = BuildAuditBenchmark(questAudits);
        root["VanillaQuestRewardBenchmark"] = BuildBenchmarkJson(benchmark);

        var correctedItems = RebuildAcquisitionItems(root["Items"]?.AsArray(), questRewardSources, config);
        root["Items"] = correctedItems;

        var acquisition = root["Acquisition"]?.AsObject()
            ?? throw new InvalidOperationException("Economy Admiral primary audit report has no Acquisition object.");
        acquisition["ItemsWithKnownAcquisition"] = correctedItems.Count;
        acquisition["TraderSourceEdges"] = correctedItems
            .OfType<JsonObject>()
            .Sum(row => row["TraderSources"]?.AsArray().Count ?? 0);
        acquisition["QuestRewardSourceEdges"] = correctedItems
            .OfType<JsonObject>()
            .Sum(row => row["QuestRewardSources"]?.AsArray().Count ?? 0);

        root["Findings"] = RebuildQuestRewardFindings(root["Findings"]?.AsArray(), questAudits, benchmark, root["Policy"]?.AsObject());

        await File.WriteAllTextAsync(reportPath, root.ToJsonString(JsonOptions), cancellationToken);

        var vanillaItemSamples = benchmark.VanillaQuestSamples;
        var questEdges = acquisition["QuestRewardSourceEdges"]?.GetValue<int>() ?? 0;
        if (vanillaItemSamples <= 0 || questEdges <= 0)
        {
            throw new InvalidOperationException(
                $"Economy Admiral typed quest-item accounting sanity gate failed: vanillaItemSamples={vanillaItemSamples}, questRewardSourceEdges={questEdges}."
            );
        }

        logger.Info(
            $"[Economy Admiral] typed quest-item accounting repaired primary audit: vanillaItemSamples={vanillaItemSamples}, " +
            $"questRewardSourceEdges={questEdges}, acquisitionItems={correctedItems.Count}; report={reportPath}"
        );
    }

    public async Task<QuestAnalysisReport> ApplyToUnifiedAnalysisAsync(
        QuestAnalysisReport analysis,
        CancellationToken cancellationToken
    )
    {
        var handbookPrices = BuildHandbookPrices();
        var successMetrics = BuildQuestMetrics(successOnly: true, handbookPrices);

        var rawRows = analysis.Quests
            .OrderBy(row => row.QuestId, StringComparer.Ordinal)
            .Select(row =>
            {
                successMetrics.TryGetValue(row.QuestId, out var metric);
                metric ??= QuestItemMetric.Empty;

                var retainedFlags = row.ObservationalFlags
                    .Where(flag => !string.Equals(flag, "HIGH_ITEM_VALUE_LOW_STRUCTURE", StringComparison.Ordinal)
                        && !string.Equals(flag, "RESTARTABLE_HIGH_ITEM_VALUE", StringComparison.Ordinal))
                    .OrderBy(flag => flag, StringComparer.Ordinal)
                    .ToList();

                return row with
                {
                    SuccessKnownHandbookValue = Math.Round(metric.KnownHandbookValue, 2),
                    UnknownPriceRewardItemRecords = metric.UnknownPriceItemRecords,
                    HandbookValueVsVanillaMedian = null,
                    ObservationalFlags = retainedFlags,
                };
            })
            .ToList();

        var vanillaMedian = MedianPositive(rawRows
            .Where(row => row.IsVanillaTraderQuest && !row.Restartable)
            .Select(row => row.SuccessKnownHandbookValue));
        var restartableMedian = MedianPositive(rawRows
            .Where(row => row.IsVanillaTraderQuest && row.Restartable)
            .Select(row => row.SuccessKnownHandbookValue));

        var vanilla = analysis.Vanilla with { MedianSuccessHandbookValue = vanillaMedian };
        var vanillaRestartable = analysis.VanillaRestartable with { MedianSuccessHandbookValue = restartableMedian };

        var rows = rawRows
            .Select(row => ApplyItemRelativeSignalsAndFlags(row, analysis.Policy, vanilla, vanillaRestartable))
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
            Note = analysis.Note + " Item-reward value is overlaid from typed SPT Reward.Items/Item.Template/Upd.StackObjectsCount records.",
            Quests = rows,
        };

        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(TypedQuestItemAccountingService).Assembly);
        var reportPath = SafePath(modPath, "reports/economy-admiral-quest-analysis.json");
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(corrected, JsonOptions), cancellationToken);

        var pricedVanillaSamples = rows.Count(row => row.IsVanillaTraderQuest && !row.Restartable && row.SuccessKnownHandbookValue > 0);
        if (pricedVanillaSamples <= 0 || corrected.Vanilla.MedianSuccessHandbookValue <= 0)
        {
            throw new InvalidOperationException(
                $"Economy Admiral typed unified-analysis sanity gate failed: pricedVanillaSamples={pricedVanillaSamples}, " +
                $"median={corrected.Vanilla.MedianSuccessHandbookValue}."
            );
        }

        logger.Info(
            $"[Economy Admiral] typed quest-item overlay applied to unified analysis: pricedVanillaSamples={pricedVanillaSamples}, " +
            $"median={corrected.Vanilla.MedianSuccessHandbookValue:0.##}, flags={flagCounts.Values.Sum()}; report={reportPath}"
        );

        return corrected;
    }

    private Dictionary<string, double> BuildHandbookPrices() => templates.Handbook.Items
        .Where(item => item.Price is > 0)
        .GroupBy(item => item.Id.ToString(), StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First().Price!.Value, StringComparer.Ordinal);

    private Dictionary<string, QuestItemMetric> BuildQuestMetrics(
        bool successOnly,
        IReadOnlyDictionary<string, double> handbookPrices
    )
    {
        var result = new Dictionary<string, QuestItemMetric>(StringComparer.Ordinal);
        foreach (var pair in templates.Quests.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
        {
            var rewards = EnumerateRewards(pair.Value, successOnly).ToList();
            var items = rewards
                .Where(reward => reward.Items is not null)
                .SelectMany(reward => reward.Items!)
                .ToList();

            var knownValue = 0d;
            var unknown = 0;
            var templateIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                var templateId = item.Template.ToString();
                if (string.IsNullOrWhiteSpace(templateId))
                {
                    continue;
                }

                templateIds.Add(templateId);
                var count = Math.Max(1d, item.Upd?.StackObjectsCount ?? 1d);
                if (handbookPrices.TryGetValue(templateId, out var price))
                {
                    knownValue += price * count;
                }
                else
                {
                    unknown++;
                }
            }

            result[pair.Key.ToString()] = new QuestItemMetric(
                items.Count,
                templateIds.Count,
                Math.Round(knownValue, 2),
                unknown
            );
        }

        return result;
    }

    private Dictionary<string, SortedSet<string>> BuildQuestRewardSources()
    {
        var result = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var pair in templates.Quests.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
        {
            var questId = pair.Key.ToString();
            var templatesInQuest = EnumerateRewards(pair.Value, successOnly: false)
                .Where(reward => reward.Items is not null)
                .SelectMany(reward => reward.Items!)
                .Select(item => item.Template.ToString())
                .Where(templateId => !string.IsNullOrWhiteSpace(templateId))
                .Distinct(StringComparer.Ordinal);

            foreach (var templateId in templatesInQuest)
            {
                if (!result.TryGetValue(templateId, out var questIds))
                {
                    questIds = new SortedSet<string>(StringComparer.Ordinal);
                    result[templateId] = questIds;
                }
                questIds.Add(questId);
            }
        }
        return result;
    }

    private static IEnumerable<Reward> EnumerateRewards(Quest quest, bool successOnly)
    {
        if (quest.Rewards is null)
        {
            yield break;
        }

        foreach (var pair in quest.Rewards)
        {
            if (successOnly && !string.Equals(pair.Key, "Success", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var reward in pair.Value)
            {
                yield return reward;
            }
        }
    }

    private static JsonArray RebuildAcquisitionItems(
        JsonArray? existingRows,
        IReadOnlyDictionary<string, SortedSet<string>> questRewardSources,
        EconomyConfig config
    )
    {
        var existing = new Dictionary<string, (List<string> TraderSources, string? OverrideNote)>(StringComparer.Ordinal);
        if (existingRows is not null)
        {
            foreach (var node in existingRows.OfType<JsonObject>())
            {
                var templateId = ReadString(node, "TemplateId");
                existing[templateId] = (ReadStringList(node["TraderSources"]?.AsArray()), ReadNullableString(node, "OverrideNote"));
            }
        }

        var allTemplateIds = existing.Keys
            .Concat(questRewardSources.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal);

        var rows = new JsonArray();
        foreach (var templateId in allTemplateIds)
        {
            existing.TryGetValue(templateId, out var current);
            var traderSources = (current.TraderSources ?? []).OrderBy(value => value, StringComparer.Ordinal).ToList();
            var questSources = questRewardSources.TryGetValue(templateId, out var sourceSet)
                ? sourceSet.ToList()
                : [];

            config.ManualOverrides.TryGetValue(templateId, out var manualOverride);
            if (manualOverride?.Ignore == true)
            {
                continue;
            }

            var totalSources = traderSources.Count + questSources.Count;
            var rarity = !string.IsNullOrWhiteSpace(manualOverride?.Rarity)
                ? manualOverride!.Rarity!
                : ClassifyRarity(totalSources, config.Rarity);
            var note = manualOverride?.Note ?? current.OverrideNote;

            rows.Add(new JsonObject
            {
                ["TemplateId"] = templateId,
                ["Rarity"] = rarity,
                ["Ignored"] = false,
                ["OverrideNote"] = note,
                ["TraderSources"] = ToJsonArray(traderSources),
                ["QuestRewardSources"] = ToJsonArray(questSources),
            });
        }

        return rows;
    }

    private static string ClassifyRarity(int sourceCount, RarityThresholds thresholds)
    {
        if (sourceCount >= thresholds.CommonMinSources) return "Common";
        if (sourceCount >= thresholds.UncommonMinSources) return "Uncommon";
        if (sourceCount >= thresholds.RareMinSources) return "Rare";
        return "Exceptional";
    }

    private static AuditBenchmark BuildAuditBenchmark(JsonArray questAudits)
    {
        var vanilla = questAudits.OfType<JsonObject>()
            .Where(row => ReadBool(row, "IsVanillaTraderQuest") && !ReadBool(row, "Restartable"))
            .ToList();
        var restartable = questAudits.OfType<JsonObject>()
            .Where(row => ReadBool(row, "IsVanillaTraderQuest") && ReadBool(row, "Restartable"))
            .ToList();

        var values = PositiveSorted(vanilla.Select(row => ReadDouble(row, "KnownHandbookValue")));
        var normalized = PositiveSorted(vanilla.Select(row => ReadDouble(row, "NormalizedHandbookValue")));
        var restartableValues = PositiveSorted(restartable.Select(row => ReadDouble(row, "KnownHandbookValue")));
        var restartableNormalized = PositiveSorted(restartable.Select(row => ReadDouble(row, "NormalizedHandbookValue")));

        return new AuditBenchmark(
            values.Count,
            Percentile(values, 0.50),
            Percentile(values, 0.90),
            Percentile(normalized, 0.50),
            Percentile(normalized, 0.90),
            restartableValues.Count,
            Percentile(restartableValues, 0.50),
            Percentile(restartableNormalized, 0.50)
        );
    }

    private static JsonObject BuildBenchmarkJson(AuditBenchmark benchmark) => new()
    {
        ["VanillaQuestSamples"] = benchmark.VanillaQuestSamples,
        ["VanillaMedianHandbookValue"] = benchmark.VanillaMedianHandbookValue,
        ["VanillaP90HandbookValue"] = benchmark.VanillaP90HandbookValue,
        ["VanillaMedianNormalizedHandbookValue"] = benchmark.VanillaMedianNormalizedHandbookValue,
        ["VanillaP90NormalizedHandbookValue"] = benchmark.VanillaP90NormalizedHandbookValue,
        ["VanillaRestartableSamples"] = benchmark.VanillaRestartableSamples,
        ["VanillaRestartableMedianHandbookValue"] = benchmark.VanillaRestartableMedianHandbookValue,
        ["VanillaRestartableMedianNormalizedHandbookValue"] = benchmark.VanillaRestartableMedianNormalizedHandbookValue,
    };

    private static JsonArray RebuildQuestRewardFindings(
        JsonArray? existing,
        JsonArray questAudits,
        AuditBenchmark benchmark,
        JsonObject? policyNode
    )
    {
        var removeCodes = new HashSet<string>(StringComparer.Ordinal)
        {
            "QUEST_REWARD_UNPRICED_ITEMS",
            "QUEST_REWARD_VALUE_OUTLIER",
            "RESTARTABLE_REWARD_VALUE_OUTLIER",
            "QUEST_REWARD_BUDGET_OUTLIER",
            "RESTARTABLE_REWARD_BUDGET_OUTLIER",
        };

        var findings = new List<JsonObject>();
        if (existing is not null)
        {
            foreach (var row in existing.OfType<JsonObject>())
            {
                var code = ReadString(row, "Code");
                if (!removeCodes.Contains(code))
                {
                    findings.Add((JsonObject)row.DeepClone());
                }
            }
        }

        var rawNormalMultiple = ReadDouble(policyNode, "QuestRewardVsVanillaMedianWarnMultiple", 3.0);
        var rawRestartableMultiple = ReadDouble(policyNode, "RestartableRewardVsVanillaMedianWarnMultiple", 1.5);
        var normalizedNormalMultiple = ReadDouble(policyNode, "NormalizedRewardVsVanillaMedianWarnMultiple", 2.5);
        var normalizedRestartableMultiple = ReadDouble(policyNode, "RestartableNormalizedRewardVsVanillaMedianWarnMultiple", 1.25);

        foreach (var row in questAudits.OfType<JsonObject>())
        {
            var questId = ReadString(row, "QuestId");
            var restartable = ReadBool(row, "Restartable");
            var unknown = ReadInt(row, "UnknownPriceItemRecords");
            var known = ReadDouble(row, "KnownHandbookValue");
            var normalized = ReadDouble(row, "NormalizedHandbookValue");

            if (unknown > 0)
            {
                findings.Add(NewFinding(
                    "Info",
                    "QUEST_REWARD_UNPRICED_ITEMS",
                    questId,
                    $"Quest reward contains {unknown} item records without handbook prices.",
                    unknown,
                    null
                ));
            }

            var rawBaseline = restartable && benchmark.VanillaRestartableMedianHandbookValue > 0
                ? benchmark.VanillaRestartableMedianHandbookValue
                : benchmark.VanillaMedianHandbookValue;
            var rawMultiple = restartable ? rawRestartableMultiple : rawNormalMultiple;
            var rawThreshold = rawBaseline * rawMultiple;
            if (rawBaseline > 0 && known > rawThreshold)
            {
                findings.Add(NewFinding(
                    restartable ? "Error" : "Warning",
                    restartable ? "RESTARTABLE_REWARD_VALUE_OUTLIER" : "QUEST_REWARD_VALUE_OUTLIER",
                    questId,
                    $"Known handbook reward value {known:0.##} exceeds the vanilla median benchmark threshold.",
                    known,
                    Math.Round(rawThreshold, 2)
                ));
            }

            var normalizedBaseline = restartable && benchmark.VanillaRestartableMedianNormalizedHandbookValue > 0
                ? benchmark.VanillaRestartableMedianNormalizedHandbookValue
                : benchmark.VanillaMedianNormalizedHandbookValue;
            var normalizedMultiple = restartable ? normalizedRestartableMultiple : normalizedNormalMultiple;
            var normalizedThreshold = normalizedBaseline * normalizedMultiple;
            if (normalizedBaseline > 0 && normalized > normalizedThreshold)
            {
                findings.Add(NewFinding(
                    restartable ? "Error" : "Warning",
                    restartable ? "RESTARTABLE_REWARD_BUDGET_OUTLIER" : "QUEST_REWARD_BUDGET_OUTLIER",
                    questId,
                    $"Progression-normalized reward value {normalized:0.##} exceeds the vanilla normalized median threshold.",
                    normalized,
                    Math.Round(normalizedThreshold, 2)
                ));
            }
        }

        var ordered = findings
            .OrderBy(row => ReadString(row, "Code"), StringComparer.Ordinal)
            .ThenBy(row => ReadString(row, "SubjectType"), StringComparer.Ordinal)
            .ThenBy(row => ReadString(row, "SubjectId"), StringComparer.Ordinal)
            .ToList();

        var result = new JsonArray();
        foreach (var finding in ordered)
        {
            result.Add(finding);
        }
        return result;
    }

    private static JsonObject NewFinding(
        string severity,
        string code,
        string questId,
        string detail,
        double? metric,
        double? threshold
    ) => new()
    {
        ["Severity"] = severity,
        ["Code"] = code,
        ["SubjectType"] = "Quest",
        ["SubjectId"] = questId,
        ["Detail"] = detail,
        ["Metric"] = metric,
        ["Threshold"] = threshold,
    };

    private static QuestAnalysisRow ApplyItemRelativeSignalsAndFlags(
        QuestAnalysisRow row,
        AuditPolicy policy,
        QuestAnalysisBaseline vanilla,
        QuestAnalysisBaseline restartable
    )
    {
        var baseline = row.Restartable && restartable.QuestSamples > 0 ? restartable : vanilla;
        var ratio = Ratio(row.SuccessKnownHandbookValue, baseline.MedianSuccessHandbookValue);
        var flags = row.ObservationalFlags.ToList();
        var lowDepth = row.PrerequisiteDepthVsVanillaMedian is null
            || row.PrerequisiteDepthVsVanillaMedian <= policy.LowDepthMaxRelativeMultiple;
        var lowStructure = row.StructuredConstraintsVsVanillaMedian is null
            || row.StructuredConstraintsVsVanillaMedian <= policy.LowStructureMaxRelativeMultiple;

        if (ratio >= policy.HighItemValueLowStructureWarnMultiple && lowDepth && lowStructure)
        {
            flags.Add("HIGH_ITEM_VALUE_LOW_STRUCTURE");
        }
        if (row.Restartable && ratio >= policy.RestartableHighItemValueWarnMultiple)
        {
            flags.Add("RESTARTABLE_HIGH_ITEM_VALUE");
        }

        return row with
        {
            HandbookValueVsVanillaMedian = ratio,
            ObservationalFlags = flags.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList(),
        };
    }

    private static JsonObject? GetObject(JsonNode? node) => node as JsonObject;

    private static string ReadString(JsonObject row, string key) => row[key]?.GetValue<string>() ?? string.Empty;
    private static string? ReadNullableString(JsonObject row, string key) => row[key] is null ? null : row[key]!.GetValue<string?>();
    private static bool ReadBool(JsonObject row, string key) => row[key]?.GetValue<bool>() ?? false;
    private static int ReadInt(JsonObject row, string key) => row[key]?.GetValue<int>() ?? 0;
    private static double ReadDouble(JsonObject row, string key) => row[key]?.GetValue<double>() ?? 0d;
    private static double ReadDouble(JsonObject? row, string key, double fallback) => row?[key]?.GetValue<double>() ?? fallback;

    private static List<string> ReadStringList(JsonArray? array) => array is null
        ? []
        : array.Where(node => node is not null).Select(node => node!.GetValue<string>()).ToList();

    private static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values.OrderBy(value => value, StringComparer.Ordinal))
        {
            array.Add(value);
        }
        return array;
    }

    private static List<double> PositiveSorted(IEnumerable<double> values) => values
        .Where(value => value > 0)
        .OrderBy(value => value)
        .ToList();

    private static double MedianPositive(IEnumerable<double> values) => Percentile(PositiveSorted(values), 0.50);
    private static double? Ratio(double value, double baseline) => value > 0 && baseline > 0 ? Math.Round(value / baseline, 4) : null;

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

    private sealed record QuestItemMetric(
        int ItemRecordCount,
        int DistinctTemplateCount,
        double KnownHandbookValue,
        int UnknownPriceItemRecords
    )
    {
        public static QuestItemMetric Empty { get; } = new(0, 0, 0, 0);
    }

    private sealed record AuditBenchmark(
        int VanillaQuestSamples,
        double VanillaMedianHandbookValue,
        double VanillaP90HandbookValue,
        double VanillaMedianNormalizedHandbookValue,
        double VanillaP90NormalizedHandbookValue,
        int VanillaRestartableSamples,
        double VanillaRestartableMedianHandbookValue,
        double VanillaRestartableMedianNormalizedHandbookValue
    );
}
