using System;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRaidPlanProvider
    {
        private readonly PlannerClientCache cache;
        private readonly object gate = new object();
        private long cachedRevision = -1;
        private PlannerRaidPlanCollection readyFirst;
        private PlannerRaidPlanCollection densityFirst;
        private PlannerRaidPlanCollection readyFirstIncludingAvailable;
        private PlannerRaidPlanCollection densityFirstIncludingAvailable;

        public PlannerRaidPlanProvider(PlannerClientCache cache)
        {
            this.cache = cache ?? throw new ArgumentNullException("cache");
        }

        public PlannerRaidPlanCollection Get(
            PlannerRaidPlanRankingMode rankingMode = PlannerRaidPlanRankingMode.ReadyFirst,
            bool includeAvailable = false)
        {
            lock (gate)
            {
                if (!cache.HasTopology || !cache.HasState)
                    return new PlannerRaidPlanCollection(
                        cache.Index == null ? 0L : cache.Index.GeneratedAtUnixSeconds,
                        rankingMode,
                        Array.Empty<PlannerRaidPlan>());

                long revision = cache.Revision;
                if (revision != cachedRevision)
                {
                    cachedRevision = revision;
                    readyFirst = null;
                    densityFirst = null;
                    readyFirstIncludingAvailable = null;
                    densityFirstIncludingAvailable = null;
                }

                PlannerRaidPlanCollection existing = Select(rankingMode, includeAvailable);
                if (existing != null) return existing;

                PlannerRaidPlanCollection built = PlannerRaidPlanCollectionBuilder.Build(
                    cache.LocationIndex,
                    cache.Index,
                    rankingMode,
                    includeAvailable);
                Store(rankingMode, includeAvailable, built);
                return built;
            }
        }

        private PlannerRaidPlanCollection Select(PlannerRaidPlanRankingMode mode, bool includeAvailable)
        {
            if (mode == PlannerRaidPlanRankingMode.QuestDensityFirst)
                return includeAvailable ? densityFirstIncludingAvailable : densityFirst;
            return includeAvailable ? readyFirstIncludingAvailable : readyFirst;
        }

        private void Store(PlannerRaidPlanRankingMode mode, bool includeAvailable, PlannerRaidPlanCollection value)
        {
            if (mode == PlannerRaidPlanRankingMode.QuestDensityFirst)
            {
                if (includeAvailable) densityFirstIncludingAvailable = value;
                else densityFirst = value;
            }
            else
            {
                if (includeAvailable) readyFirstIncludingAvailable = value;
                else readyFirst = value;
            }
        }
    }
}
