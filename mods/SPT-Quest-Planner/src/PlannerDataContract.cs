namespace SPTQuestPlanner;

public static class PlannerDataContract
{
    public const string TopologyRoute = "/admiralam/quest-planner/topology";
    public const string StateRoute = "/admiralam/quest-planner/state";
    public const string SnapshotRoute = "/admiralam/quest-planner/snapshot";
    public const string DiagnosticsRoute = "/admiralam/quest-planner/diagnostics";
    public const int SchemaVersion = 9;
}

public sealed record PlannerTopologyEnvelope(
    int SchemaVersion,
    IReadOnlyList<QuestNode> QuestNodes,
    IReadOnlyList<PrerequisiteEdge> Prerequisites,
    IReadOnlyList<ItemRequirement> ItemRequirements,
    IReadOnlyList<QuestObjectiveFact> QuestObjectives,
    PlannerGraphValidation GraphValidation,
    IReadOnlyList<string> Warnings);

public sealed record PlannerStateEnvelope(
    int SchemaVersion,
    long GeneratedAtUnixSeconds,
    PlayerProjection Player,
    InventoryProjection Inventory,
    IReadOnlyDictionary<QuestState, int> StateCounts,
    PlannerEvaluationResult Evaluation,
    IReadOnlyList<OutstandingItemRequirement> OutstandingItems,
    IReadOnlyList<string> Warnings);

// Combined compatibility payload. Production clients should load TopologyRoute once
// and refresh StateRoute only when player state may have changed.
public sealed record PlannerSnapshotEnvelope(
    int SchemaVersion,
    long GeneratedAtUnixSeconds,
    IReadOnlyList<QuestNode> QuestNodes,
    IReadOnlyList<PrerequisiteEdge> Prerequisites,
    IReadOnlyList<ItemRequirement> ItemRequirements,
    IReadOnlyList<QuestObjectiveFact> QuestObjectives,
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
