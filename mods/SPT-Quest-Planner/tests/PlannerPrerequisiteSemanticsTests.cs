using System;
using System.Collections.Generic;
using SPTQuestPlanner;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerPrerequisiteSemanticsTests
    {
        [Fact]
        public void Extract_PreservesAcceptedStatusesRawStatusesAndAvailableAfter()
        {
            var quests = new Dictionary<string, object>
            {
                ["source"] = Quest("source", Array.Empty<object>()),
                ["target"] = Quest("target", new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["conditionType"] = "Quest",
                        ["id"] = "edge",
                        ["target"] = "source",
                        ["status"] = new object[] { 2, 3, 4 },
                        ["availableAfter"] = 3600
                    }
                })
            };

            QuestExtractionResult result = QuestExtractor.Extract(quests);

            PrerequisiteEdge edge = Assert.Single(result.Prerequisites);
            Assert.Contains(QuestState.Started, edge.AcceptedSourceStates);
            Assert.Contains(QuestState.Success, edge.AcceptedSourceStates);
            Assert.Equal(new[] { 2, 3, 4 }, edge.AcceptedSourceRawStatuses);
            Assert.Equal(3600, edge.AvailableAfterSeconds);
        }

        [Fact]
        public void Evaluate_DistinguishesRawStartedFromAvailableForFinish()
        {
            PrerequisiteEdge edge = new PrerequisiteEdge(
                "source",
                "target",
                new HashSet<QuestState> { QuestState.Started },
                AcceptedSourceRawStatuses: new HashSet<int> { 3 });
            (PlannerGraph graph, _) = PlannerGraph.Build(
                new[]
                {
                    new QuestNode("source", null, null, null, false),
                    new QuestNode("target", null, null, null, false)
                },
                new[] { edge });

            PlayerProjection rawStarted = Projection(
                new PlayerQuestState("source", QuestState.Started, 2, null, null));
            PlayerProjection rawFinish = Projection(
                new PlayerQuestState("source", QuestState.Started, 3, null, null));

            PlannerEvaluationResult startedResult = PlannerEvaluator.Evaluate(graph, Array.Empty<ItemRequirement>(), rawStarted);
            PlannerEvaluationResult finishResult = PlannerEvaluator.Evaluate(graph, Array.Empty<ItemRequirement>(), rawFinish);

            Assert.False(startedResult.Quests["target"].PrerequisitesSatisfied);
            Assert.Equal(PlannerQuestDisposition.Blocked, startedResult.Quests["target"].Disposition);
            Assert.True(finishResult.Quests["target"].PrerequisitesSatisfied);
            Assert.Equal(PlannerQuestDisposition.Reachable, finishResult.Quests["target"].Disposition);
        }

        [Fact]
        public void ClientQuery_DistinguishesRawStartedFromAvailableForFinish()
        {
            PlannerTopologyPrerequisite edge = new PlannerTopologyPrerequisite(
                "source", "target", new[] { 3 }, 0, new[] { 3 });
            PlannerTopologyIndex topology = Topology(
                QuestNodeClient("source", Array.Empty<PlannerTopologyPrerequisite>(), new[] { edge }),
                QuestNodeClient("target", new[] { edge }, Array.Empty<PlannerTopologyPrerequisite>()));

            PlannerClientIndex rawStarted = ClientState(
                new PlannerQuestClientState("source", 4, 3, true, true, rawProfileStatus: 2),
                new PlannerQuestClientState("target", 1, 1, true, false, rawProfileStatus: 0));
            PlannerClientIndex rawFinish = ClientState(
                new PlannerQuestClientState("source", 4, 3, true, true, rawProfileStatus: 3),
                new PlannerQuestClientState("target", 1, 1, true, false, rawProfileStatus: 0));

            Assert.Equal(new[] { "source" }, new PlannerQueryEngine(topology, rawStarted).GetImmediateBlockers("target"));
            Assert.Empty(new PlannerQueryEngine(topology, rawFinish).GetImmediateBlockers("target"));
        }

        [Fact]
        public void Extract_MarksUnsupportedStartGateAsIncompleteCoverage()
        {
            var quests = new Dictionary<string, object>
            {
                ["target"] = Quest("target", new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["conditionType"] = "TraderStanding",
                        ["target"] = "trader",
                        ["value"] = 0.6
                    }
                })
            };

            QuestExtractionResult result = QuestExtractor.Extract(quests);

            QuestNode node = Assert.Single(result.Nodes);
            Assert.False(node.StartConditionCoverageComplete);
            Assert.Contains(result.Warnings, value => value.Contains("TraderStanding", StringComparison.Ordinal));
        }

        [Fact]
        public void ImmediateUnlock_RequiresSuccessAcceptedAndZeroDelay()
        {
            PlannerTopologyPrerequisite delayed = new PlannerTopologyPrerequisite("source", "delayed", new[] { 4 }, 60);
            PlannerTopologyPrerequisite startedOnly = new PlannerTopologyPrerequisite("source", "started-only", new[] { 3 }, 0);
            PlannerTopologyPrerequisite immediate = new PlannerTopologyPrerequisite("source", "immediate", new[] { 4 }, 0);

            PlannerTopologyIndex topology = Topology(
                QuestNodeClient("source", Array.Empty<PlannerTopologyPrerequisite>(), new[] { delayed, startedOnly, immediate }),
                QuestNodeClient("delayed", new[] { delayed }, Array.Empty<PlannerTopologyPrerequisite>()),
                QuestNodeClient("started-only", new[] { startedOnly }, Array.Empty<PlannerTopologyPrerequisite>()),
                QuestNodeClient("immediate", new[] { immediate }, Array.Empty<PlannerTopologyPrerequisite>()));

            PlannerClientIndex state = new PlannerClientIndex(
                0,
                new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal)
                {
                    ["delayed"] = new PlannerQuestClientState("delayed", 1, 1, true, false),
                    ["started-only"] = new PlannerQuestClientState("started-only", 1, 1, true, false),
                    ["immediate"] = new PlannerQuestClientState("immediate", 1, 1, true, false)
                },
                new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal));

            IReadOnlyList<string> unlocks = new PlannerQueryEngine(topology, state).GetImmediateUnlocksIfCompleted("source");

            Assert.Equal(new[] { "immediate" }, unlocks);
        }

        [Fact]
        public void ImmediateUnlock_RequiresOtherPrerequisiteAcceptedState()
        {
            PlannerTopologyPrerequisite sourceEdge = new PlannerTopologyPrerequisite("source", "target", new[] { 4 }, 0);
            PlannerTopologyPrerequisite blockerEdge = new PlannerTopologyPrerequisite("blocker", "target", new[] { 3 }, 0);

            PlannerTopologyIndex topology = Topology(
                QuestNodeClient("source", Array.Empty<PlannerTopologyPrerequisite>(), new[] { sourceEdge }),
                QuestNodeClient("blocker", Array.Empty<PlannerTopologyPrerequisite>(), new[] { blockerEdge }),
                QuestNodeClient("target", new[] { sourceEdge, blockerEdge }, Array.Empty<PlannerTopologyPrerequisite>()));

            PlannerClientIndex state = new PlannerClientIndex(
                0,
                new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal)
                {
                    ["blocker"] = new PlannerQuestClientState("blocker", 5, 4, true, true),
                    ["target"] = new PlannerQuestClientState("target", 1, 1, true, false)
                },
                new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal));

            Assert.Empty(new PlannerQueryEngine(topology, state).GetImmediateUnlocksIfCompleted("source"));
        }

        [Fact]
        public void ImmediateUnlock_SuppressedWhenStartGateCoverageIsIncomplete()
        {
            PlannerTopologyPrerequisite edge = new PlannerTopologyPrerequisite("source", "target", new[] { 4 }, 0);
            PlannerTopologyIndex topology = Topology(
                QuestNodeClient("source", Array.Empty<PlannerTopologyPrerequisite>(), new[] { edge }),
                QuestNodeClient("target", new[] { edge }, Array.Empty<PlannerTopologyPrerequisite>(), startCoverageComplete: false));
            PlannerClientIndex state = new PlannerClientIndex(
                0,
                new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal)
                {
                    ["target"] = new PlannerQuestClientState("target", 1, 1, true, false)
                },
                new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal));

            Assert.Empty(new PlannerQueryEngine(topology, state).GetImmediateUnlocksIfCompleted("source"));
        }

        private static PlayerProjection Projection(params PlayerQuestState[] states)
        {
            Dictionary<string, PlayerQuestState> quests = new Dictionary<string, PlayerQuestState>(StringComparer.Ordinal);
            foreach (PlayerQuestState state in states) quests[state.QuestId] = state;
            return new PlayerProjection(
                99,
                quests,
                new Dictionary<string, PlayerTaskConditionCounter>(StringComparer.Ordinal),
                Array.Empty<string>());
        }

        private static PlannerClientIndex ClientState(params PlannerQuestClientState[] states)
        {
            Dictionary<string, PlannerQuestClientState> quests = new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal);
            foreach (PlannerQuestClientState state in states) quests[state.QuestId] = state;
            return new PlannerClientIndex(0, quests, new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal));
        }

        private static Dictionary<string, object> Quest(string id, IReadOnlyList<object> startConditions)
        {
            return new Dictionary<string, object>
            {
                ["_id"] = id,
                ["conditions"] = new Dictionary<string, object>
                {
                    ["AvailableForStart"] = startConditions,
                    ["AvailableForFinish"] = Array.Empty<object>()
                }
            };
        }

        private static PlannerTopologyQuest QuestNodeClient(
            string id,
            IReadOnlyList<PlannerTopologyPrerequisite> prerequisites,
            IReadOnlyList<PlannerTopologyPrerequisite> dependents,
            bool startCoverageComplete = true)
        {
            string[] prerequisiteIds = new string[prerequisites.Count];
            for (int i = 0; i < prerequisites.Count; i++) prerequisiteIds[i] = prerequisites[i].SourceQuestId;
            string[] dependentIds = new string[dependents.Count];
            for (int i = 0; i < dependents.Count; i++) dependentIds[i] = dependents[i].TargetQuestId;

            return new PlannerTopologyQuest(
                id,
                null,
                null,
                null,
                false,
                prerequisiteIds,
                dependentIds,
                Array.Empty<string>(),
                startCoverageComplete,
                prerequisites,
                dependents);
        }

        private static PlannerTopologyIndex Topology(params PlannerTopologyQuest[] quests)
        {
            Dictionary<string, PlannerTopologyQuest> values = new Dictionary<string, PlannerTopologyQuest>(StringComparer.Ordinal);
            foreach (PlannerTopologyQuest quest in quests) values[quest.QuestId] = quest;
            return new PlannerTopologyIndex(values, new Dictionary<string, PlannerTopologyItem>(StringComparer.Ordinal));
        }
    }
}
