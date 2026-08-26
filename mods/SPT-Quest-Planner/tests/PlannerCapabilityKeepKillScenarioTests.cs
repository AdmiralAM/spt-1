using System;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerCapabilityKeepKillScenarioTests
    {
        [Fact]
        public void FocusedSharedActionCanChangeTheRaidDecisionInsteadOfRepeatingQuestDensity()
        {
            PlannerRaidDecisionIntent intent = new PlannerRaidDecisionIntent(
                "capability-gate",
                new[] { "fieldwork", "capability-gate" },
                new[] { "fieldwork" },
                new[] { "fieldwork" },
                Array.Empty<string>());

            PlannerRaidDecisionSignals customs = Signals(
                new[] { "fieldwork", "setup" },
                overlap: new PlannerRaidActionOverlap(
                    "kill|customs|pmc",
                    PlannerRaidObjectiveKind.Kill,
                    new[] { "fieldwork", "setup" },
                    2),
                objectiveCount: 2);
            PlannerRaidDecisionSignals reserve = Signals(
                new[] { "fieldwork" },
                objectiveCount: 9);

            PlannerRaidDecisionSet set = PlannerRaidDecisionSetBuilder.Build(
                new[]
                {
                    new PlannerRaidDecisionCandidate("customs", customs),
                    new PlannerRaidDecisionCandidate("reserve", reserve)
                },
                intent);

            Assert.True(set.HasUniqueRecommendation);
            Assert.Equal("customs", set.Recommendation.LocationId);
        }

        [Fact]
        public void FocusedUnlockVersusPreparationCostMustRemainSeveralGoodOptions()
        {
            PlannerRaidDecisionIntent intent = new PlannerRaidDecisionIntent(
                "capability-gate",
                new[] { "customs-step", "customs-next", "woods-step", "capability-gate" },
                new[] { "customs-step", "woods-step" },
                new[] { "customs-step", "woods-step" },
                Array.Empty<string>());

            PlannerRaidDecisionSignals customs = Signals(
                new[] { "customs-step" },
                immediateUnlocks: new[] { "customs-next" },
                missing: 1,
                objectiveCount: 1);
            PlannerRaidDecisionSignals woods = Signals(
                new[] { "woods-step" },
                missing: 0,
                objectiveCount: 1);

            PlannerRaidDecisionSet set = PlannerRaidDecisionSetBuilder.Build(
                new[]
                {
                    new PlannerRaidDecisionCandidate("customs", customs),
                    new PlannerRaidDecisionCandidate("woods", woods)
                },
                intent);
            PlannerRaidDecisionPresentation presentation = PlannerRaidDecisionPresentationBuilder.Build(set);

            Assert.False(set.HasUniqueRecommendation);
            Assert.Equal(2, set.Contenders.Count);
            Assert.Equal(PlannerRaidDecisionPresentationKind.SeveralGoodOptions, presentation.Kind);
        }

        [Fact]
        public void WaitingOnlyCapabilityMustTellPlayerNotToRaidForTheGoal()
        {
            PlannerCapabilityGoalDefinition definition = new PlannerCapabilityGoalDefinition(
                "labs-access",
                "clearance",
                "Admiral Trader",
                PlannerCapabilitySupplyKind.BoundedRenewable,
                "labs-card",
                1,
                1,
                "scenario");
            PlannerCapabilityGoal goal = new PlannerCapabilityGoal(
                definition,
                new PlannerRaidDecisionIntent(
                    "clearance",
                    new[] { "delayed-step", "clearance" },
                    new[] { "delayed-step" },
                    Array.Empty<string>(),
                    Array.Empty<string>()));
            PlannerRaidFocusDelayEvidence delay = new PlannerRaidFocusDelayEvidence(
                new[] { "delayed-step" },
                Array.Empty<string>(),
                Array.Empty<string>());

            PlannerCapabilityGoalPresentation presentation = PlannerCapabilityGoalPresentationBuilder.Build(
                goal,
                null,
                delay);

            Assert.Equal(PlannerCapabilityGoalPresentationKind.WaitingForAvailability, presentation.Kind);
            Assert.Empty(presentation.ActionableQuestIds);
            Assert.Contains("No raid action is required", presentation.Caution);
        }

        private static PlannerRaidDecisionSignals Signals(
            string[] questIds,
            string[] immediateUnlocks = null,
            int missing = 0,
            int objectiveCount = 0,
            PlannerRaidActionOverlap overlap = null)
        {
            string[] unlocks = immediateUnlocks ?? Array.Empty<string>();
            PlannerRaidActionOverlap[] overlaps = overlap == null
                ? Array.Empty<PlannerRaidActionOverlap>()
                : new[] { overlap };

            return new PlannerRaidDecisionSignals(
                questIds.Length,
                0,
                overlaps,
                unlocks.Length,
                missing,
                0,
                0,
                objectiveCount,
                objectiveCount,
                questIds,
                Array.Empty<string>(),
                unlocks);
        }
    }
}
