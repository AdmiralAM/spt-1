using System;
using System.Collections.Generic;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRaidDecisionDelta
    {
        public PlannerRaidDecisionDelta(
            int nonRepeatableQuestDelta,
            int overlapGroupDelta,
            int maxOverlapQuestDelta,
            int immediateUnlockDelta,
            int missingPreparationDelta,
            int unresolvedPreparationDelta,
            double repeatableShareDelta,
            double evidenceCoverageDelta,
            IReadOnlyList<string> evidence)
        {
            NonRepeatableQuestDelta = nonRepeatableQuestDelta;
            OverlapGroupDelta = overlapGroupDelta;
            MaxOverlapQuestDelta = maxOverlapQuestDelta;
            ImmediateUnlockDelta = immediateUnlockDelta;
            MissingPreparationDelta = missingPreparationDelta;
            UnresolvedPreparationDelta = unresolvedPreparationDelta;
            RepeatableShareDelta = repeatableShareDelta;
            EvidenceCoverageDelta = evidenceCoverageDelta;
            Evidence = evidence ?? Array.Empty<string>();
        }

        // Positive progression deltas favour the candidate on the left.
        public int NonRepeatableQuestDelta { get; private set; }
        public int OverlapGroupDelta { get; private set; }
        public int MaxOverlapQuestDelta { get; private set; }
        public int ImmediateUnlockDelta { get; private set; }

        // Positive friction delta means the left candidate has more friction.
        public int MissingPreparationDelta { get; private set; }
        public int UnresolvedPreparationDelta { get; private set; }
        public double RepeatableShareDelta { get; private set; }

        // Positive coverage delta means the left candidate is better understood.
        public double EvidenceCoverageDelta { get; private set; }
        public IReadOnlyList<string> Evidence { get; private set; }

        public bool HasMeaningfulProvenDifference
        {
            get
            {
                return NonRepeatableQuestDelta != 0 ||
                       OverlapGroupDelta != 0 ||
                       MaxOverlapQuestDelta != 0 ||
                       ImmediateUnlockDelta != 0 ||
                       MissingPreparationDelta != 0 ||
                       UnresolvedPreparationDelta != 0 ||
                       Math.Abs(RepeatableShareDelta) > 0.000001d ||
                       Math.Abs(EvidenceCoverageDelta) > 0.000001d;
            }
        }
    }

    public static class PlannerRaidDecisionDeltaBuilder
    {
        public static PlannerRaidDecisionDelta Compare(
            PlannerRaidDecisionSignals left,
            PlannerRaidDecisionSignals right)
        {
            if (left == null) throw new ArgumentNullException("left");
            if (right == null) throw new ArgumentNullException("right");

            int nonRepeatable = left.NonRepeatableQuestCount - right.NonRepeatableQuestCount;
            int overlapGroups = left.CrossQuestOverlapGroupCount - right.CrossQuestOverlapGroupCount;
            int maxOverlap = left.MaxOverlappingQuestCount - right.MaxOverlappingQuestCount;
            int unlocks = left.ImmediateUnlockCount - right.ImmediateUnlockCount;
            int missing = left.MissingPreparationTemplateCount - right.MissingPreparationTemplateCount;
            int unresolved = left.UnresolvedPreparationCount - right.UnresolvedPreparationCount;
            double repeatableShare = left.RepeatableShare - right.RepeatableShare;
            double coverage = left.EvidenceCoverage - right.EvidenceCoverage;

            List<string> evidence = new List<string>(8);
            AddCountEvidence(evidence, nonRepeatable, "active non-repeatable quest", higherIsBetter: true);
            AddCountEvidence(evidence, overlapGroups, "cross-quest overlap group", higherIsBetter: true);
            AddCountEvidence(evidence, maxOverlap, "quest in the strongest shared action", higherIsBetter: true);
            AddCountEvidence(evidence, unlocks, "immediate downstream unlock", higherIsBetter: true);
            AddCountEvidence(evidence, missing, "missing preparation item type", higherIsBetter: false);
            AddCountEvidence(evidence, unresolved, "unresolved preparation requirement", higherIsBetter: false);
            AddRatioEvidence(evidence, repeatableShare, "repeatable share", higherIsBetter: false);
            AddRatioEvidence(evidence, coverage, "evidence coverage", higherIsBetter: true);

            return new PlannerRaidDecisionDelta(
                nonRepeatable,
                overlapGroups,
                maxOverlap,
                unlocks,
                missing,
                unresolved,
                repeatableShare,
                coverage,
                evidence.ToArray());
        }

        private static void AddCountEvidence(
            List<string> evidence,
            int delta,
            string label,
            bool higherIsBetter)
        {
            if (delta == 0) return;
            bool favoursLeft = higherIsBetter ? delta > 0 : delta < 0;
            evidence.Add(
                (favoursLeft ? "LEFT: " : "RIGHT: ") +
                Math.Abs(delta) + " " + label + (Math.Abs(delta) == 1 ? string.Empty : "s"));
        }

        private static void AddRatioEvidence(
            List<string> evidence,
            double delta,
            string label,
            bool higherIsBetter)
        {
            if (Math.Abs(delta) <= 0.000001d) return;
            bool favoursLeft = higherIsBetter ? delta > 0d : delta < 0d;
            evidence.Add(
                (favoursLeft ? "LEFT: " : "RIGHT: ") +
                label + " delta " + Math.Abs(delta).ToString("0.###"));
        }
    }
}
