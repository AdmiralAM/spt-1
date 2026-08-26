using System;
using System.Collections.Generic;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerTerminalPrerequisiteConflictTests
    {
        [Fact]
        public void CompletedNonRepeatableSourceThatCannotSatisfyTargetIsTerminalConflict()
        {
            PlannerTopologyPrerequisite edge = new PlannerTopologyPrerequisite("source", "target", new[] { 3 }, 0);
            PlannerTopologyIndex topology = Topology(edge, sourceRepeatable: false);
            PlannerClientIndex state = State(
                new PlannerQuestClientState("source", 5, 4, true, true),
                new PlannerQuestClientState("target", 1, 1, true, false));

            PlannerQueryEngine query = new PlannerQueryEngine(topology, state);
            IReadOnlyList<PlannerTopologyPrerequisite> conflicts = query.GetTerminalPrerequisiteConflicts("target");

            Assert.Single(conflicts);
            Assert.Equal("source", conflicts[0].SourceQuestId);
            Assert.Equal("target", conflicts[0].TargetQuestId);
        }

        [Fact]
        public void RepeatableSourceIsNotClaimedTerminalBecauseItMayCycleAgain()
        {
            PlannerTopologyPrerequisite edge = new PlannerTopologyPrerequisite("source", "target", new[] { 3 }, 0);
            PlannerTopologyIndex topology = Topology(edge, sourceRepeatable: true);
            PlannerClientIndex state = State(
                new PlannerQuestClientState("source", 5, 4, true, true),
                new PlannerQuestClientState("target", 1, 1, true, false));

            Assert.Empty(new PlannerQueryEngine(topology, state).GetTerminalPrerequisiteConflicts("target"));
        }

        [Fact]
        public void TerminalConflictStopsFocusedPolicyFromFallingBackToUnrelatedRaidRecommendation()
        {
            PlannerTopologyPrerequisite edge = new PlannerTopologyPrerequisite("source", "target", new[] { 3 }, 0);
            PlannerTopologyIndex topology = Topology(edge, sourceRepeatable: false);
            PlannerClientIndex state = State(
                new PlannerQuestClientState("source", 5, 4, true, true),
                new PlannerQuestClientState("target", 1, 1, true, false));
            PlannerRaidDecisionIntent intent = PlannerRaidDecisionIntentBuilder.Build("target", topology, state);

            PlannerRaidDecisionSignals left = Signals("unrelated-a", unlocks: 2);
            PlannerRaidDecisionSignals right = Signals("unrelated-b", unlocks: 0);
            PlannerRaidDecision decision = PlannerRaidDecisionIntentPolicy.Decide(left, right, intent);

            Assert.True(intent.HasTerminalFocusConflict);
            Assert.Single(intent.FocusTerminalConflictEdges);
            Assert.Equal(PlannerRaidDecisionOutcome.Abstain, decision.Outcome);
            Assert.Contains("terminal prerequisite-state conflict", decision.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("source -> target", decision.Evidence[0], StringComparison.Ordinal);
        }

        private static PlannerRaidDecisionSignals Signals(string questId, int unlocks)
        {
            return new PlannerRaidDecisionSignals(
                1,
                0,
                Array.Empty<PlannerRaidActionOverlap>(),
                unlocks,
                0,
                0,
                0,
                1,
                0,
                nonRepeatableQuestIds: new[] { questId },
                immediateUnlockQuestIds: unlocks > 0 ? new[] { "next" } : Array.Empty<string>());
        }

        private static PlannerTopologyIndex Topology(PlannerTopologyPrerequisite edge, bool sourceRepeatable)
        {
            Dictionary<string, PlannerTopologyQuest> quests = new Dictionary<string, PlannerTopologyQuest>(StringComparer.Ordinal)
            {
                ["source"] = new PlannerTopologyQuest(
                    "source", null, null, null, sourceRepeatable,
                    Array.Empty<string>(), new[] { "target" }, Array.Empty<string>(), true,
                    Array.Empty<PlannerTopologyPrerequisite>(), new[] { edge }),
                ["target"] = new PlannerTopologyQuest(
                    "target", null, null, null, false,
                    new[] { "source" }, Array.Empty<string>(), Array.Empty<string>(), true,
                    new[] { edge }, Array.Empty<PlannerTopologyPrerequisite>())
            };
            return new PlannerTopologyIndex(quests, new Dictionary<string, PlannerTopologyItem>(StringComparer.Ordinal));
        }

        private static PlannerClientIndex State(params PlannerQuestClientState[] values)
        {
            Dictionary<string, PlannerQuestClientState> quests = new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal);
            foreach (PlannerQuestClientState value in values) quests[value.QuestId] = value;
            return new PlannerClientIndex(0, quests, new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal));
        }
    }
}
