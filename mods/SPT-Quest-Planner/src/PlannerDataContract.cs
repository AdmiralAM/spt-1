namespace SPTQuestPlanner;

public static class PlannerDataContract
{
    public const string SnapshotRoute = "/admiralam/quest-planner/snapshot";
    public const int SchemaVersion = 6;
}

public sealed record PlannerSnapshotEnvelope(
    int SchemaVersion,
    long GeneratedAtUnixSeconds,
    object Profile,
    object RawQuests,
    IReadOnlyList<QuestNode> QuestNodes,
    IReadOnlyList<PrerequisiteEdge> Prerequisites,
    IReadOnlyList<ItemRequirement> ItemRequirements,
    PlayerProjection Player,
    InventoryProjection Inventory,
    IReadOnlyDictionary<QuestState, int> StateCounts,
    PlannerGraphValidation GraphValidation,
    PlannerEvaluationResult Evaluation,
    IReadOnlyList<OutstandingItemRequirement> OutstandingItems,
    IReadOnlyList<string> ExtractionWarnings);
