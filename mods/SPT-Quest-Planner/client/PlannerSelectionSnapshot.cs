namespace SPTQuestPlanner.Client
{
    public sealed class PlannerSelectionSnapshot
    {
        public PlannerSelectionSnapshot(long cacheRevision, string activeLocationId, string progressionTargetQuestId)
        {
            CacheRevision = cacheRevision;
            ActiveLocationId = activeLocationId ?? string.Empty;
            ProgressionTargetQuestId = progressionTargetQuestId ?? string.Empty;
        }

        public long CacheRevision { get; private set; }
        public string ActiveLocationId { get; private set; }
        public string ProgressionTargetQuestId { get; private set; }
        public bool HasActiveRaidPlan { get { return !string.IsNullOrWhiteSpace(ActiveLocationId); } }
        public bool HasProgressionTarget { get { return !string.IsNullOrWhiteSpace(ProgressionTargetQuestId); } }
    }
}
