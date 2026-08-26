using System;
using System.Collections.Generic;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerPrerequisitePathStateTests
    {
        [Fact]
        public void StartedPrerequisiteAcceptedByTargetIsExcludedFromFuturePath()
        {
            PlannerTopologyPrerequisite rootToSource = new PlannerTopologyPrerequisite("root", "source", new[] { 4 }, 0);
            PlannerTopologyPrerequisite sourceToTarget = new PlannerTopologyPrerequisite("source", "target", new[] { 3 }, 0);
            PlannerTopologyIndex topology = Topology(rootToSource, sourceToTarget);
            PlannerClientIndex state = State(
                new PlannerQuestClientState("root", 1, 1, true, false),
                new PlannerQuestClientState("source", 4, 3, true, true),
                new PlannerQuestClientState("target", 1, 1, true, false));

            PlannerQueryEngine engine = new PlannerQueryEngine(topology, state);

            Assert.Equal(new[] { "target" }, engine.GetIncompletePrerequisitePlan("target"));
            Assert.Empty(engine.GetIncompleteAncestors("target"));
        }

        [Fact]
        public void ActivePrerequisiteRequiringSuccessRemainsWorkWithoutDraggingSatisfiedHistory()
        {
            PlannerTopologyPrerequisite rootToSource = new PlannerTopologyPrerequisite("root", "source", new[] { 4 }, 0);
            PlannerTopologyPrerequisite sourceToTarget = new PlannerTopologyPrerequisite("source", "target", new[] { 4 }, 0);
            PlannerTopologyIndex topology = Topology(rootToSource, sourceToTarget);
            PlannerClientIndex state = State(
                new PlannerQuestClientState("root", 5, 4, true, true),
                new PlannerQuestClientState("source", 4, 3, true, true),
                new PlannerQuestClientState("target", 1, 1, true, false));

            PlannerQueryEngine engine = new PlannerQueryEngine(topology, state);

            Assert.Equal(new[] { "source", "target" }, engine.GetIncompletePrerequisitePlan("target"));
            Assert.Equal(new[] { "source" }, engine.GetIncompleteAncestors("target"));
        }

        [Fact]
        public void UnsatisfiedBranchTraversesOnlyItsUnsatisfiedEdges()
        {
            PlannerTopologyPrerequisite doneToMiddle = new PlannerTopologyPrerequisite("done", "middle", new[] { 4 }, 0);
            PlannerTopologyPrerequisite pendingToMiddle = new PlannerTopologyPrerequisite("pending", "middle", new[] { 4 }, 0);
            PlannerTopologyPrerequisite middleToTarget = new PlannerTopologyPrerequisite("middle", "target", new[] { 4 }, 0);
            PlannerTopologyIndex topology = Topology(doneToMiddle, pendingToMiddle, middleToTarget);
            PlannerClientIndex state = State(
                new PlannerQuestClientState("done", 5, 4, true, true),
                new PlannerQuestClientState("pending", 3, 2, true, true),
                new PlannerQuestClientState("middle", 1, 1, true, false),
                new PlannerQuestClientState("target", 1, 1, true, false));

            PlannerQueryEngine engine = new PlannerQueryEngine(topology, state);

            Assert.Equal(new[] { "pending", "middle", "target" }, engine.GetIncompletePrerequisitePlan("target"));
            Assert.Equal(new[] { "middle", "pending" }, engine.GetIncompleteAncestors("target"));
        }

        private static PlannerTopologyIndex Topology(params PlannerTopologyPrerequisite[] edges)
        {
            Dictionary<string, List<PlannerTopologyPrerequisite>> incoming = new Dictionary<string, List<PlannerTopologyPrerequisite>>(StringComparer.Ordinal);
            Dictionary<string, List<PlannerTopologyPrerequisite>> outgoing = new Dictionary<string, List<PlannerTopologyPrerequisite>>(StringComparer.Ordinal);
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (PlannerTopologyPrerequisite edge in edges)
            {
                ids.Add(edge.SourceQuestId);
                ids.Add(edge.TargetQuestId);
                if (!incoming.ContainsKey(edge.TargetQuestId)) incoming[edge.TargetQuestId] = new List<PlannerTopologyPrerequisite>();
                if (!outgoing.ContainsKey(edge.SourceQuestId)) outgoing[edge.SourceQuestId] = new List<PlannerTopologyPrerequisite>();
                incoming[edge.TargetQuestId].Add(edge);
                outgoing[edge.SourceQuestId].Add(edge);
            }

            Dictionary<string, PlannerTopologyQuest> quests = new Dictionary<string, PlannerTopologyQuest>(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                List<PlannerTopologyPrerequisite> inEdges;
                List<PlannerTopologyPrerequisite> outEdges;
                incoming.TryGetValue(id, out inEdges);
                outgoing.TryGetValue(id, out outEdges);
                inEdges = inEdges ?? new List<PlannerTopologyPrerequisite>();
                outEdges = outEdges ?? new List<PlannerTopologyPrerequisite>();

                string[] prerequisites = new string[inEdges.Count];
                for (int i = 0; i < inEdges.Count; i++) prerequisites[i] = inEdges[i].SourceQuestId;
                string[] dependents = new string[outEdges.Count];
                for (int i = 0; i < outEdges.Count; i++) dependents[i] = outEdges[i].TargetQuestId;

                quests[id] = new PlannerTopologyQuest(
                    id, null, null, null, false,
                    prerequisites, dependents, Array.Empty<string>(), true,
                    inEdges.ToArray(), outEdges.ToArray());
            }

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
