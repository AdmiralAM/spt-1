using System.Threading;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class ClientRefreshTests
{
    [Fact]
    public void TopologyLoadsOnceWhileStateMayRefreshRepeatedly()
    {
        FakeTransport transport = new();
        FakeDecoder decoder = new();
        PlannerClientCache cache = new();
        PlannerRefreshCoordinator coordinator = new(transport, decoder, cache);

        Assert.True(coordinator.TryRefreshState(CancellationToken.None, out string? firstError), firstError);
        Assert.True(coordinator.TryRefreshState(CancellationToken.None, out string? secondError), secondError);

        Assert.Equal(1, transport.TopologyCalls);
        Assert.Equal(2, transport.StateCalls);
        Assert.True(cache.HasTopology);
        Assert.True(cache.HasState);
        Assert.NotNull(cache.TopologyIndex);
        Assert.NotNull(cache.RequirementIndex);
        Assert.NotNull(cache.Index);
        Assert.Equal(3, cache.Revision); // topology once + two state swaps
    }

    [Fact]
    public void OlderStatePayloadCannotReplaceNewerStateOrTypedIndex()
    {
        PlannerClientCache cache = new();
        PlannerClientIndex newer = new(200, new Dictionary<string, PlannerQuestClientState>(), new Dictionary<string, PlannerItemClientState>());
        PlannerClientIndex older = new(100, new Dictionary<string, PlannerQuestClientState>(), new Dictionary<string, PlannerItemClientState>());
        cache.ReplaceState(new PlannerPayload(8, 200, "new"), newer);
        cache.ReplaceState(new PlannerPayload(8, 100, "old"), older);

        Assert.Equal(200, cache.State!.GeneratedAtUnixSeconds);
        Assert.Equal("new", cache.State.Json);
        Assert.Same(newer, cache.Index);
        Assert.Equal(1, cache.Revision);
    }

    private sealed class FakeTransport : IPlannerTransport
    {
        public int TopologyCalls { get; private set; }
        public int StateCalls { get; private set; }

        public string GetJson(string route)
        {
            if (route == PlannerClientContract.TopologyRoute)
            {
                TopologyCalls++;
                return "topology";
            }

            if (route == PlannerClientContract.StateRoute)
            {
                StateCalls++;
                return "state-" + StateCalls;
            }

            throw new Xunit.Sdk.XunitException("Unexpected route: " + route);
        }
    }

    private sealed class FakeDecoder : IPlannerPayloadDecoder
    {
        private long stateRevision = 100;

        public PlannerPayload DecodeTopology(string json)
        {
            const string payload = "{\"schemaVersion\":8,\"questNodes\":[{\"questId\":\"q1\",\"repeatable\":false}],\"prerequisites\":[],\"itemRequirements\":[]}";
            return new PlannerPayload(PlannerClientContract.SchemaVersion, 0, payload);
        }

        public PlannerPayload DecodeState(string json)
        {
            long generated = ++stateRevision;
            string payload = "{\"schemaVersion\":8,\"generatedAtUnixSeconds\":" + generated + ",\"evaluation\":{\"quests\":{}},\"outstandingItems\":[]}";
            return new PlannerPayload(PlannerClientContract.SchemaVersion, generated, payload);
        }
    }
}
