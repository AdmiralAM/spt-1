using System.Text.Json;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTEconomy;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.Watermark + 1)]
public sealed class VanillaBaselineService(
    TemplateTable templates,
    TradersTable traders,
    EconomyRuntimeConfigService runtimeConfigService,
    ISptLogger<VanillaBaselineService> logger
) : IOnLoad
{
    private VanillaBaselineSnapshot? snapshot;

    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var config = await runtimeConfigService.GetAsync(cancellationToken);
        if (config.Mode == EconomyMode.Off)
        {
            return;
        }

        var handbookPrices = templates.Handbook.Items
            .Where(item => item.Price is > 0)
            .GroupBy(item => item.Id.ToString(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Price!.Value, StringComparer.Ordinal);

        var questIds = templates.Quests.Keys.Select(id => id.ToString()).ToHashSet(StringComparer.Ordinal);
        var prerequisiteMap = templates.Quests
            .OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
            .ToDictionary(
                pair => pair.Key.ToString(),
                pair => ExtractPrerequisites(pair.Value, questIds),
                StringComparer.Ordinal
            );

        var memo = new Dictionary<string, int>(StringComparer.Ordinal);
        var cycleMembers = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<VanillaQuestBaselineRow>();

        foreach (var pair in templates.Quests.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
        {
            var questId = pair.Key.ToString();
            var quest = pair.Value;
            var successRewards = EnumerateRewards(quest, successOnly: true).ToList();
            var allRewards = EnumerateRewards(quest, successOnly: false).ToList();
            var successItemValue = CalculateItemValue(successRewards, handbookPrices);
            var allItemValue = CalculateItemValue(allRewards, handbookPrices);
            var objectiveConditions = EnumerateObjectiveConditions(quest).ToList();
            var counterConditions = objectiveConditions
                .Where(condition => condition.Counter?.Conditions is not null)
                .SelectMany(condition => condition.Counter!.Conditions!)
                .ToList();
            var depth = CalculateDepth(questId, prerequisiteMap, memo, new HashSet<string>(StringComparer.Ordinal), cycleMembers);

            rows.Add(new VanillaQuestBaselineRow
            {
                QuestId = questId,
                Restartable = quest.Restartable,
                SuccessKnownHandbookValue = Math.Round(successItemValue.KnownValue, 2),
                AllRewardKnownHandbookValue = Math.Round(allItemValue.KnownValue, 2),
                UnknownSuccessItemRecords = successItemValue.UnknownRecords,
                Experience = Math.Round(successRewards.Where(reward => reward.Type == RewardType.Experience).Sum(reward => reward.Value ?? 0d), 2),
                TraderStanding = Math.Round(successRewards.Where(reward => reward.Type == RewardType.TraderStanding).Sum(reward => reward.Value ?? 0d), 4),
                TraderUnlocks = CountTargets(successRewards, RewardType.TraderUnlock),
                AssortmentUnlocks = CountTargets(successRewards, RewardType.AssortmentUnlock),
                ProductionSchemeUnlocks = CountTargets(successRewards, RewardType.ProductionScheme),
                RequiredLevel = ExtractRequiredLevel(quest.Conditions.AvailableForStart),
                ObjectiveConditionCount = objectiveConditions.Count,
                DirectPrerequisiteCount = prerequisiteMap[questId].Count,
                MaximumPrerequisiteDepth = depth,
                IsPrerequisiteCycleMember = false,
                StructuredConstraintCount = CountStructuredConstraints(objectiveConditions, counterConditions),
            });
        }

        rows = rows.Select(row => row with { IsPrerequisiteCycleMember = cycleMembers.Contains(row.QuestId) }).ToList();
        snapshot = new VanillaBaselineSnapshot
        {
            CapturePriority = OnLoadOrder.Watermark + 1,
            QuestCount = rows.Count,
            TraderCount = traders.Count,
            HandbookItemCount = handbookPrices.Count,
            QuestIds = rows.Select(row => row.QuestId).ToHashSet(StringComparer.Ordinal),
            Quests = rows,
        };

        logger.Info($"[Economy Admiral] pristine vanilla baseline captured before normal mod callbacks: quests={snapshot.QuestCount}, traders={snapshot.TraderCount}, handbookPrices={snapshot.HandbookItemCount}, priority={snapshot.CapturePriority}");
    }

    public VanillaBaselineSnapshot GetSnapshot() => snapshot
        ?? throw new InvalidOperationException("Economy Admiral pristine vanilla baseline was not captured before final analysis.");

    private static (double KnownValue, int UnknownRecords) CalculateItemValue(
        IEnumerable<Reward> rewards,
        IReadOnlyDictionary<string, double> handbookPrices
    )
    {
        var known = 0d;
        var unknown = 0;
        foreach (var item in rewards.Where(reward => reward.Items is not null).SelectMany(reward => reward.Items!))
        {
            var templateId = item.Template.ToString();
            if (string.IsNullOrWhiteSpace(templateId))
            {
                continue;
            }

            var count = Math.Max(1d, item.Upd?.StackObjectsCount ?? 1d);
            if (handbookPrices.TryGetValue(templateId, out var price))
            {
                known += price * count;
            }
            else
            {
                unknown++;
            }
        }
        return (known, unknown);
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

    private static int CountStructuredConstraints(
        IReadOnlyCollection<QuestCondition> objectiveConditions,
        IReadOnlyCollection<QuestConditionCounterCondition> counterConditions
    )
    {
        var timed = objectiveConditions.Count(condition => condition.CompleteInSeconds is > 0)
            + counterConditions.Count(condition => condition.CompleteInSeconds is > 0);
        var oneSession = objectiveConditions.Count(condition => condition.OneSessionOnly == true)
            + counterConditions.Count(condition => condition.ResetOnSessionEnd == true);
        var fir = objectiveConditions.Count(condition => condition.OnlyFoundInRaid == true);
        var plant = objectiveConditions.Count(condition => condition.PlantTime is > 0);
        var distance = counterConditions.Count(condition => condition.Distance?.Value is > 0);
        var daytime = counterConditions.Count(condition => condition.Daytime is not null);
        return timed + oneSession + fir + plant + distance + daytime;
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
            var element = JsonSerializer.SerializeToElement(condition.Target);
            foreach (var target in ExtractStrings(element))
            {
                if (knownQuestIds.Contains(target)) result.Add(target);
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
        HashSet<string> cycleMembers
    )
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
}

public sealed record VanillaBaselineSnapshot
{
    public required int CapturePriority { get; init; }
    public required int QuestCount { get; init; }
    public required int TraderCount { get; init; }
    public required int HandbookItemCount { get; init; }
    public required HashSet<string> QuestIds { get; init; }
    public required List<VanillaQuestBaselineRow> Quests { get; init; }
}

public sealed record VanillaQuestBaselineRow
{
    public required string QuestId { get; init; }
    public required bool Restartable { get; init; }
    public required double SuccessKnownHandbookValue { get; init; }
    public required double AllRewardKnownHandbookValue { get; init; }
    public required int UnknownSuccessItemRecords { get; init; }
    public required double Experience { get; init; }
    public required double TraderStanding { get; init; }
    public required int TraderUnlocks { get; init; }
    public required int AssortmentUnlocks { get; init; }
    public required int ProductionSchemeUnlocks { get; init; }
    public required int RequiredLevel { get; init; }
    public required int ObjectiveConditionCount { get; init; }
    public required int DirectPrerequisiteCount { get; init; }
    public required int MaximumPrerequisiteDepth { get; init; }
    public required bool IsPrerequisiteCycleMember { get; init; }
    public required int StructuredConstraintCount { get; init; }
}
