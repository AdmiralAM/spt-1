using System;
using System.Collections.Generic;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerRecommendationViewModelTests
    {
        [Fact]
        public void Build_ResolvesQuestLabelsAndUnlockSummary()
        {
            PlannerTopologyIndex topology = Topology();
            PlannerClientIndex state = State();
            PlannerQueryEngine query = new PlannerQueryEngine(topology, state);
            PlannerRecommendationViewModelBuilder builder = new PlannerRecommendationViewModelBuilder(topology, null, query);
            PlannerRecommendation recommendation = new PlannerRecommendation(
                new PlannerRoutePriority("q2", 4, 2, 1, 2d, 1d, 1d, false, 1),
                new[] { "Quest is active now." });

            PlannerRecommendationViewModel vm = Assert.Single(builder.Build(new[] { recommendation }));

            Assert.Equal(1, vm.Rank);
            Assert.Equal("Quest Two", vm.QuestName);
            Assert.Equal("trader-b", vm.TraderId);
            Assert.Equal(new[] { "q1" }, vm.BlockerQuestIds);
            Assert.Equal(new[] { "Quest One" }, vm.BlockerQuestNames);
            Assert.Equal(new[] { "q3" }, vm.ImmediateUnlockQuestIds);
            Assert.Equal(new[] { "Quest Three" }, vm.ImmediateUnlockQuestNames);
            Assert.Equal(3d, vm.TotalOutstanding);
            Assert.Equal(1d, vm.FirOutstanding);
            Assert.False(vm.FullyOwned);
        }

        [Fact]
        public void Build_RejectsOversizedPresentationBatch()
        {
            PlannerTopologyIndex topology = Topology();
            PlannerClientIndex state = State();
            PlannerRecommendationViewModelBuilder builder = new PlannerRecommendationViewModelBuilder(
                topology, null, new PlannerQueryEngine(topology, state));
            List<PlannerRecommendation> values = new List<PlannerRecommendation>();
            for (int i = 0; i < 33; i++)
                values.Add(new PlannerRecommendation(new PlannerRoutePriority("q2", 4, 1, 0, 0d, 0d, 0d, true, i + 1), Array.Empty<string>()));

            Assert.Throws<InvalidOperationException>(() => builder.Build(values));
        }

        private static PlannerTopologyIndex Topology()
        {
            Dictionary<string, PlannerTopologyQuest> quests = new Dictionary<string, PlannerTopologyQuest>(StringComparer.Ordinal)
            {
                ["q1"] = new PlannerTopologyQuest("q1", "trader-a", "Quest One", null, false, Array.Empty<string>(), new[] { "q2" }, Array.Empty<string>()),
                ["q2"] = new PlannerTopologyQuest("q2", "trader-b", "Quest Two", null, false, new[] { "q1" }, new[] { "q3" }, Array.Empty<string>()),
                ["q3"] = new PlannerTopologyQuest("q3", "trader-c", "Quest Three", null, false, new[] { "q2" }, Array.Empty<string>(), Array.Empty<string>())
            };
            return new PlannerTopologyIndex(quests, null);
        }

        private static PlannerClientIndex State()
        {
            Dictionary<string, PlannerQuestClientState> quests = new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal)
            {
                ["q1"] = new PlannerQuestClientState("q1", 2, 0, true, true),
                ["q2"] = new PlannerQuestClientState("q2", 4, 0, true, false),
                ["q3"] = new PlannerQuestClientState("q3", 2, 0, true, false)
            };
            return new PlannerClientIndex(1, quests, null);
        }
    }
}
