using System.Text.Json;
using Path = System.IO.Path;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTEconomy;

[Injectable]
public sealed class QuestProgressionGraphService(
    TemplateTable templates,
    ModHelper modHelper,
    ISptLogger<QuestProgressionGraphService> logger
)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<QuestProgressionSnapshot> RunAsync(VanillaBaselineSnapshot baselineSnapshot, CancellationToken cancellationToken)
    {
        if (baselineSnapshot.QuestCount <= 0)
            throw new InvalidOperationException("Economy Admiral progression graph requires a non-empty pristine startup snapshot.");

        var current = Analyze(baselineSnapshot);
        var report = new QuestProgressionGraphReport
        {
            SchemaVersion = 2,
            DepthAffectsRewardAllowance = false,
            BenchmarkSource = "PristineStartupSnapshot",
            QuestCount = current.Quests.Count,
            QuestsWithPrerequisites = current.Quests.Count(row => row.DirectPrerequisiteCount > 0),
            MaximumObservedDepth = current.MaximumObservedDepth,
            CycleMemberCount = current.CycleMembers.Count,
            CycleMembers = current.CycleMembers,
            VanillaDepthBenchmark = current.VanillaDepthBenchmark,
            VanillaRestartableDepthBenchmark = current.VanillaRestartableDepthBenchmark,
            Quests = current.Quests,
        };

        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(QuestProgressionGraphService).Assembly);
        var reportPath = Path.GetFullPath(Path.Combine(modPath, "reports", "economy-admiral-progression-graph.json"));
        var modRoot = Path.GetFullPath(modPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!reportPath.StartsWith(modRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Economy Admiral progression graph report path must stay inside the mod directory.");

        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        logger.Info($"[Economy Admiral] quest progression graph complete from pristine baseline: finalQuests={report.QuestCount}, pristineQuests={baselineSnapshot.QuestCount}, maxDepth={report.MaximumObservedDepth}, cycleMembers={report.CycleMemberCount}; report={reportPath}");
        return current;
    }

    private QuestProgressionSnapshot Analyze(VanillaBaselineSnapshot baselineSnapshot)
    {
        var questIds = templates.Quests.Keys.Select(id => id.ToString()).ToHashSet(StringComparer.Ordinal);
        var prerequisiteMap = templates.Quests
            .OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key.ToString(), pair => ExtractPrerequisites(pair.Value, questIds), StringComparer.Ordinal);

        var memo = new Dictionary<string, int>(StringComparer.Ordinal);
        var cycleMembers = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<QuestProgressionGraphRow>();

        foreach (var pair in templates.Quests.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
        {
            var questId = pair.Key.ToString();
            var depth = CalculateDepth(questId, prerequisiteMap, memo, new HashSet<string>(StringComparer.Ordinal), cycleMembers);
            rows.Add(new QuestProgressionGraphRow
            {
                QuestId = questId,
                QuestName = pair.Value.QuestName ?? pair.Value.Name,
                TraderId = pair.Value.TraderId.ToString(),
                IsVanillaTraderQuest = baselineSnapshot.QuestIds.Contains(questId),
                Restartable = pair.Value.Restartable,
                DirectPrerequisiteCount = prerequisiteMap[questId].Count,
                DirectPrerequisites = prerequisiteMap[questId],
                MaximumPrerequisiteDepth = depth,
                IsCycleMember = false,
            });
        }

        rows = rows.Select(row => row with { IsCycleMember = cycleMembers.Contains(row.QuestId) }).ToList();

        var vanillaDepths = baselineSnapshot.Quests
            .Where(row => !row.Restartable && !row.IsPrerequisiteCycleMember)
            .Select(row => (double)row.MaximumPrerequisiteDepth)
            .OrderBy(value => value)
            .ToList();
        var vanillaRestartableDepths = baselineSnapshot.Quests
            .Where(row => row.Restartable && !row.IsPrerequisiteCycleMember)
            .Select(row => (double)row.MaximumPrerequisiteDepth)
            .OrderBy(value => value)
            .ToList();

        return new QuestProgressionSnapshot
        {
            MaximumObservedDepth = rows.Count == 0 ? 0 : rows.Max(row => row.MaximumPrerequisiteDepth),
            CycleMembers = cycleMembers.OrderBy(value => value, StringComparer.Ordinal).ToList(),
            VanillaDepthBenchmark = BuildDepthBenchmark(vanillaDepths),
            VanillaRestartableDepthBenchmark = BuildDepthBenchmark(vanillaRestartableDepths),
            Quests = rows,
        };
    }

    private static QuestDepthBenchmark BuildDepthBenchmark(IReadOnlyList<double> sortedDepths) => new()
    {
        QuestSamples = sortedDepths.Count,
        MedianDepth = Percentile(sortedDepths, 0.50),
        P90Depth = Percentile(sortedDepths, 0.90),
        MaximumDepth = sortedDepths.Count == 0 ? 0 : (int)sortedDepths[^1],
    };

    private static List<string> ExtractPrerequisites(Quest quest, IReadOnlySet<string> knownQuestIds)
    {
        if (quest.Conditions.AvailableForStart is null) return [];
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var condition in quest.Conditions.AvailableForStart)
        {
            if (!string.Equals(condition.ConditionType, "Quest", StringComparison.OrdinalIgnoreCase) || condition.Target is null) continue;
            var targetElement = JsonSerializer.SerializeToElement(condition.Target);
            foreach (var target in ExtractStrings(targetElement))
                if (knownQuestIds.Contains(target)) result.Add(target);
        }
        return result.OrderBy(value => value, StringComparer.Ordinal).ToList();
    }

    private static IEnumerable<string> ExtractStrings(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var value = element.GetString();
                if (!string.IsNullOrWhiteSpace(value)) yield return value;
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    foreach (var nested in ExtractStrings(item)) yield return nested;
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                    foreach (var nested in ExtractStrings(property.Value)) yield return nested;
                break;
        }
    }

    private static int CalculateDepth(
        string questId,
        IReadOnlyDictionary<string, List<string>> prerequisiteMap,
        Dictionary<string, int> memo,
        HashSet<string> visiting,
        HashSet<string> cycleMembers)
    {
        if (memo.TryGetValue(questId, out var cached)) return cached;
        if (!visiting.Add(questId))
        {
            foreach (var member in visiting) cycleMembers.Add(member);
            cycleMembers.Add(questId);
            return 0;
        }

        var maxParentDepth = 0;
        prerequisiteMap.TryGetValue(questId, out var prerequisites);
        if (prerequisites is not null)
            foreach (var prerequisite in prerequisites)
                maxParentDepth = Math.Max(maxParentDepth, CalculateDepth(prerequisite, prerequisiteMap, memo, visiting, cycleMembers));

        visiting.Remove(questId);
        var depth = (prerequisites?.Count ?? 0) == 0 ? 0 : maxParentDepth + 1;
        memo[questId] = depth;
        return depth;
    }

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
}

