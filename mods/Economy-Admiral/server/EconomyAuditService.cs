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
/// Source-correct primary audit path proven by the physical SPT 4.1.3 parity gate.
/// Reads typed final quest rewards directly and uses the pristine startup snapshot for
/// vanilla membership/benchmarks. It does not mutate TemplateTable or TradersTable.
/// </summary>
[Injectable]
public sealed class EconomyAuditService(
    TemplateTable templates,
    TradersTable traders,
    ModHelper modHelper,
    EconomyRuntimeConfigService runtimeConfigService,
    ISptLogger<EconomyAuditService> logger
)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public async Task RunAsync(VanillaBaselineSnapshot baseline, CancellationToken cancellationToken)
    {
        var config = await runtimeConfigService.GetAsync(cancellationToken);
        if (config.Mode == EconomyMode.Off) return;

        var policy = ResolvePolicy(config);
        var handbookPrices = templates.Handbook.Items
            .Where(item => item.Price is > 0)
            .GroupBy(item => item.Id.ToString(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Price!.Value, StringComparer.Ordinal);

        var findings = new List<AuditFinding>();
        var acquisitions = new Dictionary<string, MutableAcquisition>(StringComparer.Ordinal);
        var traderAudits = ScanTraderAcquisition(acquisitions, findings);
        var questAudits = ScanQuestRewards(acquisitions, handbookPrices, policy, baseline);
        var benchmark = BuildPristineBenchmark(baseline, policy);
        AddQuestAuditFindings(questAudits, benchmark, policy, findings);

        var items = acquisitions
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => FinalizeItem(pair.Key, pair.Value, config))
            .Where(item => !item.Ignored)
            .ToList();

        foreach (var item in items.Where(item => item.TraderSources.Count >= policy.DuplicateTraderSourcesWarnCount))
        {
            findings.Add(new AuditFinding
            {
                Severity = "Warning",
                Code = "TRADER_SOURCE_SATURATION",
                SubjectType = "Item",
                SubjectId = item.TemplateId,
                Detail = $"Item is sold by {item.TraderSources.Count} traders.",
                Metric = item.TraderSources.Count,
                Threshold = policy.DuplicateTraderSourcesWarnCount,
            });
        }

        var report = new EconomyAuditReport
        {
            SchemaVersion = 3,
            Mode = config.Mode.ToString(),
            Preset = config.Preset.ToString(),
            EnforcementApplied = false,
            RepeatedRaidLootDecay = config.RepeatedRaidLootDecay,
            RewardNormalizationModel = "knownHandbookValue / (1 + cappedLevelGate*levelGateWeight + cappedObjectiveCount*objectiveConditionWeight)",
            Policy = policy,
            Database = new DatabaseSummary
            {
                TemplateItems = templates.Items.Count,
                HandbookItemsWithPrice = handbookPrices.Count,
                Quests = templates.Quests.Count,
                Traders = traders.Count,
                TraderAssortRecords = traders.Values.Sum(trader => trader.Assort.Items.Count),
            },
            Acquisition = new AcquisitionSummary
            {
                ItemsWithKnownAcquisition = items.Count,
                TraderSourceEdges = items.Sum(item => item.TraderSources.Count),
                QuestRewardSourceEdges = items.Sum(item => item.QuestRewardSources.Count),
            },
            VanillaQuestRewardBenchmark = benchmark,
            TraderAudits = traderAudits,
            QuestRewardAudits = questAudits,
            Findings = findings
                .OrderBy(finding => finding.Code, StringComparer.Ordinal)
                .ThenBy(finding => finding.SubjectType, StringComparer.Ordinal)
                .ThenBy(finding => finding.SubjectId, StringComparer.Ordinal)
                .ToList(),
            Items = items,
        };

        var root = JsonSerializer.SerializeToNode(report, JsonOptions)?.AsObject()
            ?? throw new InvalidOperationException("Economy Admiral primary audit serialization failed.");
        root["VanillaBenchmarkSource"] = "PristineStartupSnapshot";
        root["PristineQuestCount"] = baseline.QuestCount;

        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(EconomyAuditService).Assembly);
        var reportPath = SafePath(modPath, config.ReportRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllTextAsync(reportPath, root.ToJsonString(JsonOptions), cancellationToken);

        logger.Info(
            $"[Economy Admiral] primary audit complete from typed final DB + pristine startup snapshot: " +
            $"quests={report.Database.Quests}, pristine={baseline.QuestCount}, questRewardEdges={report.Acquisition.QuestRewardSourceEdges}; report={reportPath}"
        );
    }

    private List<TraderAudit> ScanTraderAcquisition(
        Dictionary<string, MutableAcquisition> acquisitions,
        List<AuditFinding> findings
    )
    {
        var audits = new List<TraderAudit>();
        foreach (var traderPair in traders.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
        {
            var traderId = traderPair.Key.ToString();
            var assort = traderPair.Value.Assort;
            var roots = assort.Items
                .Where(item => string.Equals(item.ParentId, "hideout", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var missingBarter = 0;
            var missingLoyalty = 0;

            foreach (var item in roots)
            {
                var templateId = item.Template.ToString();
                if (!string.IsNullOrWhiteSpace(templateId))
                    GetOrCreate(acquisitions, templateId).TraderSources.Add(traderId);

                if (!assort.BarterScheme.ContainsKey(item.Id))
                {
                    missingBarter++;
                    findings.Add(new AuditFinding
                    {
                        Severity = "Error",
                        Code = "TRADER_OFFER_MISSING_BARTER",
                        SubjectType = "TraderOffer",
                        SubjectId = $"{traderId}:{item.Id}",
                        Detail = "Root trader offer has no barter scheme.",
                    });
                }

                if (!assort.LoyalLevelItems.ContainsKey(item.Id))
                {
                    missingLoyalty++;
                    findings.Add(new AuditFinding
                    {
                        Severity = "Error",
                        Code = "TRADER_OFFER_MISSING_LOYALTY",
                        SubjectType = "TraderOffer",
                        SubjectId = $"{traderId}:{item.Id}",
                        Detail = "Root trader offer has no loyalty-level mapping.",
                    });
                }
            }

            audits.Add(new TraderAudit
            {
                TraderId = traderId,
                TraderName = traderPair.Value.Base.Name,
                RootOffers = roots.Count,
                MissingBarterSchemes = missingBarter,
                MissingLoyaltyMappings = missingLoyalty,
            });
        }
        return audits;
    }

    private List<QuestRewardAudit> ScanQuestRewards(
        Dictionary<string, MutableAcquisition> acquisitions,
        IReadOnlyDictionary<string, double> handbookPrices,
        AuditPolicy policy,
        VanillaBaselineSnapshot baseline
    )
    {
        var audits = new List<QuestRewardAudit>();
        foreach (var questPair in templates.Quests.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
        {
            var quest = questPair.Value;
            var questId = questPair.Key.ToString();
            var typedItems = EnumerateRewards(quest)
                .Where(reward => reward.Items is not null)
                .SelectMany(reward => reward.Items!)
                .ToList();

            var knownValue = 0d;
            var unknownPriceItems = 0;
            var distinctTemplates = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in typedItems)
            {
                var templateId = item.Template.ToString();
                if (string.IsNullOrWhiteSpace(templateId)) continue;
                distinctTemplates.Add(templateId);
                GetOrCreate(acquisitions, templateId).QuestRewardSources.Add(questId);
                var count = Math.Max(1d, item.Upd?.StackObjectsCount ?? 1d);
                if (handbookPrices.TryGetValue(templateId, out var price)) knownValue += price * count;
                else unknownPriceItems++;
            }

            var requiredLevel = ExtractRequiredLevel(quest.Conditions.AvailableForStart);
            var objectiveConditionCount = (quest.Conditions.AvailableForFinish?.Count ?? 0) + (quest.Conditions.Success?.Count ?? 0);
            var progressionScore = CalculateProgressionScore(requiredLevel, objectiveConditionCount, policy);
            var normalizedValue = progressionScore > 0 ? knownValue / progressionScore : knownValue;

            audits.Add(new QuestRewardAudit
            {
                QuestId = questId,
                QuestName = quest.QuestName ?? quest.Name,
                TraderId = quest.TraderId.ToString(),
                IsVanillaTraderQuest = baseline.QuestIds.Contains(questId),
                Restartable = quest.Restartable,
                RequiredLevel = requiredLevel,
                ObjectiveConditionCount = objectiveConditionCount,
                ProgressionScore = Math.Round(progressionScore, 4),
                RewardItemRecords = typedItems.Count,
                DistinctRewardTemplates = distinctTemplates.Count,
                KnownHandbookValue = Math.Round(knownValue, 2),
                NormalizedHandbookValue = Math.Round(normalizedValue, 2),
                UnknownPriceItemRecords = unknownPriceItems,
            });
        }
        return audits;
    }

    private static IEnumerable<Reward> EnumerateRewards(Quest quest)
    {
        if (quest.Rewards is null) yield break;
        foreach (var pair in quest.Rewards)
            foreach (var reward in pair.Value)
                yield return reward;
    }

    private static QuestRewardBenchmark BuildPristineBenchmark(VanillaBaselineSnapshot baseline, AuditPolicy policy)
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

    private static void AddQuestAuditFindings(
        IReadOnlyCollection<QuestRewardAudit> questAudits,
        QuestRewardBenchmark benchmark,
        AuditPolicy policy,
        List<AuditFinding> findings
    )
    {
        foreach (var audit in questAudits)
        {
            if (audit.UnknownPriceItemRecords > 0)
            {
                findings.Add(new AuditFinding
                {
                    Severity = "Info",
                    Code = "QUEST_REWARD_UNPRICED_ITEMS",
                    SubjectType = "Quest",
                    SubjectId = audit.QuestId,
                    Detail = $"Quest reward contains {audit.UnknownPriceItemRecords} item records without handbook prices.",
                    Metric = audit.UnknownPriceItemRecords,
                });
            }

            var rawBaseline = audit.Restartable && benchmark.VanillaRestartableMedianHandbookValue > 0
                ? benchmark.VanillaRestartableMedianHandbookValue
                : benchmark.VanillaMedianHandbookValue;
            var rawMultiple = audit.Restartable
                ? policy.RestartableRewardVsVanillaMedianWarnMultiple
                : policy.QuestRewardVsVanillaMedianWarnMultiple;
            if (rawBaseline > 0 && audit.KnownHandbookValue > rawBaseline * rawMultiple)
            {
                findings.Add(new AuditFinding
                {
                    Severity = audit.Restartable ? "Error" : "Warning",
                    Code = audit.Restartable ? "RESTARTABLE_REWARD_VALUE_OUTLIER" : "QUEST_REWARD_VALUE_OUTLIER",
                    SubjectType = "Quest",
                    SubjectId = audit.QuestId,
                    Detail = $"Known handbook reward value {audit.KnownHandbookValue:0.##} exceeds the vanilla median benchmark threshold.",
                    Metric = audit.KnownHandbookValue,
                    Threshold = Math.Round(rawBaseline * rawMultiple, 2),
                });
            }

            var normalizedBaseline = audit.Restartable && benchmark.VanillaRestartableMedianNormalizedHandbookValue > 0
                ? benchmark.VanillaRestartableMedianNormalizedHandbookValue
                : benchmark.VanillaMedianNormalizedHandbookValue;
            var normalizedMultiple = audit.Restartable
                ? policy.RestartableNormalizedRewardVsVanillaMedianWarnMultiple
                : policy.NormalizedRewardVsVanillaMedianWarnMultiple;
            if (normalizedBaseline > 0 && audit.NormalizedHandbookValue > normalizedBaseline * normalizedMultiple)
            {
                findings.Add(new AuditFinding
                {
                    Severity = audit.Restartable ? "Error" : "Warning",
                    Code = audit.Restartable ? "RESTARTABLE_REWARD_BUDGET_OUTLIER" : "QUEST_REWARD_BUDGET_OUTLIER",
                    SubjectType = "Quest",
                    SubjectId = audit.QuestId,
                    Detail = $"Progression-normalized reward value {audit.NormalizedHandbookValue:0.##} exceeds the vanilla normalized median threshold.",
                    Metric = audit.NormalizedHandbookValue,
                    Threshold = Math.Round(normalizedBaseline * normalizedMultiple, 2),
                });
            }
        }
    }

    private static MutableAcquisition GetOrCreate(Dictionary<string, MutableAcquisition> acquisitions, string templateId)
    {
        if (!acquisitions.TryGetValue(templateId, out var acquisition))
        {
            acquisition = new MutableAcquisition();
            acquisitions.Add(templateId, acquisition);
        }
        return acquisition;
    }

    private static ItemAcquisitionReport FinalizeItem(string templateId, MutableAcquisition acquisition, EconomyConfig config)
    {
        config.ManualOverrides.TryGetValue(templateId, out var manualOverride);
        var sourceCount = acquisition.TraderSources.Count + acquisition.QuestRewardSources.Count;
        return new ItemAcquisitionReport
        {
            TemplateId = templateId,
            Rarity = manualOverride?.Rarity ?? ClassifyRarity(sourceCount, config.Rarity),
            Ignored = manualOverride?.Ignore ?? false,
            OverrideNote = manualOverride?.Note,
            TraderSources = acquisition.TraderSources.OrderBy(value => value, StringComparer.Ordinal).ToList(),
            QuestRewardSources = acquisition.QuestRewardSources.OrderBy(value => value, StringComparer.Ordinal).ToList(),
        };
    }

    private static string ClassifyRarity(int sourceCount, RarityThresholds thresholds)
    {
        if (sourceCount >= thresholds.CommonMinSources) return "Common";
        if (sourceCount >= thresholds.UncommonMinSources) return "Uncommon";
        if (sourceCount >= thresholds.RareMinSources) return "Rare";
        return "Exceptional";
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

    private static double CalculateProgressionScore(int requiredLevel, int objectiveConditionCount, AuditPolicy policy)
    {
        var levelContribution = Math.Min(
            Math.Max(0, requiredLevel - 1) * Math.Max(0, policy.LevelGateWeight),
            Math.Max(0, policy.MaxLevelGateContribution)
        );
        var objectiveContribution = Math.Min(
            Math.Max(1, objectiveConditionCount) * Math.Max(0, policy.ObjectiveConditionWeight),
            Math.Max(0, policy.MaxObjectiveContribution)
        );
        return 1d + levelContribution + objectiveContribution;
    }

    private static double Normalize(double value, int requiredLevel, int objectiveCount, AuditPolicy policy)
    {
        var levelGate = Math.Min(policy.MaxLevelGateContribution, Math.Max(0, requiredLevel - 1) * policy.LevelGateWeight);
        var objectives = Math.Min(policy.MaxObjectiveContribution, Math.Max(0, objectiveCount) * policy.ObjectiveConditionWeight);
        var score = 1d + levelGate + objectives;
        return score > 0 ? value / score : value;
    }

    private static List<double> Positive(IEnumerable<double> values) => values.Where(value => value > 0).OrderBy(value => value).ToList();

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

    private static string SafePath(string modPath, string relativePath)
    {
        var root = Path.GetFullPath(modPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(modPath, relativePath));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Economy Admiral report path must stay inside the mod directory.");
        return path;
    }

    private sealed class MutableAcquisition
    {
        public HashSet<string> TraderSources { get; } = new(StringComparer.Ordinal);
        public HashSet<string> QuestRewardSources { get; } = new(StringComparer.Ordinal);
    }
}

public sealed record EconomyAuditReport
{
    public required int SchemaVersion { get; init; }
    public required string Mode { get; init; }
    public required string Preset { get; init; }
    public required bool EnforcementApplied { get; init; }
    public required bool RepeatedRaidLootDecay { get; init; }
    public required string RewardNormalizationModel { get; init; }
    public required AuditPolicy Policy { get; init; }
    public required DatabaseSummary Database { get; init; }
    public required AcquisitionSummary Acquisition { get; init; }
    public required QuestRewardBenchmark VanillaQuestRewardBenchmark { get; init; }
    public required List<TraderAudit> TraderAudits { get; init; }
    public required List<QuestRewardAudit> QuestRewardAudits { get; init; }
    public required List<AuditFinding> Findings { get; init; }
    public required List<ItemAcquisitionReport> Items { get; init; }
}

public sealed record DatabaseSummary
{
    public required int TemplateItems { get; init; }
    public required int HandbookItemsWithPrice { get; init; }
    public required int Quests { get; init; }
    public required int Traders { get; init; }
    public required int TraderAssortRecords { get; init; }
}

public sealed record AcquisitionSummary
{
    public required int ItemsWithKnownAcquisition { get; init; }
    public required int TraderSourceEdges { get; init; }
    public required int QuestRewardSourceEdges { get; init; }
}

public sealed record QuestRewardBenchmark
{
    public required int VanillaQuestSamples { get; init; }
    public required double VanillaMedianHandbookValue { get; init; }
    public required double VanillaP90HandbookValue { get; init; }
    public required double VanillaMedianNormalizedHandbookValue { get; init; }
    public required double VanillaP90NormalizedHandbookValue { get; init; }
    public required int VanillaRestartableSamples { get; init; }
    public required double VanillaRestartableMedianHandbookValue { get; init; }
    public required double VanillaRestartableMedianNormalizedHandbookValue { get; init; }
}

public sealed record TraderAudit
{
    public required string TraderId { get; init; }
    public required string TraderName { get; init; }
    public required int RootOffers { get; init; }
    public required int MissingBarterSchemes { get; init; }
    public required int MissingLoyaltyMappings { get; init; }
}

public sealed record QuestRewardAudit
{
    public required string QuestId { get; init; }
    public required string QuestName { get; init; }
    public required string TraderId { get; init; }
    public required bool IsVanillaTraderQuest { get; init; }
    public required bool Restartable { get; init; }
    public required int RequiredLevel { get; init; }
    public required int ObjectiveConditionCount { get; init; }
    public required double ProgressionScore { get; init; }
    public required int RewardItemRecords { get; init; }
    public required int DistinctRewardTemplates { get; init; }
    public required double KnownHandbookValue { get; init; }
    public required double NormalizedHandbookValue { get; init; }
    public required int UnknownPriceItemRecords { get; init; }
}

public sealed record AuditFinding
{
    public required string Severity { get; init; }
    public required string Code { get; init; }
    public required string SubjectType { get; init; }
    public required string SubjectId { get; init; }
    public required string Detail { get; init; }
    public double? Metric { get; init; }
    public double? Threshold { get; init; }
}

public sealed record ItemAcquisitionReport
{
    public required string TemplateId { get; init; }
    public required string Rarity { get; init; }
    public required bool Ignored { get; init; }
    public string? OverrideNote { get; init; }
    public required List<string> TraderSources { get; init; }
    public required List<string> QuestRewardSources { get; init; }
}