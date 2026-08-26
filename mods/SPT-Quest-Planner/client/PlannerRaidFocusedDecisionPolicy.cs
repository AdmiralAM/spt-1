using System;
using System.Collections.Generic;

namespace SPTQuestPlanner.Client
{
    public static class PlannerRaidFocusedDecisionPolicy
    {
        private const double CoverageEpsilon = 0.000001d;

        public static PlannerRaidDecision Decide(
            PlannerRaidDecisionSignals left,
            PlannerRaidDecisionSignals right,
            PlannerRaidDecisionIntent intent)
        {
            if (left == null) throw new ArgumentNullException("left");
            if (right == null) throw new ArgumentNullException("right");

            if (intent == null || !intent.HasFocusQuest)
                return PlannerRaidDecisionPolicy.Decide(left, right);

            PlannerRaidFocusEvidence leftEvidence = PlannerRaidFocusEvidenceBuilder.Build(left, intent);
            PlannerRaidFocusEvidence rightEvidence = PlannerRaidFocusEvidenceBuilder.Build(right, intent);

            if (leftEvidence.AdvancesActionableFocus != rightEvidence.AdvancesActionableFocus)
            {
                bool preferLeft = leftEvidence.AdvancesActionableFocus;
                return new PlannerRaidDecision(
                    preferLeft ? PlannerRaidDecisionOutcome.PreferLeft : PlannerRaidDecisionOutcome.PreferRight,
                    "Player progression focus selects the candidate that advances the actionable focused frontier.",
                    new[]
                    {
                        (preferLeft ? "LEFT" : "RIGHT") + ": advances actionable focused quest(s): " +
                        string.Join(", ", preferLeft ? leftEvidence.MatchedActionableQuestIds : rightEvidence.MatchedActionableQuestIds)
                    });
            }

            // If neither candidate advances the focused frontier, focus provides no evidence and the
            // ordinary conservative decision policy remains valid.
            if (!leftEvidence.AdvancesActionableFocus)
                return PlannerRaidDecisionPolicy.Decide(left, right);

            int leftAdvantages = 0;
            int rightAdvantages = 0;
            List<string> evidence = new List<string>();

            CompareBenefit(
                leftEvidence.MatchedActionableQuestIds.Count,
                rightEvidence.MatchedActionableQuestIds.Count,
                "actionable focused quest",
                evidence,
                ref leftAdvantages,
                ref rightAdvantages);
            CompareBenefit(
                CountFocusedOverlapGroups(left, leftEvidence),
                CountFocusedOverlapGroups(right, rightEvidence),
                "focused cross-quest overlap group",
                evidence,
                ref leftAdvantages,
                ref rightAdvantages);
            CompareBenefit(
                leftEvidence.FocusedImmediateUnlockQuestIds.Count,
                rightEvidence.FocusedImmediateUnlockQuestIds.Count,
                "focused-path immediate unlock",
                evidence,
                ref leftAdvantages,
                ref rightAdvantages);
            CompareFriction(
                left.MissingPreparationTemplateCount,
                right.MissingPreparationTemplateCount,
                "missing preparation item type",
                evidence,
                ref leftAdvantages,
                ref rightAdvantages);
            CompareFriction(
                left.UnresolvedPreparationCount,
                right.UnresolvedPreparationCount,
                "unresolved preparation requirement",
                evidence,
                ref leftAdvantages,
                ref rightAdvantages);
            CompareCoverage(
                left.EvidenceCoverage,
                right.EvidenceCoverage,
                evidence,
                ref leftAdvantages,
                ref rightAdvantages);

            if (leftAdvantages > 0 && rightAdvantages == 0)
            {
                return new PlannerRaidDecision(
                    PlannerRaidDecisionOutcome.PreferLeft,
                    "Left candidate conservatively dominates the other candidate within the selected progression focus.",
                    evidence.ToArray());
            }

            if (rightAdvantages > 0 && leftAdvantages == 0)
            {
                return new PlannerRaidDecision(
                    PlannerRaidDecisionOutcome.PreferRight,
                    "Right candidate conservatively dominates the other candidate within the selected progression focus.",
                    evidence.ToArray());
            }

            if (leftAdvantages > 0 && rightAdvantages > 0)
            {
                return new PlannerRaidDecision(
                    PlannerRaidDecisionOutcome.Abstain,
                    "Both candidates advance the selected progression focus but have competing proven focused advantages.",
                    evidence.ToArray());
            }

            // Focus evidence is equivalent. General progression evidence may then act as a secondary
            // conservative tie-breaker without contradicting the explicit player goal.
            return PlannerRaidDecisionPolicy.Decide(left, right);
        }

        private static int CountFocusedOverlapGroups(
            PlannerRaidDecisionSignals signals,
            PlannerRaidFocusEvidence focusEvidence)
        {
            if (focusEvidence.MatchedActionableQuestIds.Count == 0) return 0;
            HashSet<string> focused = new HashSet<string>(focusEvidence.MatchedActionableQuestIds, StringComparer.Ordinal);
            int count = 0;
            for (int i = 0; i < signals.ActionOverlaps.Count; i++)
            {
                PlannerRaidActionOverlap overlap = signals.ActionOverlaps[i];
                for (int q = 0; q < overlap.QuestIds.Count; q++)
                {
                    if (!focused.Contains(overlap.QuestIds[q])) continue;
                    count++;
                    break;
                }
            }
            return count;
        }

        private static void CompareBenefit(
            int left,
            int right,
            string label,
            List<string> evidence,
            ref int leftAdvantages,
            ref int rightAdvantages)
        {
            if (left == right) return;
            bool preferLeft = left > right;
            if (preferLeft) leftAdvantages++;
            else rightAdvantages++;
            evidence.Add((preferLeft ? "LEFT" : "RIGHT") + ": " + Math.Max(left, right) + " vs " + Math.Min(left, right) + " " + label + "(s)");
        }

        private static void CompareFriction(
            int left,
            int right,
            string label,
            List<string> evidence,
            ref int leftAdvantages,
            ref int rightAdvantages)
        {
            if (left == right) return;
            bool preferLeft = left < right;
            if (preferLeft) leftAdvantages++;
            else rightAdvantages++;
            evidence.Add((preferLeft ? "LEFT" : "RIGHT") + ": lower " + label + " count (" + Math.Min(left, right) + " vs " + Math.Max(left, right) + ")");
        }

        private static void CompareCoverage(
            double left,
            double right,
            List<string> evidence,
            ref int leftAdvantages,
            ref int rightAdvantages)
        {
            double delta = left - right;
            if (Math.Abs(delta) <= CoverageEpsilon) return;
            bool preferLeft = delta > 0d;
            if (preferLeft) leftAdvantages++;
            else rightAdvantages++;
            evidence.Add((preferLeft ? "LEFT" : "RIGHT") + ": higher evidence coverage within the candidate profile");
        }
    }
}