public sealed record QuestProgressionSnapshot
{
    public required int MaximumObservedDepth { get; init; }
    public required List<string> CycleMembers { get; init; }
    public required QuestDepthBenchmark VanillaDepthBenchmark { get; init; }
    public required QuestDepthBenchmark VanillaRestartableDepthBenchmark { get; init; }
    public required List<QuestProgressionGraphRow> Quests { get; init; }
}

public sealed record QuestProgressionGraphReport
{
    public required int SchemaVersion { get; init; }
    public required bool DepthAffectsRewardAllowance { get; init; }
    public required string BenchmarkSource { get; init; }
    public required int QuestCount { get; init; }
    public required int QuestsWithPrerequisites { get; init; }
    public required int MaximumObservedDepth { get; init; }
    public required int CycleMemberCount { get; init; }
    public required List<string> CycleMembers { get; init; }
    public required QuestDepthBenchmark VanillaDepthBenchmark { get; init; }
    public required QuestDepthBenchmark VanillaRestartableDepthBenchmark { get; init; }
    public required List<QuestProgressionGraphRow> Quests { get; init; }
}

public sealed record QuestDepthBenchmark
{
    public required int QuestSamples { get; init; }
    public required double MedianDepth { get; init; }
    public required double P90Depth { get; init; }
    public required int MaximumDepth { get; init; }
}

public sealed record QuestProgressionGraphRow
{
    public required string QuestId { get; init; }
    public required string QuestName { get; init; }
    public required string TraderId { get; init; }
    public required bool IsVanillaTraderQuest { get; init; }
    public required bool Restartable { get; init; }
    public required int DirectPrerequisiteCount { get; init; }
    public required List<string> DirectPrerequisites { get; init; }
    public required int MaximumPrerequisiteDepth { get; init; }
    public required bool IsCycleMember { get; init; }
}
