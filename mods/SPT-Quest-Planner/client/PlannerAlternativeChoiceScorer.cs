using System;
using System.Collections.Generic;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerAlternativeChoiceScore
    {
        public PlannerAlternativeChoiceScore(
            string templateId,
            double alreadyAllocated,
            double exactConflictRequired,
            double exactConflictOutstanding,
            int rank)
        {
            TemplateId = templateId ?? string.Empty;
            AlreadyAllocated = Math.Max(0d, alreadyAllocated);
            ExactConflictRequired = Math.Max(0d, exactConflictRequired);
            ExactConflictOutstanding = Math.Max(0d, exactConflictOutstanding);
            Rank = Math.Max(1, rank);
        }

        public string TemplateId { get; private set; }
        public double AlreadyAllocated { get; private set; }
        public double ExactConflictRequired { get; private set; }
        public double ExactConflictOutstanding { get; private set; }
        public int Rank { get; private set; }
    }

    public sealed class PlannerAlternativeChoiceScorer
    {
        private const int MaxCandidates = 128;
        private const double Epsilon = 0.000001d;

        public IReadOnlyList<PlannerAlternativeChoiceScore> Rank(
            PlannerAlternativeItemNeed alternative,
            PlannerPathItemPlan plan)
        {
            if (alternative == null) throw new ArgumentNullException("alternative");
            if (plan == null) throw new ArgumentNullException("plan");

            PlannerQuestItemRequirement requirement = alternative.Requirement;
            if (requirement.TemplateIds.Count > MaxCandidates)
                throw new InvalidOperationException("Alternative item condition exceeds bounded candidate limit of " + MaxCandidates + ".");

            Dictionary<string, double> allocated = new Dictionary<string, double>(StringComparer.Ordinal);
            for (int i = 0; i < alternative.Allocations.Count; i++)
            {
                PlannerTemplateAllocation value = alternative.Allocations[i];
                if (value == null || string.IsNullOrWhiteSpace(value.TemplateId)) continue;
                double current;
                allocated.TryGetValue(value.TemplateId, out current);
                allocated[value.TemplateId] = current + Math.Max(0d, value.Allocated);
            }

            List<MutableScore> scores = new List<MutableScore>(requirement.TemplateIds.Count);
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < requirement.TemplateIds.Count; i++)
            {
                string templateId = requirement.TemplateIds[i];
                if (string.IsNullOrWhiteSpace(templateId) || !seen.Add(templateId)) continue;

                double conflictRequired = 0d;
                double conflictOutstanding = 0d;
                for (int n = 0; n < plan.ExactNeeds.Count; n++)
                {
                    PlannerPathItemNeed need = plan.ExactNeeds[n];
                    if (!string.Equals(need.TemplateId, templateId, StringComparison.Ordinal)) continue;
                    conflictRequired += Math.Max(0d, need.Required);
                    conflictOutstanding += Math.Max(0d, need.Outstanding);
                }

                double alreadyAllocated;
                allocated.TryGetValue(templateId, out alreadyAllocated);
                scores.Add(new MutableScore(
                    templateId,
                    Math.Max(0d, alreadyAllocated),
                    conflictRequired,
                    conflictOutstanding));
            }

            scores.Sort(Compare);
            PlannerAlternativeChoiceScore[] result = new PlannerAlternativeChoiceScore[scores.Count];
            for (int i = 0; i < scores.Count; i++)
            {
                MutableScore score = scores[i];
                result[i] = new PlannerAlternativeChoiceScore(
                    score.TemplateId,
                    score.AlreadyAllocated,
                    score.ExactConflictRequired,
                    score.ExactConflictOutstanding,
                    i + 1);
            }
            return result;
        }

        private static int Compare(MutableScore a, MutableScore b)
        {
            int outstandingConflict = CompareDouble(a.ExactConflictOutstanding, b.ExactConflictOutstanding);
            if (outstandingConflict != 0) return outstandingConflict;

            int requiredConflict = CompareDouble(a.ExactConflictRequired, b.ExactConflictRequired);
            if (requiredConflict != 0) return requiredConflict;

            int allocated = CompareDouble(b.AlreadyAllocated, a.AlreadyAllocated);
            if (allocated != 0) return allocated;

            return string.Compare(a.TemplateId, b.TemplateId, StringComparison.Ordinal);
        }

        private static int CompareDouble(double a, double b)
        {
            double delta = a - b;
            if (Math.Abs(delta) <= Epsilon) return 0;
            return delta < 0d ? -1 : 1;
        }

        private sealed class MutableScore
        {
            public MutableScore(
                string templateId,
                double alreadyAllocated,
                double exactConflictRequired,
                double exactConflictOutstanding)
            {
                TemplateId = templateId;
                AlreadyAllocated = alreadyAllocated;
                ExactConflictRequired = exactConflictRequired;
                ExactConflictOutstanding = exactConflictOutstanding;
            }

            public string TemplateId;
            public double AlreadyAllocated;
            public double ExactConflictRequired;
            public double ExactConflictOutstanding;
        }
    }
}
