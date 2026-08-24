using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRaidPlanCollection
    {
        public PlannerRaidPlanCollection(
            long generatedAtUnixSeconds,
            PlannerRaidPlanRankingMode rankingMode,
            IReadOnlyList<PlannerRaidPlan> plans)
        {
            GeneratedAtUnixSeconds = generatedAtUnixSeconds;
            RankingMode = rankingMode;
            Plans = plans ?? Array.Empty<PlannerRaidPlan>();
        }

        public long GeneratedAtUnixSeconds { get; private set; }
        public PlannerRaidPlanRankingMode RankingMode { get; private set; }
        public IReadOnlyList<PlannerRaidPlan> Plans { get; private set; }
        public int LocationCount { get { return Plans.Count; } }
        public int ReadyLocationCount { get { return Plans.Count(value => value.PreparationReady); } }
        public int TotalQuestCount { get { return Plans.SelectMany(value => value.QuestIds).Distinct(StringComparer.Ordinal).Count(); } }
        public int TotalObjectiveCount { get { return Plans.Sum(value => value.ObjectiveCount); } }

        public PlannerRaidPlan GetLocation(string locationId)
        {
            if (string.IsNullOrWhiteSpace(locationId)) return null;
            for (int i = 0; i < Plans.Count; i++)
                if (string.Equals(Plans[i].LocationId, locationId, StringComparison.OrdinalIgnoreCase)) return Plans[i];
            return null;
        }
    }

    public static class PlannerRaidPlanCollectionBuilder
    {
        public static PlannerRaidPlanCollection Build(
            PlannerLocationIndex locations,
            PlannerClientIndex state,
            PlannerRaidPlanRankingMode rankingMode = PlannerRaidPlanRankingMode.ReadyFirst,
            bool includeAvailable = false,
            int maxLocations = 64,
            int maxObjectivesPerLocation = 128)
        {
            if (locations == null) throw new ArgumentNullException("locations");
            if (state == null) throw new ArgumentNullException("state");

            IReadOnlyList<PlannerRaidOpportunity> opportunities = PlannerRaidOpportunityBuilder.Build(
                locations,
                state,
                includeAvailable,
                maxLocations,
                maxObjectivesPerLocation);

            PlannerRaidPlan[] plans = new PlannerRaidPlan[opportunities.Count];
            for (int i = 0; i < opportunities.Count; i++)
                plans[i] = PlannerRaidPlanBuilder.Build(opportunities[i], state);

            IReadOnlyList<PlannerRaidPlan> ranked = PlannerRaidPlanRanker.Rank(plans, rankingMode);
            return new PlannerRaidPlanCollection(state.GeneratedAtUnixSeconds, rankingMode, ranked);
        }
    }
}
