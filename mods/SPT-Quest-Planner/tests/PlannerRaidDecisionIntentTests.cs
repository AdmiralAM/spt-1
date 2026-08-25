using System;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerRaidDecisionIntentTests
    {
        [Fact]
        public void FocusQuestResolvesOtherwiseCompetingCandidates()
        {
            PlannerRaidDecisionSignals focused = Signals(new[] { "setup" }, unlocks: 1, missing: 1);
            PlannerRaidDecisionSignals readyAlternative = Signals(new[] { "other" }, unlocks: 0, missing: 0);

            PlannerRaidDecision baseline = PlannerRaidDecisionPolicy.Decide(focused, readyAlternative);
            PlannerRaidDecision intentional = PlannerRaidDecisionIntentPolicy.Decide(
                focused,
                readyAlternative,
                new PlannerRaidDecisionIntent("setup"));

            Assert.Equal(PlannerRaidDecisionOutcome.Abstain, baseline.Outcome);
            Assert.Equal(PlannerRaidDecisionOutcome.PreferLeft, intentional.Outcome);
            Assert.Contains("progression focus", intentional.Reason);
        }

        [Fact]
        public void EmptyIntentFallsBackToConservativePolicy()
        {
            PlannerRaidDecisionSignals left = Signals(new[] { "q1" }, unlocks: 1, missing: 1);
            PlannerRaidDecisionSignals right = Signals(new[] { "q2" }, unlocks: 0, missing: 0);

            PlannerRaidDecision baseline = PlannerRaidDecisionPolicy.Decide(left, right);
            PlannerRaidDecision intentional = PlannerRaidDecisionIntentPolicy.Decide(left, right, new PlannerRaidDecisionIntent());

            Assert.Equal(baseline.Outcome, intentional.Outcome);
        }

        private static PlannerRaidDecisionSignals Signals(string[] questIds, int unlocks, int missing)
        {
            return new PlannerRaidDecisionSignals(
                questIds.Length,
                0,
                Array.Empty<PlannerRaidActionOverlap>(),
                unlocks,
                missing,
                0,
                0,
                questIds.Length,
                0,
                nonRepeatableQuestIds: questIds,
                immediateUnlockQuestIds: unlocks > 0 ? new[] { "next" } : Array.Empty<string>());
        }
    }
}
