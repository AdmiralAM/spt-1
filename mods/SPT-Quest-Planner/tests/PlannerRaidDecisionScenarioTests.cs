using System;
using System.Collections.Generic;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerRaidDecisionScenarioTests
    {
        [Fact]
        public void CustomsStyleSynergyCanBeatReserveStyleRawDensity()
        {
            PlannerRaidDecisionSignals customs = Signals(
                new[] { "Setup", "ShooterBorn" },
                overlapQuestIds: new[] { "Setup", "ShooterBorn" },
                unlocks: new[] { "Setup-Next" },
                objectiveCount: 2,
                missing: 0,
                unresolved: 0,
                unknown: 0);

            PlannerRaidDecisionSignals reserve = Signals(
                new[] { "ReserveKill", "ReserveExtract" },
                overlapQuestIds: Array.Empty<string>(),
                unlocks: Array.Empty<string>(),
                objectiveCount: 9,
                missing: 0,
                unresolved: 0,
                unknown: 0);

            PlannerRaidDecision decision = PlannerRaidDecisionPolicy.Decide(customs, reserve);

            Assert.Equal(PlannerRaidDecisionOutcome.PreferLeft, decision.Outcome);
            Assert.True(decision.HasRecommendation);
            PlannerRaidDecisionExplanation explanation = PlannerRaidDecisionExplanationBuilder.Build("customs", customs);
            Assert.Contains("Setup", explanation.ProgressionQuestIds);
            Assert.Contains("ShooterBorn", explanation.ProgressionQuestIds);
            Assert.True(explanation.HasCrossQuestSynergy);
            Assert.True(explanation.HasProgressionLeverage);
        }

        [Fact]
        public void HigherProgressionLeverageWithMissingPreparationRemainsAPlayerTradeoff()
        {
            PlannerRaidDecisionSignals customs = Signals(
                new[] { "Setup", "ShooterBorn" },
                overlapQuestIds: new[] { "Setup", "ShooterBorn" },
                unlocks: new[] { "A", "B" },
                objectiveCount: 2,
                missing: 1,
                unresolved: 0,
                unknown: 0);

            PlannerRaidDecisionSignals shoreline = Signals(
                new[] { "ReadyQuest" },
                overlapQuestIds: Array.Empty<string>(),
                unlocks: Array.Empty<string>(),
                objectiveCount: 2,
                missing: 0,
                unresolved: 0,
                unknown: 0);

            PlannerRaidDecision decision = PlannerRaidDecisionPolicy.Decide(customs, shoreline);

            Assert.Equal(PlannerRaidDecisionOutcome.Abstain, decision.Outcome);
            Assert.Contains("competing proven advantages", decision.Reason);
        }

        private static PlannerRaidDecisionSignals Signals(
            IReadOnlyList<string> progressionQuestIds,
            IReadOnlyList<string> overlapQuestIds,
            IReadOnlyList<string> unlocks,
            int objectiveCount,
            int missing,
            int unresolved,
            int unknown)
        {
            PlannerRaidActionOverlap[] overlaps = overlapQuestIds.Count >= 2
                ? new[] { new PlannerRaidActionOverlap("kill|map|pmc", PlannerRaidObjectiveKind.Kill, overlapQuestIds, overlapQuestIds.Count) }
                : Array.Empty<PlannerRaidActionOverlap>();

            return new PlannerRaidDecisionSignals(
                progressionQuestIds.Count,
                0,
                overlaps,
                unlocks.Count,
                missing,
                unresolved,
                unknown,
                objectiveCount,
                objectiveCount,
                progressionQuestIds,
                Array.Empty<string>(),
                unlocks);
        }
    }
}
