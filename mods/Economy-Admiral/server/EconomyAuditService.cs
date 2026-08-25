using System.Text.Json;
using Path = System.IO.Path;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTEconomy;

[Injectable]
public sealed class EconomyAuditService(
    TemplateTable templates,
    TradersTable traders,
    ModHelper modHelper,
    ISptLogger<EconomyAuditService> logger
)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly HashSet<string> VanillaTraderIds = new(StringComparer.Ordinal)
    {
        "54cb50c76803fa8b248b4571", // Prapor
        "54cb57776803fa99248b456e", // Therapist
        "579dc571d53a0658a154fbec", // Fence
        "58330581ace78e27b8b10cee", // Skier
        "5935c25fb3acc3127c3d8cd9", // Peacekeeper
        "5a7c2eca46aef81a7ca2145d", // Mechanic
        "5ac3b934156ae10c4430e83c", // Ragman
        "5c0647fdd443bc2504c2d371", // Jaeger
        "638f541a29ffd1183d187f57", // Lightkeeper
        "6617beeaa9cfa777ca915b7c", // Ref
    };

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(EconomyAuditService).Assembly);
        var config = await LoadConfigAsync(modPath, cancellationToken);

        if (config.Mode == EconomyMode.Off)
        {
            logger.Info("[SPT Economy] mode=Off; final DB audit skipped");
            return;
        }

        if (config.Mode == EconomyMode.Enforce)
        {
            logger.Warning("[SPT Economy] mode=Enforce requested, but enforcement is not implemented in this slice; running read-only audit only");
        }

        var policy = ResolvePolicy(config);
        var handbookPrices = templates.Handbook.Items
            .Where(item => item.Price is > 0)
            .GroupBy(item => item.Id.ToString(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Price!.Value, StringComparer.Ordinal);

        var findings = new List<AuditFinding>();
        var acquisitions = new Dictionary<string, MutableAcquisition>(StringComparer.Ordinal);
        var traderAudits = ScanTraderAcquisition(acquisitions, findings);
        var questAudits = ScanQuestRewards(acquisitions, handbookPrices, policy);
        var benchmark = BuildVanillaBenchmark(questAudits);
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

        var reportPath = Path.GetFullPath(Path.Combine(modPath, config.ReportRelativePath));
        var modRoot = Path.GetFullPath(modPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!reportPath.StartsWith(modRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SPT Economy report path must stay inside the mod directory.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions), cancellationToken);

        logger.Info($"[SPT Economy] final DB audit complete: {report.Database.TemplateItems} templates, {report.Database.Traders} traders, {report.Database.Quests} quests, {items.Count} items with trader/quest acquisition, {report.Findings.Count} findings; report={reportPath}");
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
                {
                    GetOrCreate(acquisitions, templateId).TraderSources.Add(traderId);
                }

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
        AuditPolicy policy
    )
    {
        var audits = new List<QuestRewardAudit>();

        foreach (var questPair in templates.Quests.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
        {
            var quest = questPair.Value;
            var questId = questPair.Key.ToString();
            var traderId = quest.TraderId.ToString();
            var rewardItems = new List<RewardItemValue>();

            if (quest.Rewards is not null)
            {
                var rewards = JsonSerializer.SerializeToElement(quest.Rewards);
                rewardItems.AddRange(FindRewardItems(rewards));
            }

            foreach (var templateId in rewardItems.Select(item => item.TemplateId).Distinct(StringComparer.Ordinal))
            {
                GetOrCreate(acquisitions, templateId).QuestRewardSources.Add(questId);
            }

            var knownValue = 0d;
            var unknownPriceItems = 0;
            foreach (var rewardItem in rewardItems)
            {
                if (handbookPrices.TryGetValue(rewardItem.TemplateId, out var price))
                {
                    knownValue += price * rewardItem.Count;
                }
                else
                {
                    unknownPriceItems++;
                }
            }

            var requiredLevel = ExtractRequiredLevel(quest.Conditions.AvailableForStart);
            var objectiveConditionCount = (quest.Conditions.AvailableForFinish?.Count ?? 0) + (quest.Conditions.Success?.Count ?? 0);
            var progressionScore = CalculateProgressionScore(requiredLevel, objectiveConditionCount, policy);
            var normalizedValue = progressionScore > 0 ? knownValue / progressionScore : knownValue;

            audits.Add(new QuestRewardAudit
            {
                QuestId = questId,
                QuestName = quest.QuestName ?? quest.Name,
                TraderId = traderId,
                IsVanillaTraderQuest = VanillaTraderIds.Contains(traderId),
                Restartable = quest.Restartable,
                RequiredLevel = requiredLevel,
                ObjectiveConditionCount = objectiveConditionCount,
                ProgressionScore = Math.Round(progressionScore, 4),
                RewardItemRecords = rewardItems.Count,
                DistinctRewardTemplates = rewardItems.Select(item => item.TemplateId).Distinct(StringComparer.Ordinal).Count(),
                KnownHandbookValue = Math.Round(knownValue, 2),
                NormalizedHandbookValue = Math.Round(normalizedValue, 2),
                UnknownPriceItemRecords = unknownPriceItems,
            });
        }

        return audits;
    }

    private static QuestRewardBenchmark BuildVanillaBenchmark(IReadOnlyCollection<QuestRewardAudit> questAudits)
    {
        var values = questAudits
            .Where(audit => audit.IsVanillaTraderQuest && !audit.Restartable && audit.KnownHandbookValue > 0)
            .Select(audit => audit.KnownHandbookValue)
            .OrderBy(value => value)
            .ToList();
        var normalizedValues = questAudits
            .Where(audit => audit.IsVanillaTraderQuest && !audit.Restartable && audit.NormalizedHandbookValue > 0)
            .Select(audit => audit.NormalizedHandbookValue)
            .OrderBy(value => value)
            .ToList();
        var restartableValues = questAudits
            .Where(audit => audit.IsVanillaTraderQuest && audit.Restartable && audit.KnownHandbookValue > 0)
            .Select(audit => audit.KnownHandbookValue)
            .OrderBy(value => value)
            .ToList();
        var restartableNormalizedValues = questAudits
            .Where(audit => audit.IsVanillaTraderQuest && audit.Restartable && audit.NormalizedHandbookValue > 0)
            .Select(audit => audit.NormalizedHandbookValue)
            .OrderBy(value => value)
            .ToList();

        return new QuestRewardBenchmark
        {
            VanillaQuestSamples = values.Count,
            VanillaMedianHandbookValue = Percentile(values, 0.50),
            VanillaP90HandbookValue = Percentile(values, 0.90),
            VanillaMedianNormalizedHandbookValue = Percentile(normalizedValues, 0.50),
            VanillaP90NormalizedHandbookValue = Percentile(normalizedValues, 0.90),
            VanillaRestartableSamples = restartableValues.Count,
            VanillaRestartableMedianHandbookValue = Percentile(restartableValues, 0.50),
            VanillaRestartableMedianNormalizedHandbookValue = Percentile(restartableNormalizedValues, 0.50),
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

            AddRawRewardFinding(audit, benchmark, policy, findings);
            AddNormalizedRewardFinding(audit, benchmark, policy, findings);
        }
    }

    private static void AddRawRewardFinding(
        QuestRewardAudit audit,
        QuestRewardBenchmark benchmark,
        AuditPolicy policy,
        List<AuditFinding> findings
    )
    {
        var baseline = audit.Restartable && benchmark.VanillaRestartableMedianHandbookValue > 0
            ? benchmark.VanillaRestartableMedianHandbookValue
            : benchmark.VanillaMedianHandbookValue;
        if (baseline <= 0 || audit.KnownHandbookValue <= 0)
        {
            return;
        }

        var multiple = audit.Restartable
            ? policy.RestartableRewardVsVanillaMedianWarnMultiple
            : policy.QuestRewardVsVanillaMedianWarnMultiple;
        var threshold = baseline * multiple;
        if (audit.KnownHandbookValue <= threshold)
        {
            return;
        }

        findings.Add(new AuditFinding
        {
            Severity = audit.Restartable ? "Error" : "Warning",
            Code = audit.Restartable ? "RESTARTABLE_REWARD_VALUE_OUTLIER" : "QUEST_REWARD_VALUE_OUTLIER",
            SubjectType = "Quest",
            SubjectId = audit.QuestId,
            Detail = $"Known handbook reward value {audit.KnownHandbookValue:0.##} exceeds the vanilla median benchmark threshold.",
            Metric = audit.KnownHandbookValue,
            Threshold = Math.Round(threshold, 2),
        });
    }

    private static void AddNormalizedRewardFinding(
        QuestRewardAudit audit,
        QuestRewardBenchmark benchmark,
        AuditPolicy policy,
        List<AuditFinding> findings
    )
    {
        var baseline = audit.Restartable && benchmark.VanillaRestartableMedianNormalizedHandbookValue > 0
            ? benchmark.VanillaRestartableMedianNormalizedHandbookValue
            : benchmark.VanillaMedianNormalizedHandbookValue;
        if (baseline <= 0 || audit.NormalizedHandbookValue <= 0)
        {
            return;
        }

        var multiple = audit.Restartable
            ? policy.RestartableNormalizedRewardVsVanillaMedianWarnMultiple
            : policy.NormalizedRewardVsVanillaMedianWarnMultiple;
        var threshold = baseline * multiple;
        if (audit.NormalizedHandbookValue <= threshold)
        {
            return;
        }

        findings.Add(new AuditFinding
        {
            Severity = audit.Restartable ? "Error" : "Warning",
            Code = audit.Restartable ? "RESTARTABLE_REWARD_BUDGET_OUTLIER" : "QUEST_REWARD_BUDGET_OUTLIER",
            SubjectType = "Quest",
            SubjectId = audit.QuestId,
            Detail = $"Progression-normalized reward value {audit.NormalizedHandbookValue:0.##} exceeds the vanilla normalized median threshold.",
            Metric = audit.NormalizedHandbookValue,
            Threshold = Math.Round(threshold, 2),
        });
    }

    private static int ExtractRequiredLevel(IEnumerable<QuestCondition>? conditions)
    {
        if (conditions is null)
        {
            return 1;
        }

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

    private static IEnumerable<RewardItemValue> FindRewardItems(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("_tpl", out var tpl) && tpl.ValueKind == JsonValueKind.String)
                {
                    var templateId = tpl.GetString();
                    if (!string.IsNullOrWhiteSpace(templateId))
                    {
                        yield return new RewardItemValue(templateId, FindStackCount(element));
                    }
                }

                foreach (var property in element.EnumerateObject())
                {
                    foreach (var nested in FindRewardItems(property.Value))
                    {
                        yield return nested;
                    }
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var nested in FindRewardItems(item))
                    {
                        yield return nested;
                    }
                }
                break;
        }
    }

    private static double FindStackCount(JsonElement item)
    {
        if (!item.TryGetProperty("upd", out var upd) || upd.ValueKind != JsonValueKind.Object)
        {
            return 1d;
        }

        if (TryGetNumber(upd, "StackObjectsCount", out var count) || TryGetNumber(upd, "stackObjectsCount", out count))
        {
            return Math.Max(1d, count);
        }

        return 1d;
    }

    private static bool TryGetNumber(JsonElement element, string propertyName, out double value)
    {
        value = 0;
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetDouble(out value);
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
        if (sourceCount >= thresholds.CommonMinSources)
        {
            return "Common";
        }

        if (sourceCount >= thresholds.UncommonMinSources)
        {
            return "Uncommon";
        }

        if (sourceCount >= thresholds.RareMinSources)
        {
            return "Rare";
        }

        return "Exceptional";
    }

    private static AuditPolicy ResolvePolicy(EconomyConfig config)
    {
        return config.Preset switch
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
    }

    private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
        {
            return 0;
        }

        if (sortedValues.Count == 1)
        {
            return Math.Round(sortedValues[0], 2);
        }

        var position = (sortedValues.Count - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return Math.Round(sortedValues[lower], 2);
        }

        var fraction = position - lower;
        return Math.Round(sortedValues[lower] + ((sortedValues[upper] - sortedValues[lower]) * fraction), 2);
    }

    private static async Task<EconomyConfig> LoadConfigAsync(string modPath, CancellationToken cancellationToken)
    {
        var configPath = Path.Combine(modPath, "config", "config.json");
        if (!File.Exists(configPath))
        {
            return new EconomyConfig();
        }

        await using var stream = File.OpenRead(configPath);
        return await JsonSerializer.DeserializeAsync<EconomyConfig>(stream, JsonOptions, cancellationToken)
            ?? new EconomyConfig();
    }

    private sealed class MutableAcquisition
    {
        public HashSet<string> TraderSources { get; } = new(StringComparer.Ordinal);
        public HashSet<string> QuestRewardSources { get; } = new(StringComparer.Ordinal);
    }

    private sealed record RewardItemValue(string TemplateId, double Count);
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
