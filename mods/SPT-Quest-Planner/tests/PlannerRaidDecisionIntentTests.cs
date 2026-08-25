using System;
using System.Collections.Generic;
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
        public void FutureFocusQuestResolvesRaidThroughIncompletePrerequisitePath()
        {
            PlannerTopologyQuest prerequisite = Quest("prereq", dependents: new[] { "future" });
            PlannerTopologyQuest future = Quest("future", prerequisites: new[] { "prereq" });
            PlannerTopologyIndex topology = Topology(prerequisite, future);
            PlannerClientIndex state = EmptyState();

            PlannerRaidDecisionIntent intent = PlannerRaidDecisionIntentBuilder.Build("future", topology, state);
            PlannerRaidDecisionSignals pathRaid = Signals(new[] { "prereq" }, unlocks: 0, missing: 0);
            PlannerRaidDecisionSignals unrelatedRaid = Signals(new[] { "other" }, unlocks: 0, missing: 0);

            PlannerRaidDecision decision = PlannerRaidDecisionIntentPolicy.Decide(pathRaid, unrelatedRaid, intent);

            Assert.True(intent.HasFocusPath);
            Assert.True(intent.HasExecutableFocusFrontier);
            Assert.Contains("prereq", intent.FocusPathQuestIds);
            Assert.Contains("future", intent.FocusPathQuestIds);
            Assert.Equal(new[] { "prereq" }, intent.FocusFrontierQuestIds);
            Assert.Equal(PlannerRaidDecisionOutcome.PreferLeft, decision.Outcome);
            Assert.Contains("prerequisite path", decision.Evidence[0]);
            Assert.Contains("executable focus frontier", decision.Evidence[0]);
        }

        [Fact]
        public void MultiBranchFocusOnlyTreatsCurrentlyExecutablePathNodesAsIntentSupport()
        {
            PlannerTopologyQuest a1 = Quest("a1", dependents: new[] { "a" });
            PlannerTopologyQuest a = Quest("a", prerequisites: new[] { "a1" }, dependents: new[] { "target" });
            PlannerTopologyQuest b = Quest("b", dependents: new[] { "target" });
            PlannerTopologyQuest target = Quest("target", prerequisites: new[] { "a", "b" });
            PlannerTopologyIndex topology = Topology(a1, a, b, target);
            PlannerRaidDecisionIntent intent = PlannerRaidDecisionIntentBuilder.Build("target", topology, EmptyState());

            Assert.Equal(new[] { "a1", "b" }, intent.FocusFrontierQuestIds);
            Assert.Contains("a", intent.FocusPathQuestIds);

            PlannerRaidDecisionSignals blockedInnerNodeRaid = Signals(new[] { "a" }, unlocks: 1, missing: 1);
            PlannerRaidDecisionSignals unrelatedReadyRaid = Signals(new[] { "other" }, unlocks: 0, missing: 0);
            PlannerRaidDecision blockedDecision = PlannerRaidDecisionIntentPolicy.Decide(
                blockedInnerNodeRaid,
                unrelatedReadyRaid,
                intent);

            Assert.Equal(PlannerRaidDecisionOutcome.Abstain, blockedDecision.Outcome);
            Assert.False(PlannerRaidDecisionIntentPolicy.Supports(blockedInnerNodeRaid, intent));

            PlannerRaidDecisionSignals executableFrontierRaid = Signals(new[] { "a1" }, unlocks: 1, missing: 1);
            PlannerRaidDecision frontierDecision = PlannerRaidDecisionIntentPolicy.Decide(
                executableFrontierRaid,
                unrelatedReadyRaid,
                intent);

            Assert.Equal(PlannerRaidDecisionOutcome.PreferLeft, frontierDecision.Outcome);
            Assert.True(PlannerRaidDecisionIntentPolicy.Supports(executableFrontierRaid, intent));
        }

        [Fact]
        public void CompletingFrontierPrerequisiteAdvancesFocusFrontierDeterministically()
        {
            PlannerTopologyQuest a1 = Quest("a1", dependents: new[] { "a" });
            PlannerTopologyQuest a = Quest("a", prerequisites: new[] { "a1" }, dependents: new[] { "target" });
            PlannerTopologyQuest b = Quest("b", dependents: new[] { "target" });
            PlannerTopologyQuest target = Quest("target", prerequisites: new[] { "a", "b" });
            PlannerTopologyIndex topology = Topology(a1, a, b, target);
            PlannerClientIndex state = new PlannerClientIndex(
                0,
                new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal)
                {
                    ["a1"] = new PlannerQuestClientState("a1", 5, 0, true, false)
                },
                new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal));

            PlannerRaidDecisionIntent intent = PlannerRaidDecisionIntentBuilder.Build("target", topology, state);

            Assert.DoesNotContain("a1", intent.FocusPathQuestIds);
            Assert.Equal(new[] { "a", "b" }, intent.FocusFrontierQuestIds);
        }

        [Fact]
        public void CompletedPrerequisiteDropsOutOfFutureFocusPath()
        {
            PlannerTopologyQuest prerequisite = Quest("prereq", dependents: new[] { "future" });
            PlannerTopologyQuest future = Quest("future", prerequisites: new[] { "prereq" });
            PlannerTopologyIndex topology = Topology(prerequisite, future);
            PlannerClientIndex state = new PlannerClientIndex(
                0,
                new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal)
                {
                    ["prereq"] = new PlannerQuestClientState("prereq", 5, 0, true, false)
                },
                new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal));

            PlannerRaidDecisionIntent intent = PlannerRaidDecisionIntentBuilder.Build("future", topology, state);

            Assert.DoesNotContain("prereq", intent.FocusPathQuestIds);
            Assert.Contains("future", intent.FocusPathQuestIds);
            Assert.Equal(new[] { "future" }, intent.FocusFrontierQuestIds);
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

        private static PlannerTopologyQuest Quest(
            string id,
            IReadOnlyList<string> prerequisites = null,
            IReadOnlyList<string> dependents = null)
        {
            return new PlannerTopologyQuest(
                id,
                null,
                null,
                null,
                false,
                prerequisites ?? Array.Empty<string>(),
                dependents ?? Array.Empty<string>(),
                Array.Empty<string>());
        }

        private static PlannerTopologyIndex Topology(params PlannerTopologyQuest[] quests)
        {
            Dictionary<string, PlannerTopologyQuest> values = new Dictionary<string, PlannerTopologyQuest>(StringComparer.Ordinal);
            foreach (PlannerTopologyQuest quest in quests) values[quest.QuestId] = quest;
            return new PlannerTopologyIndex(
                values,
                new Dictionary<string, PlannerTopologyItem>(StringComparer.Ordinal));
        }

        private static PlannerClientIndex EmptyState()
        {
            return new PlannerClientIndex(
                0,
                new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal),
                new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal));
        }
    }
}
