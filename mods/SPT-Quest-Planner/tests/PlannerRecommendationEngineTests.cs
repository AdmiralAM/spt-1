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
            PlannerCandidateSelector selector = new PlannerCandidateSelector(state);
            PlannerRoutePrioritizer prioritizer = new PlannerRoutePrioritizer(
                new FakeQueryEngine(),
                new FakePathItemPlanner(),
                state);
            PlannerRecommendationEngine engine = new PlannerRecommendationEngine(selector, prioritizer);

            IReadOnlyList<PlannerRecommendation> result = engine.Recommend(2);

            Assert.Equal(2, result.Count);
            Assert.Equal(1, result[0].Route.Rank);
            Assert.NotEmpty(result[0].Reasons);
            Assert.Contains(result[0].Reasons, x => x.Contains("Quest is", StringComparison.Ordinal));
        }

        [Fact]
        public void Recommend_ValidatesTopNBound()
        {
            PlannerClientIndex state = State(Quest("a", 4));
            PlannerRecommendationEngine engine = new PlannerRecommendationEngine(
                new PlannerCandidateSelector(state),
                new PlannerRoutePrioritizer(new FakeQueryEngine(), new FakePathItemPlanner(), state));

            Assert.Throws<ArgumentOutOfRangeException>(() => engine.Recommend(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => engine.Recommend(33));
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

        private sealed class FakeQueryEngine : PlannerQueryEngine
        {
            public FakeQueryEngine() : base(null, null) { }
        }

        private sealed class FakePathItemPlanner : PlannerPathItemPlanner
        {
            public FakePathItemPlanner() : base(null, null, null) { }
        }
    }
}
