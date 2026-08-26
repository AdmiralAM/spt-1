using System;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerRaidFocusedDecisionPolicyTests
    {
        [Fact]
        public void MoreActionableFocusedProgressCanWinWithoutHiddenWeights()
        {
            PlannerRaidDecisionIntent intent = Intent("goal", "a", "b");
            PlannerRaidDecisionSignals left = Signals(new[] { "a", "b" });
            PlannerRaidDecisionSignals right = Signals(new[] { "a" });

            PlannerRaidDecision decision = PlannerRaidFocusedDecisionPolicy.Decide(left, right, intent);

            Assert.Equal(PlannerRaidDecisionOutcome.PreferLeft, decision.Outcome);
            Assert.Contains("selected progression focus", decision.Reason);
        }

        [Fact]
        public void FocusedUnlockVersusPreparationFrictionRemainsAnHonestTradeoff()
        {
            PlannerRaidDecisionIntent intent = Intent("goal", "a", "b");
            PlannerRaidDecisionSignals left = Signals(
                new[] { "a" },
                immediateUnlocks: new[] { "next" },
                missing: 1);
            PlannerRaidDecisionSignals right = Signals(new[] { "b" }, missing: 0);

            intent = new PlannerRaidDecisionIntent(
                "goal",
                new[] { "a", "b", "next", "goal" },
                new[] { "a", "b" },
                new[] { "a", "b" },
                Array.Empty<string>());

            PlannerRaidDecision decision = PlannerRaidFocusedDecisionPolicy.Decide(left, right, intent);

            Assert.Equal(PlannerRaidDecisionOutcome.Abstain, decision.Outcome);
            Assert.Contains("competing proven focused advantages", decision.Reason);
        }

        [Fact]
        public void SharedActionSynergyAroundFocusedQuestIsRelevantFocusedEvidence()
        {
            PlannerRaidDecisionIntent intent = Intent("goal", "a");
            PlannerRaidDecisionSignals left = Signals(
                new[] { "a", "side" },
                overlap: new PlannerRaidActionOverlap(
                    "kill|customs|pmc",
                    PlannerRaidObjectiveKind.Kill,
                    new[] { "a", "side" },
                    2));
            PlannerRaidDecisionSignals right = Signals(new[] { "a" });

            PlannerRaidDecision decision = PlannerRaidFocusedDecisionPolicy.Decide(left, right, intent);

            Assert.Equal(PlannerRaidDecisionOutcome.PreferLeft, decision.Outcome);
        }

        [Fact]
        public void EqualFocusedEvidenceAllowsGeneralConservativeTieBreaker()
        {
            PlannerRaidDecisionIntent intent = Intent("goal", "a");
            PlannerRaidDecisionSignals left = Signals(new[] { "a", "side" });
            PlannerRaidDecisionSignals right = Signals(new[] { "a" });

            PlannerRaidDecision decision = PlannerRaidFocusedDecisionPolicy.Decide(left, right, intent);

            Assert.Equal(PlannerRaidDecisionOutcome.PreferLeft, decision.Outcome);
        }

        [Fact]
        public void ParallelMandatoryBranchesDoNotGainPriorityFromPathDepthAlone()
        {
            PlannerRaidDecisionIntent intent = new PlannerRaidDecisionIntent(
                "goal",
                new[] { "a", "a-next", "a-later", "b", "goal" },
                new[] { "a", "b" },
                new[] { "a", "b" },
                Array.Empty<string>());
            PlannerRaidDecisionSignals left = Signals(new[] { "a" });
            PlannerRaidDecisionSignals right = Signals(new[] { "b" });

            PlannerRaidDecision decision = PlannerRaidFocusedDecisionPolicy.Decide(left, right, intent);

            Assert.Equal(PlannerRaidDecisionOutcome.Abstain, decision.Outcome);
            Assert.False(decision.HasRecommendation);
        }

        [Fact]
        public void FocusedPathImmediateUnlockCanBreakParallelBranchSymmetry()
        {
            PlannerRaidDecisionIntent intent = new PlannerRaidDecisionIntent(
                "goal",
                new[] { "a", "a-next", "b", "goal" },
                new[] { "a", "b" },
                new[] { "a", "b" },
                Array.Empty<string>());
            PlannerRaidDecisionSignals left = Signals(
                new[] { "a" },
                immediateUnlocks: new[] { "a-next" });
            PlannerRaidDecisionSignals right = Signals(new[] { "b" });

            PlannerRaidDecision decision = PlannerRaidFocusedDecisionPolicy.Decide(left, right, intent);

            Assert.Equal(PlannerRaidDecisionOutcome.PreferLeft, decision.Outcome);
            Assert.Contains("focused-path immediate unlock", string.Join(" | ", decision.Evidence));
        }

        private static PlannerRaidDecisionIntent Intent(string goal, params string[] actionable)
        {
            string[] path = new string[actionable.Length + 1];
            Array.Copy(actionable, path, actionable.Length);
            path[path.Length - 1] = goal;
            return new PlannerRaidDecisionIntent(
                goal,
                path,
                actionable,
                actionable,
                Array.Empty<string>());
        }

        private static PlannerRaidDecisionSignals Signals(
            string[] questIds,
            string[] immediateUnlocks = null,
            int missing = 0,
            int unresolved = 0,
            PlannerRaidActionOverlap overlap = null)
        {
            PlannerRaidActionOverlap[] overlaps = overlap == null
                ? Array.Empty<PlannerRaidActionOverlap>()
                : new[] { overlap };
            string[] unlocks = immediateUnlocks ?? Array.Empty<string>();

            return new PlannerRaidDecisionSignals(
                questIds.Length,
                0,
                overlaps,
                unlocks.Length,
                missing,
                unresolved,
                0,
                Math.Max(1, questIds.Length),
                questIds.Length,
                questIds,
                Array.Empty<string>(),
                unlocks);
        }
    }
}
