using System;
using System.Collections.Generic;
using System.Linq;

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
            IReadOnlyList<string> evidence)
        {
            Kind = kind;
            ProvesBeyondPrerequisiteNavigation = provesBeyondPrerequisiteNavigation;
            Evidence = evidence ?? Array.Empty<string>();
        }

        public PlannerCapabilityDecisionValueKind Kind { get; private set; }
        public bool ProvesBeyondPrerequisiteNavigation { get; private set; }
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
                        "Planner proves the selected capability gate is already completed and avoids redundant progression work.");

                case PlannerCapabilityGoalPresentationKind.WaitingForAvailability:
                    return Value(
                        PlannerCapabilityDecisionValueKind.UnnecessaryRaidAvoided,
                        true,
                        "The selected progression path is waiting on availability; no raid work is required for that branch now.");

                case PlannerCapabilityGoalPresentationKind.EvidenceIncomplete:
                case PlannerCapabilityGoalPresentationKind.ProgressionConflict:
                    return Value(
                        PlannerCapabilityDecisionValueKind.UnsupportedDecisionPrevented,
                        true,
                        "Planner refuses to fabricate a route when authoritative progression evidence is incomplete or contradictory.");

                case PlannerCapabilityGoalPresentationKind.RaidDecision:
                    return ClassifyRaidDecision(presentation.RaidDecision);

                default:
                    return Value(
                        PlannerCapabilityDecisionValueKind.NavigationOnly,
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
                    "Actionable prerequisite work exists, but no comparative raid evidence was supplied.");

            if (raid.Kind == PlannerRaidDecisionPresentationKind.SeveralGoodOptions)
            {
                if (HasMeaningfulAlternativeDifference(raid.Alternatives))
                    return Value(
                        PlannerCapabilityDecisionValueKind.TradeoffClarified,
                        true,
                        "Planner exposes competing proven advantages instead of forcing an arbitrary winner.");

                return Value(
                    PlannerCapabilityDecisionValueKind.NavigationOnly,
                    false,
                    "Several raid options remain, but no meaningful trade-off beyond prerequisite location has been proven.");
            }

            if (raid.Kind == PlannerRaidDecisionPresentationKind.BestNextRaid && raid.Primary != null)
            {
                List<string> evidence = new List<string>();
                if (raid.Primary.HasCrossQuestSynergy)
                    evidence.Add("The selected raid combines compatible work across multiple quests.");
                if (raid.Primary.HasProgressionLeverage)
                    evidence.Add("The selected raid has proven immediate progression leverage on the focused path.");
                if (!raid.Primary.PreparationReady)
                    evidence.Add("The recommendation includes explicit preparation friction rather than assuming readiness.");

                if (evidence.Count > 0)
                    return new PlannerCapabilityDecisionValue(
                        PlannerCapabilityDecisionValueKind.DecisionChanged,
                        true,
                        evidence.ToArray());
            }

            return Value(
                PlannerCapabilityDecisionValueKind.NavigationOnly,
                false,
                "The result identifies where prerequisite work can be done, but no additional decision-changing evidence is proven.");
        }

        private static bool HasMeaningfulAlternativeDifference(IReadOnlyList<PlannerRaidDecisionExplanation> alternatives)
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
                if (first.UnresolvedPreparationCount != other.UnresolvedPreparationCount) return true;
                if (Math.Abs(first.EvidenceCoverage - other.EvidenceCoverage) > 0.000001d) return true;
            }
            return false;
        }

        private static PlannerCapabilityDecisionValue Value(
            PlannerCapabilityDecisionValueKind kind,
            bool beyondNavigation,
            string evidence)
        {
            return new PlannerCapabilityDecisionValue(kind, beyondNavigation, new[] { evidence });
        }
    }
}
