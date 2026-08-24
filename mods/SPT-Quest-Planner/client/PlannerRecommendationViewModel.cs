using System;
using System.Collections.Generic;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRecommendationViewModel
    {
        private const int DispositionBlocked = 1;
        private const int DispositionReachable = 2;
        private const int DispositionAvailable = 3;
        private const int DispositionActive = 4;

        public PlannerRecommendationViewModel(
            int rank,
            string questId,
            string questName,
            string traderId,
            int disposition,
            IReadOnlyList<string> reasons,
            IReadOnlyList<string> blockerQuestIds,
            IReadOnlyList<string> blockerQuestNames,
            IReadOnlyList<string> immediateUnlockQuestIds,
            IReadOnlyList<string> immediateUnlockQuestNames,
            int pathQuestCount,
            double totalOutstanding,
            double firOutstanding,
            bool fullyOwned)
        {
            Rank = Math.Max(1, rank);
            QuestId = questId ?? string.Empty;
            QuestName = string.IsNullOrWhiteSpace(questName) ? QuestId : questName;
            TraderId = traderId ?? string.Empty;
            Disposition = disposition;
            Reasons = reasons ?? Array.Empty<string>();
            BlockerQuestIds = blockerQuestIds ?? Array.Empty<string>();
            BlockerQuestNames = blockerQuestNames ?? Array.Empty<string>();
            ImmediateUnlockQuestIds = immediateUnlockQuestIds ?? Array.Empty<string>();
            ImmediateUnlockQuestNames = immediateUnlockQuestNames ?? Array.Empty<string>();
            PathQuestCount = Math.Max(0, pathQuestCount);
            TotalOutstanding = Math.Max(0d, totalOutstanding);
            FirOutstanding = Math.Max(0d, firOutstanding);
            FullyOwned = fullyOwned;
        }

        public int Rank { get; private set; }
        public string QuestId { get; private set; }
        public string QuestName { get; private set; }
        public string TraderId { get; private set; }
        public int Disposition { get; private set; }
        public IReadOnlyList<string> Reasons { get; private set; }
        public IReadOnlyList<string> BlockerQuestIds { get; private set; }
        public IReadOnlyList<string> BlockerQuestNames { get; private set; }
        public IReadOnlyList<string> ImmediateUnlockQuestIds { get; private set; }
        public IReadOnlyList<string> ImmediateUnlockQuestNames { get; private set; }
        public int PathQuestCount { get; private set; }
        public double TotalOutstanding { get; private set; }
        public double FirOutstanding { get; private set; }
        public bool FullyOwned { get; private set; }
        public int ImmediateBlockerCount { get { return BlockerQuestIds.Count; } }
        public int ImmediateUnlockCount { get { return ImmediateUnlockQuestIds.Count; } }
        public bool IsActive { get { return Disposition == DispositionActive; } }
        public bool IsAvailable { get { return Disposition == DispositionAvailable; } }

        public string StateLabel
        {
            get
            {
                switch (Disposition)
                {
                    case DispositionActive: return "ACTIVE";
                    case DispositionAvailable: return "AVAILABLE";
                    case DispositionReachable: return "LATER";
                    case DispositionBlocked: return "BLOCKED";
                    default: return "UNKNOWN";
                }
            }
        }

        public string ActionSummary
        {
            get
            {
                if (ImmediateBlockerCount > 0) return "Clear " + ImmediateBlockerCount + " blocker(s) first";
                if (!FullyOwned)
                    return "Need " + Format(TotalOutstanding) + " item(s)" + (FirOutstanding > 0d ? ", FIR " + Format(FirOutstanding) : string.Empty);
                if (IsAvailable) return "Accept quest; item burden ready";
                if (IsActive) return "Ready to push now";
                return "Route is currently actionable";
            }
        }

        private static string Format(double value)
        {
            double rounded = Math.Round(Math.Max(0d, value));
            return Math.Abs(value - rounded) < 0.000001d ? rounded.ToString("0") : value.ToString("0.##");
        }
    }

    public sealed class PlannerRecommendationViewModelBuilder
    {
        private const int MaxViewModels = 32;

        private readonly PlannerTopologyIndex topology;
        private readonly PlannerLocaleIndex locale;
        private readonly PlannerQueryEngine query;

        public PlannerRecommendationViewModelBuilder(
            PlannerTopologyIndex topology,
            PlannerLocaleIndex locale,
            PlannerQueryEngine query)
        {
            this.topology = topology ?? throw new ArgumentNullException("topology");
            this.locale = locale;
            this.query = query ?? throw new ArgumentNullException("query");
        }

        public IReadOnlyList<PlannerRecommendationViewModel> Build(IReadOnlyList<PlannerRecommendation> recommendations)
        {
            if (recommendations == null) throw new ArgumentNullException("recommendations");
            if (recommendations.Count > MaxViewModels)
                throw new InvalidOperationException("Recommendation presentation exceeds bounded view-model limit of " + MaxViewModels + ".");

            PlannerRecommendationViewModel[] result = new PlannerRecommendationViewModel[recommendations.Count];
            for (int i = 0; i < recommendations.Count; i++) result[i] = BuildOne(recommendations[i]);
            return result;
        }

        private PlannerRecommendationViewModel BuildOne(PlannerRecommendation recommendation)
        {
            if (recommendation == null) throw new ArgumentNullException("recommendation");
            PlannerRoutePriority route = recommendation.Route;
            PlannerTopologyQuest quest = topology.GetQuest(route.TargetQuestId);

            IReadOnlyList<string> blockers = query.GetImmediateBlockers(route.TargetQuestId);
            IReadOnlyList<string> unlocks = query.GetImmediateUnlocksIfCompleted(route.TargetQuestId);

            return new PlannerRecommendationViewModel(
                route.Rank,
                route.TargetQuestId,
                PlannerQuestLabels.Resolve(topology, locale, route.TargetQuestId),
                quest == null ? string.Empty : quest.TraderId,
                route.TargetDisposition,
                recommendation.Reasons,
                Copy(blockers),
                ResolveNames(blockers),
                Copy(unlocks),
                ResolveNames(unlocks),
                route.PathQuestCount,
                route.TotalOutstanding,
                route.FirOutstanding,
                route.FullyOwned);
        }

        private string[] ResolveNames(IReadOnlyList<string> questIds)
        {
            string[] values = new string[questIds.Count];
            for (int i = 0; i < questIds.Count; i++)
                values[i] = PlannerQuestLabels.Resolve(topology, locale, questIds[i]);
            return values;
        }

        private static string[] Copy(IReadOnlyList<string> values)
        {
            string[] result = new string[values.Count];
            for (int i = 0; i < values.Count; i++) result[i] = values[i];
            return result;
        }
    }
}
