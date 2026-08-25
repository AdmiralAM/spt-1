using System;
using System.Collections.Generic;

namespace SPTQuestPlanner.Client
{
    public enum PlannerRaidDecisionOutcome
    {
        Abstain = 0,
        PreferLeft = 1,
        PreferRight = 2
    }

    public sealed class PlannerRaidDecision
    {
        public PlannerRaidDecision(
            PlannerRaidDecisionOutcome outcome,
            string reason,
            IReadOnlyList<string> evidence)
        {
            Outcome = outcome;
            Reason = reason ?? string.Empty;
            Evidence = evidence ?? Array.Empty<string>();
        }

        public PlannerRaidDecisionOutcome Outcome { get; private set; }
        public string Reason { get; private set; }
        public IReadOnlyList<string> Evidence { get; private set; }
        public bool HasRecommendation { get { return Outcome != PlannerRaidDecisionOutcome.Abstain; } }
    }

    public static class PlannerRaidDecisionPolicy
    {
        private const double CoverageEpsilon = 0.000001d;

        public static PlannerRaidDecision Decide(
            PlannerRaidDecisionSignals left,
            PlannerRaidDecisionSignals right)
        {
            if (left == null) throw new ArgumentNullException("left");
            if (right == null) throw new ArgumentNullException("right");

            PlannerRaidDecisionDelta delta = PlannerRaidDecisionDeltaBuilder.Compare(left, right);
            List<string> evidence = new List<string>(delta.Evidence);

            int leftAdvantages = 0;
            int rightAdvantages = 0;

            CompareBenefit(left.NonRepeatableQuestCount, right.NonRepeatableQuestCount, ref leftAdvantages, ref rightAdvantages);
            CompareBenefit(left.CrossQuestOverlapGroupCount, right.CrossQuestOverlapGroupCount, ref leftAdvantages, ref rightAdvantages);
            CompareBenefit(left.MaxOverlappingQuestCount, right.MaxOverlappingQuestCount, ref leftAdvantages, ref rightAdvantages);
            CompareBenefit(left.ImmediateUnlockCount, right.ImmediateUnlockCount, ref leftAdvantages, ref rightAdvantages);
            CompareFriction(left.MissingPreparationTemplateCount, right.MissingPreparationTemplateCount, ref leftAdvantages, ref rightAdvantages);
            CompareFriction(left.UnresolvedPreparationCount, right.UnresolvedPreparationCount, ref leftAdvantages, ref rightAdvantages);
            CompareCoverage(left.EvidenceCoverage, right.EvidenceCoverage, ref leftAdvantages, ref rightAdvantages);

            if (leftAdvantages == 0 && rightAdvantages == 0)
            {
                return new PlannerRaidDecision(
                    PlannerRaidDecisionOutcome.Abstain,
                    "No meaningful proven difference between the candidates.",
                    evidence.ToArray());
            }

            if (leftAdvantages > 0 && rightAdvantages == 0)
            {
                return new PlannerRaidDecision(
                    PlannerRaidDecisionOutcome.PreferLeft,
                    "Left candidate has a proven advantage without a competing disadvantage in the decision model.",
                    evidence.ToArray());
            }

            if (rightAdvantages > 0 && leftAdvantages == 0)
            {
                return new PlannerRaidDecision(
                    PlannerRaidDecisionOutcome.PreferRight,
                    "Right candidate has a proven advantage without a competing disadvantage in the decision model.",
                    evidence.ToArray());
            }

            return new PlannerRaidDecision(
                PlannerRaidDecisionOutcome.Abstain,
                "Candidates have competing proven advantages; player preference should decide until a policy-specific priority is justified.",
                evidence.ToArray());
        }

        private static void CompareBenefit(int left, int right, ref int leftAdvantages, ref int rightAdvantages)
        {
            if (left > right) leftAdvantages++;
            else if (right > left) rightAdvantages++;
        }

        private static void CompareFriction(int left, int right, ref int leftAdvantages, ref int rightAdvantages)
        {
            if (left < right) leftAdvantages++;
            else if (right < left) rightAdvantages++;
        }

        private static void CompareCoverage(double left, double right, ref int leftAdvantages, ref int rightAdvantages)
        {
            double delta = left - right;
            if (Math.Abs(delta) <= CoverageEpsilon) return;
            if (delta > 0d) leftAdvantages++;
            else rightAdvantages++;
        }
    }
}
