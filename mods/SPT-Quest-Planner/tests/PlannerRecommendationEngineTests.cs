using System;
using System.Collections.Generic;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerRecommendationEngineTests
    {
        [Fact]
        public void Recommend_ReturnsTopNWithExplanations()
        {
            PlannerClientIndex state = State(
                Quest("a", 4),
                Quest("b", 3),
                Quest("c", 2));
            PlannerTopologyIndex topology = Topology("a", "b", "c");
            PlannerQueryEngine query = new PlannerQueryEngine(topology, state);
            PlannerPathItemPlanner items = new PlannerPathItemPlanner(
                query,
                new PlannerRequirementIndex(null),
                state);
            PlannerRecommendationEngine engine = new PlannerRecommendationEngine(
                new PlannerCandidateSelector(state),
                new PlannerRoutePrioritizer(query, items, state));

            IReadOnlyList<PlannerRecommendation> result = engine.Recommend(2);

            Assert.Equal(2, result.Count);
            Assert.Equal(1, result[0].Route.Rank);
            Assert.NotEmpty(result[0].Reasons);
            Assert.Contains(result[0].Reasons, x => x.Contains("Quest is", StringComparison.Ordinal));
            Assert.Contains(result[0].Reasons, x => x.Contains("No immediate prerequisite blockers", StringComparison.Ordinal));
        }

        [Fact]
        public void Recommend_ValidatesTopNBound()
        {
            PlannerClientIndex state = State(Quest("a", 4));
            PlannerTopologyIndex topology = Topology("a");
            PlannerQueryEngine query = new PlannerQueryEngine(topology, state);
            PlannerPathItemPlanner items = new PlannerPathItemPlanner(query, new PlannerRequirementIndex(null), state);
            PlannerRecommendationEngine engine = new PlannerRecommendationEngine(
                new PlannerCandidateSelector(state),
                new PlannerRoutePrioritizer(query, items, state));

            Assert.Throws<ArgumentOutOfRangeException>(() => engine.Recommend(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => engine.Recommend(33));
        }

        private static PlannerTopologyIndex Topology(params string[] questIds)
        {
            Dictionary<string, PlannerTopologyQuest> quests = new Dictionary<string, PlannerTopologyQuest>(StringComparer.Ordinal);
            foreach (string id in questIds)
                quests[id] = new PlannerTopologyQuest(id, null, null, null, false, null, null, null);
            return new PlannerTopologyIndex(quests, null);
        }

        private static PlannerClientIndex State(params PlannerQuestClientState[] quests)
        {
            Dictionary<string, PlannerQuestClientState> map = new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal);
            foreach (PlannerQuestClientState quest in quests) map[quest.QuestId] = quest;
            return new PlannerClientIndex(1, map, null);
        }

        private static PlannerQuestClientState Quest(string id, int disposition)
        {
            return new PlannerQuestClientState(id, disposition, 0, true, true);
        }
    }
}
