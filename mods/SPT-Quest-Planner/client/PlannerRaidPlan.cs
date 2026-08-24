using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRaidPlan
    {
        public PlannerRaidPlan(string locationId, IReadOnlyList<string> questIds, IReadOnlyList<PlannerRaidObjective> objectives)
        {
            LocationId = locationId ?? string.Empty;
            QuestIds = questIds ?? Array.Empty<string>();
            Objectives = objectives ?? Array.Empty<PlannerRaidObjective>();
        }

        public string LocationId { get; private set; }
        public IReadOnlyList<string> QuestIds { get; private set; }
        public IReadOnlyList<PlannerRaidObjective> Objectives { get; private set; }
        public int QuestCount { get { return QuestIds.Count; } }
        public int ObjectiveCount { get { return Objectives.Count; } }
        public double KnownRemainingWork { get { return Objectives.Where(value => value.RemainingValue.HasValue).Sum(value => value.RemainingValue.Value); } }
        public int KnownProgressObjectiveCount { get { return Objectives.Count(value => value.HasProgress); } }
    }

    public static class PlannerRaidPlanBuilder
    {
        public static PlannerRaidPlan Build(PlannerRaidOpportunity opportunity)
        {
            if (opportunity == null) throw new ArgumentNullException("opportunity");
            PlannerRaidObjective[] ordered = opportunity.RaidObjectives
                .OrderBy(value => value.Kind)
                .ThenBy(value => value.RemainingValue ?? double.MaxValue)
                .ThenBy(value => value.QuestId, StringComparer.Ordinal)
                .ThenBy(value => value.ConditionId, StringComparer.Ordinal)
                .ToArray();
            return new PlannerRaidPlan(opportunity.LocationId, opportunity.QuestIds.ToArray(), ordered);
        }
    }
}
