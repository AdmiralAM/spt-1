using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRaidPlan
    {
        public PlannerRaidPlan(
            string locationId,
            IReadOnlyList<string> questIds,
            IReadOnlyList<PlannerRaidObjective> objectives)
            : this(
                locationId,
                questIds,
                objectives,
                new PlannerRaidPreparation(
                    Array.Empty<PlannerRaidBringNeed>(),
                    Array.Empty<PlannerRaidUnresolvedBringNeed>()))
        {
        }

        public PlannerRaidPlan(
            string locationId,
            IReadOnlyList<string> questIds,
            IReadOnlyList<PlannerRaidObjective> objectives,
            PlannerRaidPreparation preparation)
        {
            LocationId = locationId ?? string.Empty;
            QuestIds = questIds ?? Array.Empty<string>();
            Objectives = objectives ?? Array.Empty<PlannerRaidObjective>();
            Preparation = preparation ?? new PlannerRaidPreparation(
                Array.Empty<PlannerRaidBringNeed>(),
                Array.Empty<PlannerRaidUnresolvedBringNeed>());
        }

        public string LocationId { get; private set; }
        public IReadOnlyList<string> QuestIds { get; private set; }
        public IReadOnlyList<PlannerRaidObjective> Objectives { get; private set; }
        public PlannerRaidPreparation Preparation { get; private set; }
        public int QuestCount { get { return QuestIds.Count; } }
        public int ObjectiveCount { get { return Objectives.Count; } }
        public double KnownRemainingWork { get { return Objectives.Where(value => value.RemainingValue.HasValue).Sum(value => value.RemainingValue.Value); } }
        public int KnownProgressObjectiveCount { get { return Objectives.Count(value => value.HasProgress); } }
        public bool PreparationReady { get { return Preparation.Ready; } }
        public int MissingBringTemplateCount { get { return Preparation.MissingTemplateCount; } }
        public int UnresolvedPreparationCount { get { return Preparation.UnresolvedNeeds.Count; } }
    }

    public static class PlannerRaidPlanBuilder
    {
        public static PlannerRaidPlan Build(PlannerRaidOpportunity opportunity, PlannerClientIndex state)
        {
            if (opportunity == null) throw new ArgumentNullException("opportunity");
            if (state == null) throw new ArgumentNullException("state");

            PlannerRaidObjective[] ordered = opportunity.RaidObjectives
                .OrderBy(value => value.Kind)
                .ThenBy(value => value.RemainingValue ?? double.MaxValue)
                .ThenBy(value => value.QuestId, StringComparer.Ordinal)
                .ThenBy(value => value.ConditionId, StringComparer.Ordinal)
                .ToArray();

            PlannerRaidPlan provisional = new PlannerRaidPlan(
                opportunity.LocationId,
                opportunity.QuestIds.ToArray(),
                ordered);

            PlannerRaidPreparation preparation = PlannerRaidPreparationBuilder.Build(provisional, state);
            return new PlannerRaidPlan(opportunity.LocationId, opportunity.QuestIds.ToArray(), ordered, preparation);
        }
    }
}
