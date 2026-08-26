using System.Text.Json;
using Path = System.IO.Path;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTEconomy;

[Injectable]
public sealed class QuestAnalysisService(
    TemplateTable templates,
    EconomyRuntimeConfigService runtimeConfigService,
    ModHelper modHelper,
    ISptLogger<QuestAnalysisService> logger
)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly HashSet<string> VanillaTraderIds = new(StringComparer.Ordinal)
    {
        "54cb50c76803fa8b248b4571", "54cb57776803fa99248b456e", "579dc571d53a0658a154fbec",
        "58330581ace78e27b8b10cee", "5935c25fb3acc3127c3d8cd9", "5a7c2eca46aef81a7ca2145d",
        "5ac3b934156ae10c4430e83c", "5c0647fdd443bc2504c2d371", "638f541a29ffd1183d187f57",
        "6617beeaa9cfa777ca915b7c",
    };

    public async Task<QuestAnalysisReport> RunAsync(QuestProgressionSnapshot progressionSnapshot, CancellationToken cancellationToken)
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(QuestAnalysisService).Assembly);
        var config = await runtimeConfigService.GetAsync(cancellationToken);

        var policy = ResolvePolicy(config);
        var handbookPrices = templates.Handbook.Items
            .Where(item => item.Price is > 0)
            .GroupBy(item => item.Id.ToString(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Price!.Value, StringComparer.Ordinal);

        var progression = progressionSnapshot.Quests
            .ToDictionary(row => row.QuestId, StringComparer.Ordinal);

        var rawRows = templates.Quests
            .OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
            .Select(pair => BuildRow(pair.Key.ToString(), pair.Value, handbookPrices, progression))
            .ToList();

        var vanilla = BuildBaseline(rawRows.Where(row => row.IsVanillaTraderQuest && !row.Restartable).ToList());
        var vanillaRestartable = BuildBaseline(rawRows.Where(row => row.IsVanillaTraderQuest && row.Restartable).ToList());
        var rows = rawRows
            .Select(row => AddRelativeSignals(row, row.Restartable && vanillaRestartable.QuestSamples > 0 ? vanillaRestartable : vanilla))
            .Select(row => AddObservationalFlags(row, policy))
            .ToList();

        var flagCounts = rows
            .SelectMany(row => row.ObservationalFlags)
            .GroupBy(flag => flag, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var report = new QuestAnalysisReport
        {
            SchemaVersion = 3,
            Preset = config.Preset.ToString(),
            CompositeScoreApplied = false,
            RewardAllowanceAffected = false,
            OutlierFlagsAffectEnforcement = true,
            Policy = policy,
            FlagCounts = flagCounts,
            Note = "Unified quest analysis snapshot reused by preview and Enforce. XP/standing outlier flags can feed the active numeric reward policy; item rewards and structural dimensions remain non-mutating in Alpha.",
            Vanilla = vanilla,
            VanillaRestartable = vanillaRestartable,
            Quests = rows,
        };

        var reportPath = Path.GetFullPath(Path.Combine(modPath, "reports", "economy-admiral-quest-analysis.json"));
        var modRoot = Path.GetFullPath(modPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!reportPath.StartsWith(modRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Economy Admiral unified quest analysis report path must stay inside the mod directory.");

        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        logger.Info($"[Economy Admiral] unified quest analysis complete: {rows.Count} quests, {flagCounts.Values.Sum()} observational flags; report={reportPath}");
        return report;
    }

    private static QuestAnalysisRow AddObservationalFlags(QuestAnalysisRow row, AuditPolicy policy)
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
                HighItemValueLowStructureWarnMultiple = 4.0,
                HighXpLowDepthWarnMultiple = 4.0,
                HighStandingLowDepthWarnMultiple = 4.0,
                RestartableHighItemValueWarnMultiple = 3.0,
                RestartableHighXpWarnMultiple = 3.0,
                LowDepthMaxRelativeMultiple = 1.0,
                LowStructureMaxRelativeMultiple = 1.0,
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
    }

    private static QuestAnalysisRow BuildRow(
        string questId,
        Quest quest,
        IReadOnlyDictionary<string, double> handbookPrices,
        IReadOnlyDictionary<string, QuestProgressionGraphRow> progression)
    {
        var successRewards = quest.Rewards is null
            ? []
            : quest.Rewards.Where(pair => string.Equals(pair.Key, "Success", StringComparison.OrdinalIgnoreCase)).SelectMany(pair => pair.Value).ToList();

        var rewardItems = FindRewardItems(JsonSerializer.SerializeToElement(successRewards)).ToList();
        var knownValue = rewardItems.Sum(item => handbookPrices.TryGetValue(item.TemplateId, out var price) ? price * item.Count : 0d);
        var unknownPriceItems = rewardItems.Count(item => !handbookPrices.ContainsKey(item.TemplateId));

        var experience = successRewards.Where(reward => reward.Type == RewardType.Experience).Sum(reward => reward.Value ?? 0d);
        var standing = successRewards.Where(reward => reward.Type == RewardType.TraderStanding).Sum(reward => reward.Value ?? 0d);
        var traderUnlocks = CountTargets(successRewards, RewardType.TraderUnlock);
        var assortmentUnlocks = CountTargets(successRewards, RewardType.AssortmentUnlock);
        var productionUnlocks = CountTargets(successRewards, RewardType.ProductionScheme);

        var objectiveConditions = EnumerateObjectiveConditions(quest).ToList();
        var counters = objectiveConditions.Where(condition => condition.Counter?.Conditions is not null).SelectMany(condition => condition.Counter!.Conditions!).ToList();
        var timed = objectiveConditions.Count(condition => condition.CompleteInSeconds is > 0) + counters.Count(condition => condition.CompleteInSeconds is > 0);
        var oneSession = objectiveConditions.Count(condition => condition.OneSessionOnly == true) + counters.Count(condition => condition.ResetOnSessionEnd == true);
        var fir = objectiveConditions.Count(condition => condition.OnlyFoundInRaid == true);
        var plant = objectiveConditions.Count(condition => condition.PlantTime is > 0);
        var distance = counters.Count(condition => condition.Distance?.Value is > 0);
        var daytime = counters.Count(condition => condition.Daytime is not null);

        progression.TryGetValue(questId, out var graph);
        var traderId = quest.TraderId.ToString();
        return new QuestAnalysisRow
        {
            QuestId = questId,
            QuestName = quest.QuestName ?? quest.Name,
            TraderId = traderId,
            IsVanillaTraderQuest = VanillaTraderIds.Contains(traderId),
            Restartable = quest.Restartable,
            SuccessKnownHandbookValue = Math.Round(knownValue, 2),
            UnknownPriceRewardItemRecords = unknownPriceItems,
            Experience = Math.Round(experience, 2),
            TraderStanding = Math.Round(standing, 4),
            TraderUnlocks = traderUnlocks,
            AssortmentUnlocks = assortmentUnlocks,
            ProductionSchemeUnlocks = productionUnlocks,
            DirectPrerequisiteCount = graph?.DirectPrerequisiteCount ?? 0,
            MaximumPrerequisiteDepth = graph?.MaximumPrerequisiteDepth ?? 0,
            IsPrerequisiteCycleMember = graph?.IsCycleMember ?? false,
            ObjectiveConditionCount = objectiveConditions.Count,
            StructuredConstraintCount = timed + oneSession + fir + plant + distance + daytime,
            TimedConditionCount = timed,
            OneSessionConditionCount = oneSession,
            FoundInRaidConditionCount = fir,
            PlantConditionCount = plant,
            DistanceConstraintCount = distance,
            DaytimeConstraintCount = daytime,
            ObservationalFlags = [],
        };
    }

    private static QuestAnalysisBaseline BuildBaseline(IReadOnlyCollection<QuestAnalysisRow> rows)
    {
        return new QuestAnalysisBaseline
        {
            QuestSamples = rows.Count,
            MedianSuccessHandbookValue = MedianPositive(rows.Select(row => row.SuccessKnownHandbookValue)),
            MedianXp = MedianPositive(rows.Select(row => row.Experience)),
            MedianAbsoluteStanding = MedianPositive(rows.Select(row => Math.Abs(row.TraderStanding))),
            MedianPrerequisiteDepth = MedianPositive(rows.Where(row => !row.IsPrerequisiteCycleMember).Select(row => (double)row.MaximumPrerequisiteDepth)),
            MedianStructuredConstraintCount = MedianPositive(rows.Select(row => (double)row.StructuredConstraintCount)),
        };
    }

    private static QuestAnalysisRow AddRelativeSignals(QuestAnalysisRow row, QuestAnalysisBaseline baseline)
    {
        return row with
        {
            HandbookValueVsVanillaMedian = Ratio(row.SuccessKnownHandbookValue, baseline.MedianSuccessHandbookValue),
            XpVsVanillaMedian = Ratio(row.Experience, baseline.MedianXp),
            StandingVsVanillaMedian = Ratio(Math.Abs(row.TraderStanding), baseline.MedianAbsoluteStanding),
            PrerequisiteDepthVsVanillaMedian = Ratio(row.MaximumPrerequisiteDepth, baseline.MedianPrerequisiteDepth),
            StructuredConstraintsVsVanillaMedian = Ratio(row.StructuredConstraintCount, baseline.MedianStructuredConstraintCount),
        };
    }

    private static double MedianPositive(IEnumerable<double> values) => Percentile(values.Where(value => value > 0).OrderBy(value => value).ToList(), 0.50);
    private static double? Ratio(double value, double baseline) => value > 0 && baseline > 0 ? Math.Round(value / baseline, 4) : null;

    private static int CountTargets(IEnumerable<Reward> rewards, RewardType type) => rewards
        .Where(reward => reward.Type == type && !string.IsNullOrWhiteSpace(reward.Target))
        .Select(reward => reward.Target!)
        .Distinct(StringComparer.Ordinal)
        .Count();

    private static IEnumerable<QuestCondition> EnumerateObjectiveConditions(Quest quest)
    {
        if (quest.Conditions.AvailableForFinish is not null)
            foreach (var condition in quest.Conditions.AvailableForFinish) yield return condition;
        if (quest.Conditions.Success is not null)
            foreach (var condition in quest.Conditions.Success) yield return condition;
    }

    private static IEnumerable<RewardItemValue> FindRewardItems(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("_tpl", out var tpl) && tpl.ValueKind == JsonValueKind.String)
                {
                    var templateId = tpl.GetString();
                    if (!string.IsNullOrWhiteSpace(templateId)) yield return new RewardItemValue(templateId, FindStackCount(element));
                }
                foreach (var property in element.EnumerateObject())
                    foreach (var nested in FindRewardItems(property.Value)) yield return nested;
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    foreach (var nested in FindRewardItems(item)) yield return nested;
                break;
        }
    }

    private static double FindStackCount(JsonElement item)
    {
        if (!item.TryGetProperty("upd", out var upd) || upd.ValueKind != JsonValueKind.Object) return 1d;
        foreach (var key in new[] { "StackObjectsCount", "stackObjectsCount" })
            if (upd.TryGetProperty(key, out var count) && count.ValueKind == JsonValueKind.Number && count.TryGetDouble(out var value)) return Math.Max(1d, value);
        return 1d;
    }

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

    private sealed record RewardItemValue(string TemplateId, double Count);
}

