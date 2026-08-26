using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerRawQuestStatusTransportTests
    {
        [Fact]
        public void TopologyBuilderCarriesExactRawAcceptedStatuses()
        {
            const string json = "{\"questNodes\":[{\"questId\":\"source\"},{\"questId\":\"target\"}],\"prerequisites\":[{\"sourceQuestId\":\"source\",\"targetQuestId\":\"target\",\"acceptedSourceStates\":[\"Started\"],\"acceptedSourceRawStatuses\":[3],\"availableAfterSeconds\":0}],\"itemRequirements\":[]}";

            PlannerTopologyIndex topology = PlannerTopologyIndexBuilder.Build(json);
            PlannerTopologyPrerequisite edge = Assert.Single(topology.GetQuest("target").PrerequisiteEdges);

            Assert.Equal(new[] { 3 }, edge.AcceptedRawProfileStatuses);
            Assert.True(edge.HasRawProfileStatusContract);
            Assert.True(edge.AcceptsRawProfileStatus(3));
            Assert.False(edge.AcceptsRawProfileStatus(2));
        }

        [Fact]
        public void StateBuilderCarriesRawStatusAlongsideNormalizedEvaluationState()
        {
            const string json = "{\"generatedAtUnixSeconds\":1,\"player\":{\"questStates\":{\"source\":{\"questId\":\"source\",\"state\":\"Started\",\"rawStatus\":3}},\"taskConditionCounters\":{}},\"evaluation\":{\"quests\":{\"source\":{\"questId\":\"source\",\"disposition\":4,\"profileState\":3,\"levelGateSatisfied\":true,\"prerequisitesSatisfied\":true}}},\"outstandingItems\":[],\"inventory\":{\"byTemplate\":{}}}";

            PlannerClientIndex state = PlannerClientIndexBuilder.Build(json);
            PlannerQuestClientState source = state.GetQuest("source");

            Assert.NotNull(source);
            Assert.Equal(3, source.ProfileState);
            Assert.Equal(3, source.RawProfileStatus);
        }
    }
}
