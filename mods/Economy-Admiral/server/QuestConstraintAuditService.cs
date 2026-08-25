using System.Text.Json;
using Path = System.IO.Path;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTEconomy;

[Injectable]
public sealed class QuestConstraintAuditService(
    TemplateTable templates,
    ModHelper modHelper,
    ISptLogger<QuestConstraintAuditService> logger
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
            .Select(pair => BuildRow(pair.Key.ToString(), pair.Value))
            .ToList();

        var vanilla = rows.Where(row => row.IsVanillaTraderQuest && !row.Restartable).ToList();
        var vanillaRestartable = rows.Where(row => row.IsVanillaTraderQuest && row.Restartable).ToList();

        var report = new QuestConstraintAuditReport
        {
            SchemaVersion = 1,
            ConstraintsAffectRewardAllowance = false,
            Note = "Structured final-DB quest constraints only. No text interpretation and no reward multiplier is applied.",
            Vanilla = BuildBenchmark(vanilla),
            VanillaRestartable = BuildBenchmark(vanillaRestartable),
            Quests = rows,
        };

        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(QuestConstraintAuditService).Assembly);
        var reportPath = Path.GetFullPath(Path.Combine(modPath, "reports", "economy-admiral-quest-constraints.json"));
        var modRoot = Path.GetFullPath(modPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!reportPath.StartsWith(modRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Economy Admiral quest constraint report path must stay inside the mod directory.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        logger.Info($"[Economy Admiral] quest constraint audit complete: {rows.Count} quests; report={reportPath}");
    }

    private static QuestConstraintRow BuildRow(string questId, Quest quest)
    {
        var conditions = EnumerateObjectiveConditions(quest).ToList();
        var counterConditions = conditions
            .Where(condition => condition.Counter?.Conditions is not null)
            .SelectMany(condition => condition.Counter!.Conditions!)
            .ToList();

        var timed = conditions.Count(condition => condition.CompleteInSeconds is > 0)
            + counterConditions.Count(condition => condition.CompleteInSeconds is > 0);
        var oneSession = conditions.Count(condition => condition.OneSessionOnly == true)
            + counterConditions.Count(condition => condition.ResetOnSessionEnd == true);
        var fir = conditions.Count(condition => condition.OnlyFoundInRaid == true);
        var plant = conditions.Count(condition => condition.PlantTime is > 0);
        var distance = counterConditions.Count(condition => condition.Distance?.Value is > 0);
        var daytime = counterConditions.Count(condition => condition.Daytime is not null);
        var constrained = timed + oneSession + fir + plant + distance + daytime;

        var strictestTimeSeconds = conditions
            .Where(condition => condition.CompleteInSeconds is > 0)
            .Select(condition => condition.CompleteInSeconds!.Value)
            .Concat(counterConditions.Where(condition => condition.CompleteInSeconds is > 0).Select(condition => (double)condition.CompleteInSeconds!.Value))
            .DefaultIfEmpty(0)
            .Min();

        var longestDistance = counterConditions
            .Where(condition => condition.Distance?.Value is > 0)
            .Select(condition => condition.Distance!.Value!.Value)
            .DefaultIfEmpty(0)
            .Max();

        var traderId = quest.TraderId.ToString();
        return new QuestConstraintRow
        {
            QuestId = questId,
            QuestName = quest.QuestName ?? quest.Name,
            TraderId = traderId,
            IsVanillaTraderQuest = VanillaTraderIds.Contains(traderId),
            Restartable = quest.Restartable,
            ObjectiveConditionCount = conditions.Count,
            TimedConditionCount = timed,
            OneSessionConditionCount = oneSession,
            FoundInRaidConditionCount = fir,
            PlantConditionCount = plant,
            DistanceConstraintCount = distance,
            DaytimeConstraintCount = daytime,
            StructuredConstraintCount = constrained,
            StrictestCompletionTimeSeconds = Math.Round(strictestTimeSeconds, 2),
            LongestDistanceConstraint = Math.Round(longestDistance, 2),
        };
    }

    private static IEnumerable<QuestCondition> EnumerateObjectiveConditions(Quest quest)
    {
        if (quest.Conditions.AvailableForFinish is not null)
        {
            foreach (var condition in quest.Conditions.AvailableForFinish)
            {
                yield return condition;
            }
        }

        if (quest.Conditions.Success is not null)
        {
            foreach (var condition in quest.Conditions.Success)
            {
                yield return condition;
            }
        }
    }

    private static QuestConstraintBenchmark BuildBenchmark(IReadOnlyCollection<QuestConstraintRow> rows)
    {
        var constraintCounts = rows.Select(row => (double)row.StructuredConstraintCount).OrderBy(value => value).ToList();
        var timedCounts = rows.Select(row => (double)row.TimedConditionCount).OrderBy(value => value).ToList();
        var oneSessionCounts = rows.Select(row => (double)row.OneSessionConditionCount).OrderBy(value => value).ToList();
        var firCounts = rows.Select(row => (double)row.FoundInRaidConditionCount).OrderBy(value => value).ToList();
        var positiveTimes = rows.Where(row => row.StrictestCompletionTimeSeconds > 0).Select(row => row.StrictestCompletionTimeSeconds).OrderBy(value => value).ToList();
        var positiveDistances = rows.Where(row => row.LongestDistanceConstraint > 0).Select(row => row.LongestDistanceConstraint).OrderBy(value => value).ToList();

        return new QuestConstraintBenchmark
        {
            QuestSamples = rows.Count,
            MedianStructuredConstraintCount = Percentile(constraintCounts, 0.50),
            P90StructuredConstraintCount = Percentile(constraintCounts, 0.90),
            MedianTimedConditionCount = Percentile(timedCounts, 0.50),
            P90TimedConditionCount = Percentile(timedCounts, 0.90),
            MedianOneSessionConditionCount = Percentile(oneSessionCounts, 0.50),
            P90OneSessionConditionCount = Percentile(oneSessionCounts, 0.90),
            MedianFoundInRaidConditionCount = Percentile(firCounts, 0.50),
            P90FoundInRaidConditionCount = Percentile(firCounts, 0.90),
            TimedQuestSamples = positiveTimes.Count,
            MedianPositiveCompletionTimeSeconds = Percentile(positiveTimes, 0.50),
            P90PositiveCompletionTimeSeconds = Percentile(positiveTimes, 0.90),
            DistanceQuestSamples = positiveDistances.Count,
            MedianPositiveDistanceConstraint = Percentile(positiveDistances, 0.50),
            P90PositiveDistanceConstraint = Percentile(positiveDistances, 0.90),
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
}

public sealed record QuestConstraintAuditReport
{
    public required int SchemaVersion { get; init; }
    public required bool ConstraintsAffectRewardAllowance { get; init; }
    public required string Note { get; init; }
    public required QuestConstraintBenchmark Vanilla { get; init; }
    public required QuestConstraintBenchmark VanillaRestartable { get; init; }
    public required List<QuestConstraintRow> Quests { get; init; }
}

public sealed record QuestConstraintBenchmark
{
    public required int QuestSamples { get; init; }
    public required double MedianStructuredConstraintCount { get; init; }
    public required double P90StructuredConstraintCount { get; init; }
    public required double MedianTimedConditionCount { get; init; }
    public required double P90TimedConditionCount { get; init; }
    public required double MedianOneSessionConditionCount { get; init; }
    public required double P90OneSessionConditionCount { get; init; }
    public required double MedianFoundInRaidConditionCount { get; init; }
    public required double P90FoundInRaidConditionCount { get; init; }
    public required int TimedQuestSamples { get; init; }
    public required double MedianPositiveCompletionTimeSeconds { get; init; }
    public required double P90PositiveCompletionTimeSeconds { get; init; }
    public required int DistanceQuestSamples { get; init; }
    public required double MedianPositiveDistanceConstraint { get; init; }
    public required double P90PositiveDistanceConstraint { get; init; }
}

public sealed record QuestConstraintRow
{
    public required string QuestId { get; init; }
    public required string QuestName { get; init; }
    public required string TraderId { get; init; }
    public required bool IsVanillaTraderQuest { get; init; }
    public required bool Restartable { get; init; }
    public required int ObjectiveConditionCount { get; init; }
    public required int TimedConditionCount { get; init; }
    public required int OneSessionConditionCount { get; init; }
    public required int FoundInRaidConditionCount { get; init; }
    public required int PlantConditionCount { get; init; }
    public required int DistanceConstraintCount { get; init; }
    public required int DaytimeConstraintCount { get; init; }
    public required int StructuredConstraintCount { get; init; }
    public required double StrictestCompletionTimeSeconds { get; init; }
    public required double LongestDistanceConstraint { get; init; }
}
