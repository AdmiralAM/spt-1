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
public sealed class RewardUtilityAuditService(
    TemplateTable templates,
    ModHelper modHelper,
    ISptLogger<RewardUtilityAuditService> logger
)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static readonly HashSet<string> VanillaTraderIds = new(StringComparer.Ordinal)
    {
        "54cb50c76803fa8b248b4571",
        "54cb57776803fa99248b456e",
        "579dc571d53a0658a154fbec",
        "58330581ace78e27b8b10cee",
        "5935c25fb3acc3127c3d8cd9",
        "5a7c2eca46aef81a7ca2145d",
        "5ac3b934156ae10c4430e83c",
        "5c0647fdd443bc2504c2d371",
        "638f541a29ffd1183d187f57",
        "6617beeaa9cfa777ca915b7c",
    };

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var rows = templates.Quests
            .OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
            .Select(pair => BuildQuestRow(pair.Key.ToString(), pair.Value))
            .ToList();

        var vanilla = rows.Where(row => row.IsVanillaTraderQuest && !row.Restartable).ToList();
        var vanillaRestartable = rows.Where(row => row.IsVanillaTraderQuest && row.Restartable).ToList();

        var report = new RewardUtilityAuditReport
        {
            SchemaVersion = 1,
            UtilityScoringApplied = false,
            Note = "Typed SPT 4.1 reward inventory/benchmark only. XP, standing and unlocks are not converted into ruble value in this slice.",
            Vanilla = BuildBenchmark(vanilla),
            VanillaRestartable = BuildBenchmark(vanillaRestartable),
            Quests = rows,
        };

        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(RewardUtilityAuditService).Assembly);
        var reportPath = Path.GetFullPath(Path.Combine(modPath, "reports", "economy-admiral-reward-utility.json"));
        var modRoot = Path.GetFullPath(modPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!reportPath.StartsWith(modRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Economy Admiral reward utility report path must stay inside the mod directory.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        logger.Info($"[Economy Admiral] typed reward utility audit complete: {rows.Count} quests; report={reportPath}");
    }

    private static QuestRewardUtilityRow BuildQuestRow(string questId, Quest quest)
    {
        var successRewards = quest.Rewards is null
            ? []
            : quest.Rewards
                .Where(pair => string.Equals(pair.Key, "Success", StringComparison.OrdinalIgnoreCase))
                .SelectMany(pair => pair.Value)
                .ToList();

        var experience = successRewards
            .Where(reward => reward.Type == RewardType.Experience)
            .Sum(reward => reward.Value ?? 0d);
        var standing = successRewards
            .Where(reward => reward.Type == RewardType.TraderStanding)
            .Sum(reward => reward.Value ?? 0d);
        var traderUnlocks = CountDistinctTargets(successRewards, RewardType.TraderUnlock);
        var assortmentUnlocks = CountDistinctTargets(successRewards, RewardType.AssortmentUnlock);
        var productionUnlocks = CountDistinctTargets(successRewards, RewardType.ProductionScheme);

        return new QuestRewardUtilityRow
        {
            QuestId = questId,
            QuestName = quest.QuestName ?? quest.Name,
            TraderId = quest.TraderId.ToString(),
            IsVanillaTraderQuest = VanillaTraderIds.Contains(quest.TraderId.ToString()),
            Restartable = quest.Restartable,
            SuccessRewardRecords = successRewards.Count,
            Experience = Math.Round(experience, 2),
            TraderStanding = Math.Round(standing, 4),
            TraderUnlocks = traderUnlocks,
            AssortmentUnlocks = assortmentUnlocks,
            ProductionSchemeUnlocks = productionUnlocks,
        };
    }

    private static int CountDistinctTargets(IEnumerable<Reward> rewards, RewardType type)
    {
        return rewards
            .Where(reward => reward.Type == type && !string.IsNullOrWhiteSpace(reward.Target))
            .Select(reward => reward.Target!)
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    private static RewardUtilityBenchmark BuildBenchmark(IReadOnlyCollection<QuestRewardUtilityRow> rows)
    {
        var xp = rows.Where(row => row.Experience > 0).Select(row => row.Experience).OrderBy(value => value).ToList();
        var standing = rows.Where(row => row.TraderStanding != 0).Select(row => Math.Abs(row.TraderStanding)).OrderBy(value => value).ToList();
        var traderUnlocks = rows.Select(row => (double)row.TraderUnlocks).OrderBy(value => value).ToList();
        var assortmentUnlocks = rows.Select(row => (double)row.AssortmentUnlocks).OrderBy(value => value).ToList();
        var productionUnlocks = rows.Select(row => (double)row.ProductionSchemeUnlocks).OrderBy(value => value).ToList();

        return new RewardUtilityBenchmark
        {
            QuestSamples = rows.Count,
            XpSamples = xp.Count,
            MedianXp = Percentile(xp, 0.50),
            P90Xp = Percentile(xp, 0.90),
            StandingSamples = standing.Count,
            MedianAbsoluteStanding = Percentile(standing, 0.50),
            P90AbsoluteStanding = Percentile(standing, 0.90),
            MedianTraderUnlocks = Percentile(traderUnlocks, 0.50),
            P90TraderUnlocks = Percentile(traderUnlocks, 0.90),
            MedianAssortmentUnlocks = Percentile(assortmentUnlocks, 0.50),
            P90AssortmentUnlocks = Percentile(assortmentUnlocks, 0.90),
            MedianProductionSchemeUnlocks = Percentile(productionUnlocks, 0.50),
            P90ProductionSchemeUnlocks = Percentile(productionUnlocks, 0.90),
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
            return Math.Round(sortedValues[0], 4);
        }

        var position = (sortedValues.Count - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return Math.Round(sortedValues[lower], 4);
        }

        var fraction = position - lower;
        return Math.Round(sortedValues[lower] + ((sortedValues[upper] - sortedValues[lower]) * fraction), 4);
    }
}

public sealed record RewardUtilityAuditReport
{
    public required int SchemaVersion { get; init; }
    public required bool UtilityScoringApplied { get; init; }
    public required string Note { get; init; }
    public required RewardUtilityBenchmark Vanilla { get; init; }
    public required RewardUtilityBenchmark VanillaRestartable { get; init; }
    public required List<QuestRewardUtilityRow> Quests { get; init; }
}

public sealed record RewardUtilityBenchmark
{
    public required int QuestSamples { get; init; }
    public required int XpSamples { get; init; }
    public required double MedianXp { get; init; }
    public required double P90Xp { get; init; }
    public required int StandingSamples { get; init; }
    public required double MedianAbsoluteStanding { get; init; }
    public required double P90AbsoluteStanding { get; init; }
    public required double MedianTraderUnlocks { get; init; }
    public required double P90TraderUnlocks { get; init; }
    public required double MedianAssortmentUnlocks { get; init; }
    public required double P90AssortmentUnlocks { get; init; }
    public required double MedianProductionSchemeUnlocks { get; init; }
    public required double P90ProductionSchemeUnlocks { get; init; }
}

public sealed record QuestRewardUtilityRow
{
    public required string QuestId { get; init; }
    public required string QuestName { get; init; }
    public required string TraderId { get; init; }
    public required bool IsVanillaTraderQuest { get; init; }
    public required bool Restartable { get; init; }
    public required int SuccessRewardRecords { get; init; }
    public required double Experience { get; init; }
    public required double TraderStanding { get; init; }
    public required int TraderUnlocks { get; init; }
    public required int AssortmentUnlocks { get; init; }
    public required int ProductionSchemeUnlocks { get; init; }
}
