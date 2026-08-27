namespace SPTEconomy;

public sealed record QuestGateNode
{
    public required string QuestId { get; init; }
    public int? LevelRequirement { get; init; }
    public IReadOnlyList<string> PrerequisiteQuestIds { get; init; } = Array.Empty<string>();
}

public sealed record EffectiveQuestGateEvidence
{
    public required string QuestId { get; init; }
    public required int MaximumPrerequisiteDepth { get; init; }
    public int? EffectiveMinimumLevel { get; init; }
    public required int KnownLevelConstraintCount { get; init; }
    public required bool CompleteQuestGraphEvidence { get; init; }
}

public static class EffectiveQuestGateEvidenceResolver
{
    public static EffectiveQuestGateEvidence Resolve(string questId, IEnumerable<QuestGateNode> nodes)
    {
        if (string.IsNullOrWhiteSpace(questId))
        {
            throw new InvalidOperationException("Economy Admiral effective gate: quest id must not be empty.");
        }
        ArgumentNullException.ThrowIfNull(nodes);

        var map = nodes.Select(Validate).ToDictionary(node => node.QuestId, StringComparer.Ordinal);
        if (!map.ContainsKey(questId))
        {
            throw new InvalidOperationException($"Economy Admiral effective gate: missing target quest '{questId}'.");
        }

        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var memo = new Dictionary<string, EffectiveQuestGateEvidence>(StringComparer.Ordinal);
        return ResolveNode(questId, map, visiting, memo);
    }

    private static QuestGateNode Validate(QuestGateNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (string.IsNullOrWhiteSpace(node.QuestId))
        {
            throw new InvalidOperationException("Economy Admiral effective gate: quest identity must not be empty.");
        }
        if (node.LevelRequirement is < 1)
        {
            throw new InvalidOperationException($"Economy Admiral effective gate: quest '{node.QuestId}' has invalid level requirement.");
        }
        if (node.PrerequisiteQuestIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException($"Economy Admiral effective gate: quest '{node.QuestId}' has an empty prerequisite id.");
        }
        return node with
        {
            QuestId = node.QuestId.Trim(),
            PrerequisiteQuestIds = node.PrerequisiteQuestIds.Select(id => id.Trim()).Distinct(StringComparer.Ordinal).ToArray(),
        };
    }

    private static EffectiveQuestGateEvidence ResolveNode(
        string questId,
        IReadOnlyDictionary<string, QuestGateNode> map,
        HashSet<string> visiting,
        Dictionary<string, EffectiveQuestGateEvidence> memo)
    {
        if (memo.TryGetValue(questId, out var cached))
        {
            return cached;
        }
        if (!map.TryGetValue(questId, out var node))
        {
            throw new InvalidOperationException($"Economy Admiral effective gate: missing prerequisite quest '{questId}'.");
        }
        if (!visiting.Add(questId))
        {
            throw new InvalidOperationException($"Economy Admiral effective gate: prerequisite cycle detected at '{questId}'.");
        }

        var maxDepth = 0;
        var maxLevel = node.LevelRequirement;
        var knownLevelCount = node.LevelRequirement.HasValue ? 1 : 0;
        var complete = true;

        foreach (var prerequisiteId in node.PrerequisiteQuestIds)
        {
            var prerequisite = ResolveNode(prerequisiteId, map, visiting, memo);
            maxDepth = Math.Max(maxDepth, prerequisite.MaximumPrerequisiteDepth + 1);
            knownLevelCount += prerequisite.KnownLevelConstraintCount;
            if (prerequisite.EffectiveMinimumLevel.HasValue)
            {
                maxLevel = Math.Max(maxLevel ?? 0, prerequisite.EffectiveMinimumLevel.Value);
            }
            complete &= prerequisite.CompleteQuestGraphEvidence;
        }

        visiting.Remove(questId);
        var result = new EffectiveQuestGateEvidence
        {
            QuestId = questId,
            MaximumPrerequisiteDepth = maxDepth,
            EffectiveMinimumLevel = maxLevel,
            KnownLevelConstraintCount = knownLevelCount,
            CompleteQuestGraphEvidence = complete,
        };
        memo[questId] = result;
        return result;
    }
}
