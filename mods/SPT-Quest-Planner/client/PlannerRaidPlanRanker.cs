using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTQuestPlanner.Client
{
    public enum PlannerRaidPlanRankingMode
    {
        ReadyFirst = 0,
        QuestDensityFirst = 1
    }

    public static class PlannerRaidPlanRanker
    {
        public static IReadOnlyList<PlannerRaidPlan> Rank(
            IEnumerable<PlannerRaidPlan> plans,
            PlannerRaidPlanRankingMode mode = PlannerRaidPlanRankingMode.ReadyFirst)
        {
            if (plans == null) throw new ArgumentNullException("plans");

            IEnumerable<PlannerRaidPlan> source = plans.Where(value => value != null);
            IOrderedEnumerable<PlannerRaidPlan> ordered;

            if (mode == PlannerRaidPlanRankingMode.QuestDensityFirst)
            {
                ordered = source
                    .OrderByDescending(value => value.QuestCount)
                    .ThenByDescending(value => value.PreparationReady)
                    .ThenBy(value => value.MissingBringTemplateCount)
                    .ThenByDescending(value => value.ObjectiveCount)
                    .ThenBy(value => value.KnownRemainingWork)
                    .ThenBy(value => value.LocationId, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                ordered = source
                    .OrderByDescending(value => value.PreparationReady)
                    .ThenBy(value => value.MissingBringTemplateCount)
                    .ThenByDescending(value => value.QuestCount)
                    .ThenByDescending(value => value.ObjectiveCount)
                    .ThenBy(value => value.KnownRemainingWork)
                    .ThenBy(value => value.LocationId, StringComparer.OrdinalIgnoreCase);
            }

            return ordered.ToArray();
        }
    }
}
