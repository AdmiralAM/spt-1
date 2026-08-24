using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRaidPlanCard
    {
        public PlannerRaidPlanCard(
            int rank,
            string locationId,
            int questCount,
            int objectiveCount,
            bool preparationReady,
            int missingBringTemplateCount,
            int unresolvedPreparationCount,
            double knownRemainingWork,
            IReadOnlyList<PlannerRaidObjective> objectives,
            IReadOnlyList<PlannerRaidBringNeed> bringNeeds)
        {
            Rank = rank;
            LocationId = locationId ?? string.Empty;
            QuestCount = questCount;
            ObjectiveCount = objectiveCount;
            PreparationReady = preparationReady;
            MissingBringTemplateCount = Math.Max(0, missingBringTemplateCount);
            UnresolvedPreparationCount = Math.Max(0, unresolvedPreparationCount);
            KnownRemainingWork = Math.Max(0d, knownRemainingWork);
            Objectives = objectives ?? Array.Empty<PlannerRaidObjective>();
            BringNeeds = bringNeeds ?? Array.Empty<PlannerRaidBringNeed>();
        }

        public int Rank { get; private set; }
        public string LocationId { get; private set; }
        public int QuestCount { get; private set; }
        public int ObjectiveCount { get; private set; }
        public bool PreparationReady { get; private set; }
        public int MissingBringTemplateCount { get; private set; }
        public int UnresolvedPreparationCount { get; private set; }
        public double KnownRemainingWork { get; private set; }
        public IReadOnlyList<PlannerRaidObjective> Objectives { get; private set; }
        public IReadOnlyList<PlannerRaidBringNeed> BringNeeds { get; private set; }
    }

    public sealed class PlannerRaidPlanViewModel
    {
        public PlannerRaidPlanViewModel(
            long generatedAtUnixSeconds,
            PlannerRaidPlanRankingMode rankingMode,
            IReadOnlyList<PlannerRaidPlanCard> cards)
        {
            GeneratedAtUnixSeconds = generatedAtUnixSeconds;
            RankingMode = rankingMode;
            Cards = cards ?? Array.Empty<PlannerRaidPlanCard>();
        }

        public long GeneratedAtUnixSeconds { get; private set; }
        public PlannerRaidPlanRankingMode RankingMode { get; private set; }
        public IReadOnlyList<PlannerRaidPlanCard> Cards { get; private set; }
        public int LocationCount { get { return Cards.Count; } }
        public int ReadyLocationCount { get { return Cards.Count(value => value.PreparationReady); } }
        public PlannerRaidPlanCard TopRecommendation { get { return Cards.Count == 0 ? null : Cards[0]; } }
    }

    public static class PlannerRaidPlanViewModelBuilder
    {
        public static PlannerRaidPlanViewModel Build(PlannerRaidPlanCollection collection, int maxObjectivesPerCard = 12)
        {
            if (collection == null) throw new ArgumentNullException("collection");
            if (maxObjectivesPerCard <= 0) throw new ArgumentOutOfRangeException("maxObjectivesPerCard");

            PlannerRaidPlanCard[] cards = collection.Plans
                .Select((plan, index) => new PlannerRaidPlanCard(
                    index + 1,
                    plan.LocationId,
                    plan.QuestCount,
                    plan.ObjectiveCount,
                    plan.PreparationReady,
                    plan.MissingBringTemplateCount,
                    plan.Preparation.UnresolvedNeeds.Count,
                    plan.KnownRemainingWork,
                    plan.Objectives.Take(maxObjectivesPerCard).ToArray(),
                    plan.Preparation.ExactNeeds.ToArray()))
                .ToArray();

            return new PlannerRaidPlanViewModel(collection.GeneratedAtUnixSeconds, collection.RankingMode, cards);
        }
    }
}
