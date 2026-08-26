using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTQuestPlanner.Client
{
    public enum PlannerCapabilityGoalPresentationKind
    {
        NoActionProven = 0,
        RaidDecision = 1,
        WaitingForAvailability = 2,
        EvidenceIncomplete = 3,
        ProgressionConflict = 4
    }

    public sealed class PlannerCapabilityGoalPresentation
    {
        public PlannerCapabilityGoalPresentation(
            PlannerCapabilityGoalPresentationKind kind,
            string capabilityId,
            string gateQuestId,
            PlannerCapabilitySupplyKind supplyKind,
            PlannerRaidDecisionPresentation raidDecision,
            IReadOnlyList<string> actionableQuestIds,
            IReadOnlyList<string> waitingQuestIds,
            IReadOnlyList<string> unknownQuestIds,
            string resultSummary,
            string caution)
        {
            Kind = kind;
            CapabilityId = capabilityId ?? string.Empty;
            GateQuestId = gateQuestId ?? string.Empty;
            SupplyKind = supplyKind;
            RaidDecision = raidDecision;
            ActionableQuestIds = actionableQuestIds ?? Array.Empty<string>();
            WaitingQuestIds = waitingQuestIds ?? Array.Empty<string>();
            UnknownQuestIds = unknownQuestIds ?? Array.Empty<string>();
            ResultSummary = resultSummary ?? string.Empty;
            Caution = caution ?? string.Empty;
        }

        public PlannerCapabilityGoalPresentationKind Kind { get; private set; }
        public string CapabilityId { get; private set; }
        public string GateQuestId { get; private set; }
        public PlannerCapabilitySupplyKind SupplyKind { get; private set; }
        public PlannerRaidDecisionPresentation RaidDecision { get; private set; }
        public IReadOnlyList<string> ActionableQuestIds { get; private set; }
        public IReadOnlyList<string> WaitingQuestIds { get; private set; }
        public IReadOnlyList<string> UnknownQuestIds { get; private set; }
        public string ResultSummary { get; private set; }
        public string Caution { get; private set; }
    }

    public static class PlannerCapabilityGoalPresentationBuilder
    {
        public static PlannerCapabilityGoalPresentation Build(
            PlannerCapabilityGoal goal,
            PlannerRaidDecisionPresentation raidDecision,
            PlannerRaidFocusDelayEvidence delayEvidence)
        {
            if (goal == null) throw new ArgumentNullException("goal");
            if (delayEvidence == null) throw new ArgumentNullException("delayEvidence");

            PlannerRaidDecisionIntent intent = goal.QuestIntent;
            string[] actionable = intent.FocusActionableQuestIds
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] unknown = intent.FocusEligibilityUnknownQuestIds
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] waiting = delayEvidence.PendingKnownQuestIds
                .Concat(delayEvidence.ElapsedPendingRefreshQuestIds)
                .Concat(delayEvidence.TimingUnresolvedQuestIds)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            PlannerCapabilityGoalPresentationKind kind;
            string caution = string.Empty;

            if (intent.HasTerminalFocusConflict)
            {
                kind = PlannerCapabilityGoalPresentationKind.ProgressionConflict;
                caution = "The selected capability path contains a prerequisite-state conflict; Planner cannot prove a valid route.";
            }
            else if (unknown.Length > 0)
            {
                kind = PlannerCapabilityGoalPresentationKind.EvidenceIncomplete;
                caution = "One or more prerequisite-ready quests lack authoritative eligibility evidence in the current snapshot.";
            }
            else if (actionable.Length > 0)
            {
                kind = PlannerCapabilityGoalPresentationKind.RaidDecision;
                if (raidDecision == null)
                    caution = "Actionable capability work exists, but no raid comparison was supplied for this presentation.";
            }
            else if (waiting.Length > 0)
            {
                kind = PlannerCapabilityGoalPresentationKind.WaitingForAvailability;
                caution = delayEvidence.ElapsedPendingRefreshQuestIds.Count > 0
                    ? "A configured delay has elapsed locally, but authoritative SPT state has not yet confirmed availability."
                    : "No raid action is required for the waiting prerequisite branch right now.";
            }
            else
            {
                kind = PlannerCapabilityGoalPresentationKind.NoActionProven;
                caution = "Planner cannot currently prove useful raid work for this capability goal.";
            }

            return new PlannerCapabilityGoalPresentation(
                kind,
                goal.Definition.CapabilityId,
                goal.Definition.GateQuestId,
                goal.Definition.SupplyKind,
                raidDecision,
                actionable,
                waiting,
                unknown,
                DescribeResult(goal.Definition),
                caution);
        }

        private static string DescribeResult(PlannerCapabilityGoalDefinition definition)
        {
            switch (definition.SupplyKind)
            {
                case PlannerCapabilitySupplyKind.BoundedRenewable:
                    List<string> limits = new List<string>();
                    if (definition.MaxUnitsPerReset.HasValue)
                        limits.Add(definition.MaxUnitsPerReset.Value + " units/reset");
                    if (definition.MaxAcquisitionsPerReset.HasValue)
                        limits.Add(definition.MaxAcquisitionsPerReset.Value + " acquisitions/reset");
                    return "Unlocks a bounded renewable capability" +
                           (limits.Count == 0 ? "." : " (" + string.Join(", ", limits) + ").");
                case PlannerCapabilitySupplyKind.OneTimeSample:
                    return "Grants a one-time sample; no renewable access is claimed.";
                case PlannerCapabilitySupplyKind.UnboundedRenewable:
                    return "Unlocks an unbounded renewable capability.";
                default:
                    return "Capability supply result is not proven.";
            }
        }
    }
}
