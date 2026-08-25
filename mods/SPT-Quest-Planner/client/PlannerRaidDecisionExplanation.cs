using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRaidDecisionExplanation
    {
        public PlannerRaidDecisionExplanation(
            string locationId,
            IReadOnlyList<string> progressionQuestIds,
            IReadOnlyList<PlannerRaidActionOverlap> overlaps,
            IReadOnlyList<string> immediateUnlockQuestIds,
            bool preparationReady,
            int missingPreparationTemplateCount,
            int unresolvedPreparationCount,
            double evidenceCoverage,
            IReadOnlyList<string> cautions)
        {
            LocationId = locationId ?? string.Empty;
            ProgressionQuestIds = progressionQuestIds ?? Array.Empty<string>();
            Overlaps = overlaps ?? Array.Empty<PlannerRaidActionOverlap>();
            ImmediateUnlockQuestIds = immediateUnlockQuestIds ?? Array.Empty<string>();
            PreparationReady = preparationReady;
            MissingPreparationTemplateCount = Math.Max(0, missingPreparationTemplateCount);
            UnresolvedPreparationCount = Math.Max(0, unresolvedPreparationCount);
            EvidenceCoverage = Math.Max(0d, Math.Min(1d, evidenceCoverage));
            Cautions = cautions ?? Array.Empty<string>();
        }

        public string LocationId { get; private set; }
        public IReadOnlyList<string> ProgressionQuestIds { get; private set; }
        public IReadOnlyList<PlannerRaidActionOverlap> Overlaps { get; private set; }
        public IReadOnlyList<string> ImmediateUnlockQuestIds { get; private set; }
        public bool PreparationReady { get; private set; }
        public int MissingPreparationTemplateCount { get; private set; }
        public int UnresolvedPreparationCount { get; private set; }
        public double EvidenceCoverage { get; private set; }
        public IReadOnlyList<string> Cautions { get; private set; }

        public bool HasCrossQuestSynergy { get { return Overlaps.Count > 0; } }
        public bool HasProgressionLeverage { get { return ImmediateUnlockQuestIds.Count > 0; } }
    }

    public static class PlannerRaidDecisionExplanationBuilder
    {
        public static PlannerRaidDecisionExplanation Build(
            string locationId,
            PlannerRaidDecisionSignals signals)
        {
            if (signals == null) throw new ArgumentNullException("signals");

            List<string> cautions = new List<string>();
            if (signals.RepeatableQuestCount > 0)
                cautions.Add(signals.RepeatableQuestCount + " repeatable quest(s) are context only, not primary progression evidence.");
            if (signals.UnknownObjectiveCount > 0)
                cautions.Add(signals.UnknownObjectiveCount + " objective(s) have unresolved semantics and are excluded from synergy claims.");
            if (!signals.PreparationReady)
                cautions.Add("Preparation is incomplete or unresolved.");

            return new PlannerRaidDecisionExplanation(
                locationId,
                signals.NonRepeatableQuestIds.ToArray(),
                signals.ActionOverlaps.ToArray(),
                signals.ImmediateUnlockQuestIds.ToArray(),
                signals.PreparationReady,
                signals.MissingPreparationTemplateCount,
                signals.UnresolvedPreparationCount,
                signals.EvidenceCoverage,
                cautions.ToArray());
        }
    }
}
