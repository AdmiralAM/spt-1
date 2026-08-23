namespace SPTQuestPlanner;

public enum PlannerQuestDisposition
{
    Unknown = 0,
    Blocked = 1,
    Reachable = 2,
    Available = 3,
    Active = 4,
    Completed = 5,
    Failed = 6
}

public sealed record QuestEvaluation(
    string QuestId,
    PlannerQuestDisposition Disposition,
    QuestState ProfileState,
    bool LevelGateSatisfied,
    bool PrerequisitesSatisfied,
    IReadOnlyList<string> BlockingQuestIds);

public sealed record AggregatedItemRequirement(
    string TemplateId,
    double CurrentRequired,
    double FutureRequired,
    bool RequiresFoundInRaid,
    IReadOnlySet<string> CurrentQuestIds,
    IReadOnlySet<string> FutureQuestIds);

public sealed record PlannerEvaluationResult(
    IReadOnlyDictionary<string, QuestEvaluation> Quests,
    IReadOnlyList<AggregatedItemRequirement> ItemRequirements,
    IReadOnlyList<string> Warnings);

public static class PlannerEvaluator
{
    public static PlannerEvaluationResult Evaluate(
        PlannerGraph graph,
        IEnumerable<ItemRequirement> requirements,
        PlayerProjection player)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(player);

        Dictionary<string, QuestEvaluation> evaluations = new(StringComparer.Ordinal);
        List<string> warnings = new();

        foreach (QuestNode node in graph.Nodes.Values)
        {
            QuestState profileState = player.GetState(node.QuestId);
            bool levelSatisfied = node.MinimumLevel is null || (player.Level is not null && player.Level >= node.MinimumLevel);

            IReadOnlyList<PrerequisiteEdge> prerequisiteEdges = graph.GetPrerequisites(node.QuestId);
            List<string> blockers = new();
            bool prerequisitesSatisfied = true;

            foreach (PrerequisiteEdge edge in prerequisiteEdges)
            {
                QuestState sourceState = player.GetState(edge.SourceQuestId);
                if (edge.AcceptedSourceStates.Count == 0)
                {
                    prerequisitesSatisfied = false;
                    blockers.Add(edge.SourceQuestId);
                    warnings.Add($"Quest {node.QuestId}: prerequisite {edge.SourceQuestId} has no accepted source states; treated as blocking");
                    continue;
                }

                if (edge.AcceptedSourceStates.Contains(sourceState)) continue;
                prerequisitesSatisfied = false;
                blockers.Add(edge.SourceQuestId);
            }

            PlannerQuestDisposition disposition = Classify(
                profileState,
                levelSatisfied,
                prerequisitesSatisfied);

            evaluations[node.QuestId] = new QuestEvaluation(
                node.QuestId,
                disposition,
                profileState,
                levelSatisfied,
                prerequisitesSatisfied,
                blockers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        }

        IReadOnlyList<AggregatedItemRequirement> aggregated = AggregateRequirements(requirements, evaluations);
        return new PlannerEvaluationResult(evaluations, aggregated, warnings.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static PlannerQuestDisposition Classify(
        QuestState profileState,
        bool levelSatisfied,
        bool prerequisitesSatisfied)
    {
        return profileState switch
        {
            QuestState.Success => PlannerQuestDisposition.Completed,
            QuestState.Started => PlannerQuestDisposition.Active,
            QuestState.Available => PlannerQuestDisposition.Available,
            QuestState.Failed => PlannerQuestDisposition.Failed,
            QuestState.Unknown => PlannerQuestDisposition.Unknown,
            _ when levelSatisfied && prerequisitesSatisfied => PlannerQuestDisposition.Reachable,
            _ => PlannerQuestDisposition.Blocked
        };
    }

    private static IReadOnlyList<AggregatedItemRequirement> AggregateRequirements(
        IEnumerable<ItemRequirement> requirements,
        IReadOnlyDictionary<string, QuestEvaluation> evaluations)
    {
        Dictionary<string, MutableAggregate> byTemplate = new(StringComparer.Ordinal);

        foreach (ItemRequirement requirement in requirements)
        {
            if (!evaluations.TryGetValue(requirement.QuestId, out QuestEvaluation? evaluation)) continue;
            if (evaluation.Disposition is PlannerQuestDisposition.Completed or PlannerQuestDisposition.Failed) continue;

            bool current = evaluation.Disposition is PlannerQuestDisposition.Active or PlannerQuestDisposition.Available;
            bool future = evaluation.Disposition is PlannerQuestDisposition.Reachable or PlannerQuestDisposition.Blocked;
            if (!current && !future) continue;

            foreach (string templateId in requirement.TemplateIds)
            {
                if (string.IsNullOrWhiteSpace(templateId)) continue;
                if (!byTemplate.TryGetValue(templateId, out MutableAggregate? aggregate))
                {
                    aggregate = new MutableAggregate();
                    byTemplate[templateId] = aggregate;
                }

                aggregate.RequiresFoundInRaid |= requirement.FoundInRaid;
                if (current)
                {
                    aggregate.CurrentRequired += requirement.RequiredCount;
                    aggregate.CurrentQuestIds.Add(requirement.QuestId);
                }
                else if (future)
                {
                    aggregate.FutureRequired += requirement.RequiredCount;
                    aggregate.FutureQuestIds.Add(requirement.QuestId);
                }
            }
        }

        return byTemplate
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new AggregatedItemRequirement(
                pair.Key,
                pair.Value.CurrentRequired,
                pair.Value.FutureRequired,
                pair.Value.RequiresFoundInRaid,
                new HashSet<string>(pair.Value.CurrentQuestIds, StringComparer.Ordinal),
                new HashSet<string>(pair.Value.FutureQuestIds, StringComparer.Ordinal)))
            .ToArray();
    }

    private sealed class MutableAggregate
    {
        public double CurrentRequired;
        public double FutureRequired;
        public bool RequiresFoundInRaid;
        public HashSet<string> CurrentQuestIds { get; } = new(StringComparer.Ordinal);
        public HashSet<string> FutureQuestIds { get; } = new(StringComparer.Ordinal);
    }
}
