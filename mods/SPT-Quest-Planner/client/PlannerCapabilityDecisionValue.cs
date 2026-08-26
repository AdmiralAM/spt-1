using System;
using System.Collections.Generic;

namespace SPTQuestPlanner.Client
{
    public enum PlannerCapabilityDecisionValueKind
    {
        NavigationOnly = 0,
        DecisionChanged = 1,
        TradeoffClarified = 2,
        UnnecessaryRaidAvoided = 3,
        GoalAlreadyResolved = 4,
        UnsupportedDecisionPrevented = 5
    }

    public sealed class PlannerCapabilityDecisionValue
    {
        public PlannerCapabilityDecisionValue(
            PlannerCapabilityDecisionValueKind kind,
            bool provesBeyondPrerequisiteNavigation,
            bool countsTowardKeepCandidate,
            IReadOnlyList<string> evidence)
        {
            Kind = kind;
            ProvesBeyondPrerequisiteNavigation = provesBeyondPrerequisiteNavigation;
            CountsTowardKeepCandidate = countsTowardKeepCandidate;
            Evidence = evidence ?? Array.Empty<string>();
        }

        public PlannerCapabilityDecisionValueKind Kind { get; private set; }
        public bool ProvesBeyondPrerequisiteNavigation { get; private set; }
        public bool CountsTowardKeepCandidate { get; private set; }
        public IReadOnlyList<string> Evidence { get; private set; }
    }

    public static class PlannerCapabilityDecisionValueClassifier
    {
        public static PlannerCapabilityDecisionValue Classify(PlannerCapabilityGoalPresentation presentation)
        {
            if (presentation == null) throw new ArgumentNullException("presentation");

            switch (presentation.Kind)
            {
                case PlannerCapabilityGoalPresentationKind.CapabilityAlreadyUnlocked:
                    return Value(
                        PlannerCapabilityDecisionValueKind.GoalAlreadyResolved,
                        true,
                        false,
                        "Planner proves the selected capability gate is already completed and avoids redundant progression work, but this correctness alone does not justify keeping the mod.");

                case PlannerCapabilityGoalPresentationKind.WaitingForAvailability:
                    return Value(
                        PlannerCapabilityDecisionValueKind.UnnecessaryRaidAvoided,
                        true,
                        true,
                        "The selected progression path is waiting on availability; Planner prevents a pointless raid for that goal right now.");

                case PlannerCapabilityGoalPresentationKind.EvidenceIncomplete:
                case PlannerCapabilityGoalPresentationKind.ProgressionConflict:
                    return Value(
                        PlannerCapabilityDecisionValueKind.UnsupportedDecisionPrevented,
                        true,
                        false,
                        "Planner refuses to fabricate a route when authoritative progression evidence is incomplete or contradictory; this is required correctness, not sufficient KEEP evidence.");

                case PlannerCapabilityGoalPresentationKind.RaidDecision:
                    return ClassifyRaidDecision(presentation.RaidDecision);

                default:
                    return Value(
                        PlannerCapabilityDecisionValueKind.NavigationOnly,
                        false,
                        false,
                        "No decision value beyond locating the current prerequisite has been proven.");
            }
        }

        private static PlannerCapabilityDecisionValue ClassifyRaidDecision(PlannerRaidDecisionPresentation raid)
        {
            if (raid == null)
                return Value(
                    PlannerCapabilityDecisionValueKind.NavigationOnly,
                    false,
                    false,
                    "Actionable prerequisite work exists, but no comparative raid evidence was supplied.");

            if (raid.Kind == PlannerRaidDecisionPresentationKind.SeveralGoodOptions)
            {
                if (HasGameplayTradeoff(raid.Alternatives))
                    return Value(
                        PlannerCapabilityDecisionValueKind.TradeoffClarified,
                        true,
                        true,
                        "Planner exposes competing proven gameplay advantages instead of forcing an arbitrary winner.");

                return Value(
                    PlannerCapabilityDecisionValueKind.NavigationOnly,
                    false,
                    false,
                    "Several raid options remain, but no gameplay trade-off beyond prerequisite location has been proven.");
            }

            if (raid.Kind == PlannerRaidDecisionPresentationKind.BestNextRaid && raid.Primary != null)
            {
                List<string> evidence = new List<string>();
                if (raid.Primary.HasCrossQuestSynergy)
                    evidence.Add("The selected raid combines compatible work across multiple quests.");
                if (raid.Primary.HasProgressionLeverage)
                    evidence.Add("The selected raid has proven immediate progression leverage on the focused path.");

                if (evidence.Count > 0)
                    return new PlannerCapabilityDecisionValue(
                        PlannerCapabilityDecisionValueKind.DecisionChanged,
                        true,
                        true,
                        evidence.ToArray());
            }

            return Value(
                PlannerCapabilityDecisionValueKind.NavigationOnly,
                false,
                false,
                "The result identifies where prerequisite work can be done, but no additional decision-changing evidence is proven.");
        }

        private static bool HasGameplayTradeoff(IReadOnlyList<PlannerRaidDecisionExplanation> alternatives)
        {
            if (alternatives == null || alternatives.Count < 2) return false;

            PlannerRaidDecisionExplanation first = alternatives[0];
            for (int i = 1; i < alternatives.Count; i++)
            {
                PlannerRaidDecisionExplanation other = alternatives[i];
                if (other == null || first == null) continue;
                if (first.HasCrossQuestSynergy != other.HasCrossQuestSynergy) return true;
                if (first.HasProgressionLeverage != other.HasProgressionLeverage) return true;
                if (first.PreparationReady != other.PreparationReady) return true;
                if (first.MissingPreparationTemplateCount != other.MissingPreparationTemplateCount) return true;
            }
            return false;
        }

        private static PlannerCapabilityDecisionValue Value(
            PlannerCapabilityDecisionValueKind kind,
            bool beyondNavigation,
            bool countsTowardKeep,
            string evidence)
        {
            return new PlannerCapabilityDecisionValue(kind, beyondNavigation, countsTowardKeep, new[] { evidence });
        }
    }
}