public sealed record QuestAnalysisReport
{
    public required int SchemaVersion { get; init; }
    public required string Preset { get; init; }
    public required bool CompositeScoreApplied { get; init; }
    public required bool RewardAllowanceAffected { get; init; }
    public required bool OutlierFlagsAffectEnforcement { get; init; }
    public required AuditPolicy Policy { get; init; }
    public required Dictionary<string, int> FlagCounts { get; init; }
    public required string Note { get; init; }
    public required QuestAnalysisBaseline Vanilla { get; init; }
    public required QuestAnalysisBaseline VanillaRestartable { get; init; }
    public required List<QuestAnalysisRow> Quests { get; init; }
}

public sealed record QuestAnalysisBaseline
{
    public required int QuestSamples { get; init; }
    public required double MedianSuccessHandbookValue { get; init; }
    public required double MedianXp { get; init; }
    public required double MedianAbsoluteStanding { get; init; }
    public required double MedianPrerequisiteDepth { get; init; }
    public required double MedianStructuredConstraintCount { get; init; }
}

public sealed record QuestAnalysisRow
{
    public required string QuestId { get; init; }
    public required string QuestName { get; init; }
    public required string TraderId { get; init; }
    public required bool IsVanillaTraderQuest { get; init; }
    public required bool Restartable { get; init; }
    public required double SuccessKnownHandbookValue { get; init; }
    public required int UnknownPriceRewardItemRecords { get; init; }
    public required double Experience { get; init; }
    public required double TraderStanding { get; init; }
    public required int TraderUnlocks { get; init; }
    public required int AssortmentUnlocks { get; init; }
    public required int ProductionSchemeUnlocks { get; init; }
    public required int DirectPrerequisiteCount { get; init; }
    public required int MaximumPrerequisiteDepth { get; init; }
    public required bool IsPrerequisiteCycleMember { get; init; }
    public required int ObjectiveConditionCount { get; init; }
    public required int StructuredConstraintCount { get; init; }
    public required int TimedConditionCount { get; init; }
    public required int OneSessionConditionCount { get; init; }
    public required int FoundInRaidConditionCount { get; init; }
    public required int PlantConditionCount { get; init; }
    public required int DistanceConstraintCount { get; init; }
    public required int DaytimeConstraintCount { get; init; }
    public required List<string> ObservationalFlags { get; init; }
    public double? HandbookValueVsVanillaMedian { get; init; }
    public double? XpVsVanillaMedian { get; init; }
    public double? StandingVsVanillaMedian { get; init; }
    public double? PrerequisiteDepthVsVanillaMedian { get; init; }
    public double? StructuredConstraintsVsVanillaMedian { get; init; }
}
