using System;
using System.Collections.Generic;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRecommendation
    {
        public PlannerRecommendation(
            PlannerRoutePriority route,
            IReadOnlyList<string> reasons)
        {
            Route = route ?? throw new ArgumentNullException("route");
            Reasons = reasons ?? Array.Empty<string>();
        }

        public PlannerRoutePriority Route { get; private set; }
        public IReadOnlyList<string> Reasons { get; private set; }
    }

    public sealed class PlannerRecommendationEngine
    {
        private const int MaxRecommendations = 32;
        private const int ActiveDisposition = 4;
        private const int AvailableDisposition = 3;
        private const int ReachableDisposition = 2;
        private const int BlockedDisposition = 1;

        private readonly PlannerCandidateSelector selector;
        private readonly PlannerRoutePrioritizer prioritizer;

        public PlannerRecommendationEngine(
            PlannerCandidateSelector selector,
            PlannerRoutePrioritizer prioritizer)
        {
            this.selector = selector ?? throw new ArgumentNullException("selector");
            this.prioritizer = prioritizer ?? throw new ArgumentNullException("prioritizer");
        }

        public IReadOnlyList<PlannerRecommendation> Recommend(
            int topN = 5,
            PlannerCandidatePolicy policy = null)
        {
            if (topN < 1 || topN > MaxRecommendations)
                throw new ArgumentOutOfRangeException("topN", "Recommendation count must be between 1 and " + MaxRecommendations + ".");

            IReadOnlyList<string> candidates = selector.Select(policy);
            IReadOnlyList<PlannerRoutePriority> ranked = prioritizer.Rank(candidates);
            int count = Math.Min(topN, ranked.Count);
            PlannerRecommendation[] result = new PlannerRecommendation[count];
            for (int i = 0; i < count; i++)
                result[i] = new PlannerRecommendation(ranked[i], Explain(ranked[i]));
            return result;
        }

        private static IReadOnlyList<string> Explain(PlannerRoutePriority route)
        {
            List<string> reasons = new List<string>(6);
            reasons.Add(DispositionReason(route.TargetDisposition));

            if (route.ImmediateBlockerCount == 0)
                reasons.Add("No immediate prerequisite blockers.");
            else
                reasons.Add(route.ImmediateBlockerCount + " immediate prerequisite blocker(s).");

            if (route.FullyOwned)
                reasons.Add("All currently known route item requirements are already owned.");
            else
                reasons.Add(Format(route.TotalOutstanding) + " item(s) still outstanding for the analyzed route.");

            if (route.FirOutstanding > 0d)
                reasons.Add(Format(route.FirOutstanding) + " outstanding item(s) require Found in Raid status.");
            else
                reasons.Add("No outstanding FIR burden in the analyzed route.");

            if (route.PathQuestCount <= 1)
                reasons.Add("Target is immediate or has no incomplete prerequisite chain.");
            else
                reasons.Add(route.PathQuestCount + " incomplete quest(s) in the prerequisite plan.");

            return reasons.ToArray();
        }

        private static string DispositionReason(int disposition)
        {
            switch (disposition)
            {
                case ActiveDisposition: return "Quest is active now.";
                case AvailableDisposition: return "Quest is available to start.";
                case ReachableDisposition: return "Quest is reachable through current progression.";
                case BlockedDisposition: return "Quest is currently blocked.";
                default: return "Quest state is not classified for recommendation context.";
            }
        }

        private static string Format(double value)
        {
            return Math.Round(Math.Max(0d, value), 3).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
