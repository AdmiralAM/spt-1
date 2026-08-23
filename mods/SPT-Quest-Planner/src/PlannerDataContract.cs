namespace SPTQuestPlanner;

public static class PlannerDataContract
{
    public const string SnapshotRoute = "/admiralam/quest-planner/snapshot";
    public const string DiagnosticsRoute = "/admiralam/quest-planner/diagnostics";
    public const int SchemaVersion = 7;
}

public sealed record PlannerSnapshotEnvelope(
    int SchemaVersion,
    long GeneratedAtUnixSeconds,
    IReadOnlyList<QuestNode> QuestNodes,
    IReadOnlyList<PrerequisiteEdge> Prerequisites,
    IReadOnlyList<ItemRequirement> ItemRequirements,
    PlayerProjection Player,
    InventoryProjection Inventory,
    IReadOnlyDictionary<QuestState, int> StateCounts,
    PlannerGraphValidation GraphValidation,
    PlannerEvaluationResult Evaluation,
    IReadOnlyList<OutstandingItemRequirement> OutstandingItems,
    IReadOnlyList<string> Warnings);

public sealed record PlannerDiagnosticsEnvelope(
    int SchemaVersion,
    long GeneratedAtUnixSeconds,
    object Profile,
    object RawQuests,
    IReadOnlyList<string> Warnings);
