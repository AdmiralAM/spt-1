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
            IReadOnlyList<PlannerRaidBringNeed> bringNeeds,
            int killObjectiveCount = 0,
            int visitObjectiveCount = 0,
            int plantObjectiveCount = 0,
            int findObjectiveCount = 0,
            int extractObjectiveCount = 0,
            string rankReason = null,
            IReadOnlyList<string> questIds = null)
        {
            Rank = rank;
            LocationId = locationId ?? string.Empty;
            QuestCount = Math.Max(0, questCount);
            ObjectiveCount = Math.Max(0, objectiveCount);
            PreparationReady = preparationReady;
            MissingBringTemplateCount = Math.Max(0, missingBringTemplateCount);
            UnresolvedPreparationCount = Math.Max(0, unresolvedPreparationCount);
            KnownRemainingWork = Math.Max(0d, knownRemainingWork);
            Objectives = objectives ?? Array.Empty<PlannerRaidObjective>();
            BringNeeds = bringNeeds ?? Array.Empty<PlannerRaidBringNeed>();
            KillObjectiveCount = Math.Max(0, killObjectiveCount);
            VisitObjectiveCount = Math.Max(0, visitObjectiveCount);
            PlantObjectiveCount = Math.Max(0, plantObjectiveCount);
            FindObjectiveCount = Math.Max(0, findObjectiveCount);
            ExtractObjectiveCount = Math.Max(0, extractObjectiveCount);
            RankReason = rankReason ?? string.Empty;
            QuestIds = questIds ?? Array.Empty<string>();
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
        public int KillObjectiveCount { get; private set; }
        public int VisitObjectiveCount { get; private set; }
        public int PlantObjectiveCount { get; private set; }
        public int FindObjectiveCount { get; private set; }
        public int ExtractObjectiveCount { get; private set; }
        public string RankReason { get; private set; }
        public IReadOnlyList<string> QuestIds { get; private set; }

        public bool SupportsQuest(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId)) return false;
            for (int i = 0; i < QuestIds.Count; i++)
                if (string.Equals(QuestIds[i], questId, StringComparison.Ordinal)) return true;
            return false;
        }

        public string PreparationLabel
        {
            get
            {
                if (PreparationReady) return "Ready";
                if (MissingBringTemplateCount > 0 && UnresolvedPreparationCount > 0)
                    return "Need " + MissingBringTemplateCount + " item type(s); check " + UnresolvedPreparationCount;
                if (MissingBringTemplateCount > 0) return "Need " + MissingBringTemplateCount + " item type(s)";
                if (UnresolvedPreparationCount > 0) return "Check " + UnresolvedPreparationCount + " requirement(s)";
                return "No preparation data";
            }
        }

        public string ActionSummary
        {
            get
            {
                List<string> parts = new List<string>(5);
                Add(parts, KillObjectiveCount, "kill");
                Add(parts, VisitObjectiveCount, "visit");
                Add(parts, PlantObjectiveCount, "mark/plant");
                Add(parts, FindObjectiveCount, "find");
                Add(parts, ExtractObjectiveCount, "extract");
                if (parts.Count == 0) return ObjectiveCount == 1 ? "1 raid task" : ObjectiveCount + " raid tasks";
                return string.Join(" • ", parts.ToArray());
            }
        }

        private static void Add(List<string> parts, int count, string label)
        {
            if (count <= 0) return;
            parts.Add(count + " " + label + (count == 1 ? string.Empty : "s"));
        }
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

        public PlannerRaidPlanCard BestForQuest(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId)) return null;
            for (int i = 0; i < Cards.Count; i++)
                if (Cards[i].SupportsQuest(questId)) return Cards[i];
            return null;
        }
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
                    plan.Preparation.ExactNeeds.ToArray(),
                    Count(plan, PlannerRaidObjectiveKind.Kill),
                    Count(plan, PlannerRaidObjectiveKind.Visit),
                    Count(plan, PlannerRaidObjectiveKind.Plant),
                    Count(plan, PlannerRaidObjectiveKind.Find),
                    Count(plan, PlannerRaidObjectiveKind.Extract),
                    BuildRankReason(collection.RankingMode, plan),
                    plan.QuestIds.ToArray()))
                .ToArray();

            return new PlannerRaidPlanViewModel(collection.GeneratedAtUnixSeconds, collection.RankingMode, cards);
        }

        private static int Count(PlannerRaidPlan plan, PlannerRaidObjectiveKind kind)
        {
            return plan.Objectives.Count(value => value != null && value.Kind == kind);
        }

        private static string BuildRankReason(PlannerRaidPlanRankingMode mode, PlannerRaidPlan plan)
        {
            if (mode == PlannerRaidPlanRankingMode.QuestDensityFirst)
                return plan.PreparationReady
                    ? "High quest density; preparation is already ready."
                    : "High quest density; preparation still needs attention.";

            if (plan.PreparationReady)
                return "Ready now; then ranked by useful quest and raid-task density.";
            if (plan.MissingBringTemplateCount > 0)
                return "Low preparation friction, then ranked by useful quest and raid-task density.";
            return "Best available preparation state, then ranked by useful quest and raid-task density.";
        }
    }
}
