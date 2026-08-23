namespace SPTQuestPlanner;

public static class PlannerDataContract
{
    public const string SnapshotRoute = "/admiralam/quest-planner/snapshot";
    public const int SchemaVersion = 1;
}

public sealed record PlannerSnapshotEnvelope(
    int SchemaVersion,
    long GeneratedAtUnixSeconds,
    object Profile,
    object Quests);
