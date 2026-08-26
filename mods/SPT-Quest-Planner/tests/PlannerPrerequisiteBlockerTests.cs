using System;
using System.Collections.Generic;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerPrerequisiteBlockerTests
    {
        [Fact]
        public void StartedSourceIsNotBlockerWhenConditionAcceptsStarted()
        {
            PlannerTopologyPrerequisite edge = new PlannerTopologyPrerequisite("source", "target", new[] { 3 }, 0);
            PlannerTopologyIndex topology = Topology(edge);
            PlannerClientIndex state = State(
                new PlannerQuestClientState("source", 4, 3, true, true),
                new PlannerQuestClientState("target", 1, 1, true, false));

            Assert.Empty(new PlannerQueryEngine(topology, state).GetImmediateBlockers("target"));
        }

        [Fact]
        public void CompletedSourceRemainsBlockerWhenConditionAcceptsOnlyStarted()
        {
            PlannerTopologyPrerequisite edge = new PlannerTopologyPrerequisite("source", "target", new[] { 3 }, 0);
            PlannerTopologyIndex topology = Topology(edge);
            PlannerClientIndex state = State(
                new PlannerQuestClientState("source", 5, 4, true, true),
                new PlannerQuestClientState("target", 1, 1, true, false));

            Assert.Equal(new[] { "source" }, new PlannerQueryEngine(topology, state).GetImmediateBlockers("target"));
        }

        private static PlannerTopologyIndex Topology(PlannerTopologyPrerequisite edge)
        {
            Dictionary<string, PlannerTopologyQuest> quests = new Dictionary<string, PlannerTopologyQuest>(StringComparer.Ordinal)
            {
                ["source"] = new PlannerTopologyQuest(
                    "source", null, null, null, false,
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
