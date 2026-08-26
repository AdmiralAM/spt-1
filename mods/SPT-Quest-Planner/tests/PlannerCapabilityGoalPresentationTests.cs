using System;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerCapabilityGoalPresentationTests
    {
        [Fact]
        public void ActionableGoalCarriesRaidDecisionAndSupplyResult()
        {
            PlannerCapabilityGoal goal = Goal(
                new PlannerRaidDecisionIntent(
                    "gate",
                    new[] { "fieldwork", "gate" },
                    new[] { "fieldwork" },
                    new[] { "fieldwork" }));
            PlannerRaidDecisionPresentation raid = new PlannerRaidDecisionPresentation(
                PlannerRaidDecisionPresentationKind.BestNextRaid,
                null,
                Array.Empty<PlannerRaidDecisionExplanation>(),
                "Best next raid",
                "Customs advances the selected capability path.");

            PlannerCapabilityGoalPresentation result = PlannerCapabilityGoalPresentationBuilder.Build(
                goal,
                raid,
                EmptyDelay());

            Assert.Equal(PlannerCapabilityGoalPresentationKind.RaidDecision, result.Kind);
            Assert.Same(raid, result.RaidDecision);
            Assert.Equal(new[] { "fieldwork" }, result.ActionableQuestIds);
            Assert.Contains("bounded renewable capability", result.ResultSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("80 units/reset", result.ResultSummary);
        }

        [Fact]
        public void WaitingOnlyGoalDoesNotInventRaidWork()
        {
            PlannerCapabilityGoal goal = Goal(
                new PlannerRaidDecisionIntent(
                    "gate",
                    new[] { "wait", "gate" },
                    new[] { "wait" },
                    Array.Empty<string>()));
            PlannerRaidFocusDelayEvidence delay = new PlannerRaidFocusDelayEvidence(
                new[] { "wait" },
                Array.Empty<string>(),
                Array.Empty<string>());

            PlannerCapabilityGoalPresentation result = PlannerCapabilityGoalPresentationBuilder.Build(goal, null, delay);

            Assert.Equal(PlannerCapabilityGoalPresentationKind.WaitingForAvailability, result.Kind);
            Assert.Equal(new[] { "wait" }, result.WaitingQuestIds);
            Assert.Empty(result.ActionableQuestIds);
            Assert.Contains("No raid action is required", result.Caution);
        }

        [Fact]
        public void UnknownEligibilityBlocksRecommendationBeforeWaitingState()
        {
            PlannerCapabilityGoal goal = Goal(
                new PlannerRaidDecisionIntent(
                    "gate",
                    new[] { "unknown", "gate" },
                    new[] { "unknown" },
                    Array.Empty<string>(),
                    new[] { "unknown" }));
            PlannerRaidFocusDelayEvidence delay = new PlannerRaidFocusDelayEvidence(
                new[] { "unknown" },
                Array.Empty<string>(),
                Array.Empty<string>());

            PlannerCapabilityGoalPresentation result = PlannerCapabilityGoalPresentationBuilder.Build(goal, null, delay);

            Assert.Equal(PlannerCapabilityGoalPresentationKind.EvidenceIncomplete, result.Kind);
            Assert.Equal(new[] { "unknown" }, result.UnknownQuestIds);
        }

        [Fact]
        public void TerminalConflictWinsOverAllOtherPresentationStates()
        {
            PlannerTopologyPrerequisite conflict = new PlannerTopologyPrerequisite(
                "done-source",
                "gate",
                new[] { 3 },
                0);
            PlannerCapabilityGoal goal = Goal(
                new PlannerRaidDecisionIntent(
                    "gate",
                    new[] { "gate" },
                    new[] { "gate" },
                    new[] { "gate" },
                    Array.Empty<string>(),
                    new[] { conflict }));

            PlannerCapabilityGoalPresentation result = PlannerCapabilityGoalPresentationBuilder.Build(
                goal,
                null,
                EmptyDelay());

            Assert.Equal(PlannerCapabilityGoalPresentationKind.ProgressionConflict, result.Kind);
            Assert.Contains("conflict", result.Caution, StringComparison.OrdinalIgnoreCase);
        }

        private static PlannerCapabilityGoal Goal(PlannerRaidDecisionIntent intent)
        {
            PlannerCapabilityGoalDefinition definition = new PlannerCapabilityGoalDefinition(
                "assault-rifles",
                "gate",
                "Admiral Trader",
                PlannerCapabilitySupplyKind.BoundedRenewable,
                "ammo",
                80,
                80,
                "test");
            return new PlannerCapabilityGoal(definition, intent);
        }

        private static PlannerRaidFocusDelayEvidence EmptyDelay()
        {
            return new PlannerRaidFocusDelayEvidence(
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }
    }
}
