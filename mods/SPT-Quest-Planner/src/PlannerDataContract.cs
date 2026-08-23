namespace SPTQuestPlanner;

public static class PlannerDataContract
{
    public const string SnapshotRoute = "/admiralam/quest-planner/snapshot";
    public const int SchemaVersion = 2;
}

public sealed record PlannerSnapshotEnvelope(
    int SchemaVersion,
    long GeneratedAtUnixSeconds,
    object Profile,
    object RawQuests,
    IReadOnlyList<QuestNode> QuestNodes,
    IReadOnlyList<PrerequisiteEdge> Prerequisites,
    IReadOnlyList<ItemRequirement> ItemRequirements,
    PlannerGraphValidation GraphValidation,
    IReadOnlyList<string> ExtractionWarnings);
