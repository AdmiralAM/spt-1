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

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var questIds = templates.Quests.Keys
            .Select(id => id.ToString())
            .ToHashSet(StringComparer.Ordinal);

        var prerequisiteMap = templates.Quests
            .OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
            .ToDictionary(
                pair => pair.Key.ToString(),
                pair => ExtractPrerequisites(pair.Value, questIds),
                StringComparer.Ordinal
            );

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
                DirectPrerequisiteCount = prerequisiteMap[questId].Count,
                DirectPrerequisites = prerequisiteMap[questId],
                MaximumPrerequisiteDepth = depth,
                IsCycleMember = cycleMembers.Contains(questId),
            });
        }

        var report = new QuestProgressionGraphReport
        {
            SchemaVersion = 1,
            QuestCount = rows.Count,
            QuestsWithPrerequisites = rows.Count(row => row.DirectPrerequisiteCount > 0),
            MaximumObservedDepth = rows.Count == 0 ? 0 : rows.Max(row => row.MaximumPrerequisiteDepth),
            CycleMemberCount = cycleMembers.Count,
            CycleMembers = cycleMembers.OrderBy(value => value, StringComparer.Ordinal).ToList(),
            Quests = rows,
        };

        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(QuestProgressionGraphService).Assembly);
        var reportPath = Path.GetFullPath(Path.Combine(modPath, "reports", "economy-admiral-progression-graph.json"));
        var modRoot = Path.GetFullPath(modPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!reportPath.StartsWith(modRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Economy Admiral progression graph report path must stay inside the mod directory.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        logger.Info($"[Economy Admiral] quest progression graph complete: {report.QuestCount} quests, maxDepth={report.MaximumObservedDepth}, cycleMembers={report.CycleMemberCount}; report={reportPath}");
    }

    private static List<string> ExtractPrerequisites(Quest quest, IReadOnlySet<string> knownQuestIds)
    {
        if (quest.Conditions.AvailableForStart is null)
        {
            return [];
        }

        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var condition in quest.Conditions.AvailableForStart)
        {
            if (!string.Equals(condition.ConditionType, "Quest", StringComparison.OrdinalIgnoreCase) || condition.Target is null)
            {
                continue;
            }

            var targetElement = JsonSerializer.SerializeToElement(condition.Target);
            foreach (var target in ExtractStrings(targetElement))
            {
                if (knownQuestIds.Contains(target))
                {
                    result.Add(target);
                }
            }
        }

        return result.OrderBy(value => value, StringComparer.Ordinal).ToList();
    }

    private static IEnumerable<string> ExtractStrings(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var value = element.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return value;
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var nested in ExtractStrings(item))
                    {
                        yield return nested;
                    }
                }
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    foreach (var nested in ExtractStrings(property.Value))
                    {
                        yield return nested;
                    }
                }
                break;
        }
    }

    private static int CalculateDepth(
        string questId,
        IReadOnlyDictionary<string, List<string>> prerequisiteMap,
        Dictionary<string, int> memo,
        HashSet<string> visiting,
        HashSet<string> cycleMembers
    )
    {
        if (memo.TryGetValue(questId, out var cached))
        {
            return cached;
        }

        if (!visiting.Add(questId))
        {
            foreach (var member in visiting)
            {
                cycleMembers.Add(member);
            }
            cycleMembers.Add(questId);
            return 0;
        }

        var maxParentDepth = 0;
        if (prerequisiteMap.TryGetValue(questId, out var prerequisites))
        {
            foreach (var prerequisite in prerequisites)
            {
                maxParentDepth = Math.Max(
                    maxParentDepth,
                    CalculateDepth(prerequisite, prerequisiteMap, memo, visiting, cycleMembers)
                );
            }
        }

        visiting.Remove(questId);
        var depth = (prerequisites?.Count ?? 0) == 0 ? 0 : maxParentDepth + 1;
        memo[questId] = depth;
        return depth;
    }
}

public sealed record QuestProgressionGraphReport
{
    public required int SchemaVersion { get; init; }
    public required int QuestCount { get; init; }
    public required int QuestsWithPrerequisites { get; init; }
    public required int MaximumObservedDepth { get; init; }
    public required int CycleMemberCount { get; init; }
    public required List<string> CycleMembers { get; init; }
    public required List<QuestProgressionGraphRow> Quests { get; init; }
}

public sealed record QuestProgressionGraphRow
{
    public required string QuestId { get; init; }
    public required string QuestName { get; init; }
    public required string TraderId { get; init; }
    public required int DirectPrerequisiteCount { get; init; }
    public required List<string> DirectPrerequisites { get; init; }
    public required int MaximumPrerequisiteDepth { get; init; }
    public required bool IsCycleMember { get; init; }
}
