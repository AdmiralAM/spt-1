using System;
using System.Collections.Generic;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRoutePriority
    {
        public PlannerRoutePriority(
            string targetQuestId,
            int targetDisposition,
            int pathQuestCount,
            int immediateBlockerCount,
            double exactOutstanding,
            double alternativeOutstanding,
            double firOutstanding,
            bool fullyOwned,
            int rank)
        {
            TargetQuestId = targetQuestId ?? string.Empty;
            TargetDisposition = targetDisposition;
            PathQuestCount = Math.Max(0, pathQuestCount);
            ImmediateBlockerCount = Math.Max(0, immediateBlockerCount);
            ExactOutstanding = Math.Max(0d, exactOutstanding);
            AlternativeOutstanding = Math.Max(0d, alternativeOutstanding);
            FirOutstanding = Math.Max(0d, firOutstanding);
            FullyOwned = fullyOwned;
            Rank = Math.Max(1, rank);
        }

        public string TargetQuestId { get; private set; }
        public int TargetDisposition { get; private set; }
        public int PathQuestCount { get; private set; }
        public int ImmediateBlockerCount { get; private set; }
        public double ExactOutstanding { get; private set; }
        public double AlternativeOutstanding { get; private set; }
        public double TotalOutstanding { get { return ExactOutstanding + AlternativeOutstanding; } }
        public double FirOutstanding { get; private set; }
        public bool FullyOwned { get; private set; }
        public int Rank { get; private set; }
    }

    public sealed class PlannerRoutePrioritizer
    {
        private const int MaxCandidates = 256;
        private const int BlockedDisposition = 1;
        private const int ReachableDisposition = 2;
        private const int AvailableDisposition = 3;
        private const int ActiveDisposition = 4;
        private const int CompletedDisposition = 5;
        private const double Epsilon = 0.000001d;

        private readonly PlannerQueryEngine query;
        private readonly PlannerPathItemPlanner items;
        private readonly PlannerClientIndex state;

        public PlannerRoutePrioritizer(
            PlannerQueryEngine query,
            PlannerPathItemPlanner items,
            PlannerClientIndex state)
        {
            this.query = query ?? throw new ArgumentNullException("query");
            this.items = items ?? throw new ArgumentNullException("items");
            this.state = state ?? throw new ArgumentNullException("state");
        }

        public IReadOnlyList<PlannerRoutePriority> Rank(IEnumerable<string> targetQuestIds)
        {
            if (targetQuestIds == null) throw new ArgumentNullException("targetQuestIds");

            List<MutablePriority> values = new List<MutablePriority>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string targetQuestId in targetQuestIds)
            {
                if (string.IsNullOrWhiteSpace(targetQuestId) || !seen.Add(targetQuestId)) continue;
                PlannerQuestClientState questState = state.GetQuest(targetQuestId);
                if (questState == null) continue;
                if (values.Count >= MaxCandidates)
                    throw new InvalidOperationException("Quest route prioritization exceeds bounded candidate limit of " + MaxCandidates + ".");

                IReadOnlyList<string> path = query.GetIncompletePrerequisitePlan(targetQuestId);
                PlannerPathItemPlan itemPlan = items.BuildForTarget(targetQuestId);
                IReadOnlyList<string> blockers = query.GetImmediateBlockers(targetQuestId);

                double exactOutstanding = 0d;
                double alternativeOutstanding = 0d;
                double firOutstanding = 0d;
                for (int i = 0; i < itemPlan.ExactNeeds.Count; i++)
                {
                    PlannerPathItemNeed need = itemPlan.ExactNeeds[i];
                    double outstanding = Math.Max(0d, need.Outstanding);
                    exactOutstanding += outstanding;
                    if (need.FoundInRaid) firOutstanding += outstanding;
                }
                for (int i = 0; i < itemPlan.AlternativeNeeds.Count; i++)
                {
                    PlannerAlternativeItemNeed need = itemPlan.AlternativeNeeds[i];
                    double outstanding = Math.Max(0d, need.Outstanding);
                    alternativeOutstanding += outstanding;
                    if (need.Requirement.FoundInRaid) firOutstanding += outstanding;
                }

                values.Add(new MutablePriority(
                    targetQuestId,
                    questState.Disposition,
                    path.Count,
                    blockers.Count,
                    exactOutstanding,
                    alternativeOutstanding,
                    firOutstanding,
                    itemPlan.IsFullyOwned));
            }

            values.Sort(Compare);
            PlannerRoutePriority[] result = new PlannerRoutePriority[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                MutablePriority value = values[i];
                result[i] = new PlannerRoutePriority(
                    value.TargetQuestId,
                    value.TargetDisposition,
                    value.PathQuestCount,
                    value.ImmediateBlockerCount,
                    value.ExactOutstanding,
                    value.AlternativeOutstanding,
                    value.FirOutstanding,
                    value.FullyOwned,
                    i + 1);
            }
            return result;
        }

        private static int Compare(MutablePriority a, MutablePriority b)
        {
            int disposition = DispositionPriority(a.TargetDisposition).CompareTo(DispositionPriority(b.TargetDisposition));
            if (disposition != 0) return disposition;

            int blockers = a.ImmediateBlockerCount.CompareTo(b.ImmediateBlockerCount);
            if (blockers != 0) return blockers;

            int fullyOwned = b.FullyOwned.CompareTo(a.FullyOwned);
            if (fullyOwned != 0) return fullyOwned;

            int totalOutstanding = CompareDouble(a.TotalOutstanding, b.TotalOutstanding);
            if (totalOutstanding != 0) return totalOutstanding;

            int firOutstanding = CompareDouble(a.FirOutstanding, b.FirOutstanding);
            if (firOutstanding != 0) return firOutstanding;

            int pathLength = a.PathQuestCount.CompareTo(b.PathQuestCount);
            if (pathLength != 0) return pathLength;

            return string.Compare(a.TargetQuestId, b.TargetQuestId, StringComparison.Ordinal);
        }

        private static int DispositionPriority(int disposition)
        {
            switch (disposition)
            {
                case ActiveDisposition: return 0;
                case AvailableDisposition: return 1;
                case ReachableDisposition: return 2;
                case BlockedDisposition: return 3;
                case CompletedDisposition: return 5;
                default: return 4;
            }
        }

        private static int CompareDouble(double a, double b)
        {
            double delta = a - b;
            if (Math.Abs(delta) <= Epsilon) return 0;
            return delta < 0d ? -1 : 1;
        }

        private sealed class MutablePriority
        {
            public MutablePriority(
                string targetQuestId,
                int targetDisposition,
                int pathQuestCount,
                int immediateBlockerCount,
                double exactOutstanding,
                double alternativeOutstanding,
                double firOutstanding,
                bool fullyOwned)
            {
                TargetQuestId = targetQuestId;
                TargetDisposition = targetDisposition;
                PathQuestCount = pathQuestCount;
                ImmediateBlockerCount = immediateBlockerCount;
                ExactOutstanding = exactOutstanding;
                AlternativeOutstanding = alternativeOutstanding;
                FirOutstanding = firOutstanding;
                FullyOwned = fullyOwned;
            }

            public string TargetQuestId;
            public int TargetDisposition;
            public int PathQuestCount;
            public int ImmediateBlockerCount;
            public double ExactOutstanding;
            public double AlternativeOutstanding;
            public double TotalOutstanding { get { return ExactOutstanding + AlternativeOutstanding; } }
            public double FirOutstanding;
            public bool FullyOwned;
        }
    }
}
