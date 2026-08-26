using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerCapabilityDecisionSnapshot
    {
        public PlannerCapabilityDecisionSnapshot(
            string capabilityId,
            string gateQuestId,
            PlannerCapabilityGoalPresentationKind state,
            PlannerCapabilityDecisionValueKind decisionValue,
            bool countsTowardKeepCandidate,
            string primaryLocationId,
            IReadOnlyList<string> alternativeLocationIds,
            IReadOnlyList<string> actionableQuestIds,
            IReadOnlyList<string> waitingQuestIds,
            IReadOnlyList<string> unknownQuestIds,
            string resultSummary,
            string caution,
            IReadOnlyList<string> decisionEvidence)
        {
            CapabilityId = capabilityId ?? string.Empty;
            GateQuestId = gateQuestId ?? string.Empty;
            State = state;
            DecisionValue = decisionValue;
            CountsTowardKeepCandidate = countsTowardKeepCandidate;
            PrimaryLocationId = primaryLocationId ?? string.Empty;
            AlternativeLocationIds = Normalize(alternativeLocationIds);
            ActionableQuestIds = Normalize(actionableQuestIds);
            WaitingQuestIds = Normalize(waitingQuestIds);
            UnknownQuestIds = Normalize(unknownQuestIds);
            ResultSummary = resultSummary ?? string.Empty;
            Caution = caution ?? string.Empty;
            DecisionEvidence = Normalize(decisionEvidence);
        }

        public string CapabilityId { get; private set; }
        public string GateQuestId { get; private set; }
        public PlannerCapabilityGoalPresentationKind State { get; private set; }
        public PlannerCapabilityDecisionValueKind DecisionValue { get; private set; }
        public bool CountsTowardKeepCandidate { get; private set; }
        public string PrimaryLocationId { get; private set; }
        public IReadOnlyList<string> AlternativeLocationIds { get; private set; }
        public IReadOnlyList<string> ActionableQuestIds { get; private set; }
        public IReadOnlyList<string> WaitingQuestIds { get; private set; }
        public IReadOnlyList<string> UnknownQuestIds { get; private set; }
        public string ResultSummary { get; private set; }
        public string Caution { get; private set; }
        public IReadOnlyList<string> DecisionEvidence { get; private set; }

        public bool HasPrimaryRaid { get { return !string.IsNullOrWhiteSpace(PrimaryLocationId); } }
        public bool HasAlternatives { get { return AlternativeLocationIds.Count > 0; } }

        private static IReadOnlyList<string> Normalize(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0) return Array.Empty<string>();
            return values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public static class PlannerCapabilityDecisionSnapshotBuilder
    {
        public static PlannerCapabilityDecisionSnapshot Build(PlannerCapabilityGoalPresentation presentation)
        {
            if (presentation == null) throw new ArgumentNullException("presentation");

            PlannerCapabilityDecisionValue value = PlannerCapabilityDecisionValueClassifier.Classify(presentation);
            PlannerRaidDecisionPresentation raid = presentation.RaidDecision;

            string primary = string.Empty;
            List<string> alternatives = new List<string>();

            if (raid != null)
            {
                if (raid.Primary != null)
                    primary = raid.Primary.LocationId ?? string.Empty;

                if (raid.Alternatives != null)
                {
                    for (int i = 0; i < raid.Alternatives.Count; i++)
                    {
                        PlannerRaidDecisionExplanation explanation = raid.Alternatives[i];
                        if (explanation == null || string.IsNullOrWhiteSpace(explanation.LocationId)) continue;
                        if (!string.IsNullOrWhiteSpace(primary) && string.Equals(primary, explanation.LocationId, StringComparison.Ordinal)) continue;
                        alternatives.Add(explanation.LocationId);
                    }
                }
            }

            return new PlannerCapabilityDecisionSnapshot(
                presentation.CapabilityId,
                presentation.GateQuestId,
                presentation.Kind,
                value.Kind,
                value.CountsTowardKeepCandidate,
                primary,
                alternatives,
                presentation.ActionableQuestIds,
                presentation.WaitingQuestIds,
                presentation.UnknownQuestIds,
                presentation.ResultSummary,
                presentation.Caution,
                value.Evidence);
        }
    }
}
