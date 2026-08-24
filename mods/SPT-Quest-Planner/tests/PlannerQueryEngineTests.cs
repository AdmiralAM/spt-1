using System;
using System.Collections.Generic;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerQueryEngineTests
{
    [Fact]
    public void FindsImmediateBlockersAndHypotheticalUnlocks()
    {
        PlannerTopologyIndex topology = BuildTopology(
            ("q1", Array.Empty<string>(), new[] { "q3" }),
            ("q2", Array.Empty<string>(), new[] { "q3" }),
            ("q3", new[] { "q1", "q2" }, Array.Empty<string>()));
        PlannerClientIndex state = BuildState(("q1", 5), ("q2", 2), ("q3", 1));
        PlannerQueryEngine engine = new PlannerQueryEngine(topology, state);

        Assert.Equal(new[] { "q2" }, engine.GetImmediateBlockers("q3"));
        Assert.Empty(engine.GetImmediateUnlocksIfCompleted("q1"));
        Assert.Equal(new[] { "q3" }, engine.GetImmediateUnlocksIfCompleted("q2"));
    }

    [Fact]
    public void BuildsIncompletePrerequisitePlanInDependencyOrder()
    {
        PlannerTopologyIndex topology = BuildTopology(
            ("q0", Array.Empty<string>(), new[] { "q1" }),
            ("q1", new[] { "q0" }, new[] { "q2" }),
            ("q2", new[] { "q1" }, new[] { "q3" }),
            ("q3", new[] { "q2" }, Array.Empty<string>()));
        PlannerClientIndex state = BuildState(("q0", 5), ("q1", 5), ("q2", 2), ("q3", 1));
        PlannerQueryEngine engine = new PlannerQueryEngine(topology, state);

        Assert.Equal(new[] { "q2", "q3" }, engine.GetIncompletePrerequisitePlan("q3"));
        Assert.Equal(new[] { "q2" }, engine.GetIncompleteAncestors("q3"));
    }

    [Fact]
    public void HandlesQuestManiacScaleChainIteratively()
    {
        const int count = 1702;
        Dictionary<string, PlannerTopologyQuest> quests = new Dictionary<string, PlannerTopologyQuest>(StringComparer.Ordinal);
        Dictionary<string, PlannerQuestClientState> states = new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            string id = "q" + i;
            string[] prerequisites = i == 0 ? Array.Empty<string>() : new[] { "q" + (i - 1) };
            string[] dependents = i == count - 1 ? Array.Empty<string>() : new[] { "q" + (i + 1) };
            quests[id] = new PlannerTopologyQuest(id, null, null, null, false, prerequisites, dependents, Array.Empty<string>());
            states[id] = new PlannerQuestClientState(id, i < 2 ? 5 : 1, 0, false, false);
        }

        PlannerQueryEngine engine = new PlannerQueryEngine(
            new PlannerTopologyIndex(quests, new Dictionary<string, PlannerTopologyItem>(StringComparer.Ordinal)),
            new PlannerClientIndex(1, states, new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal)));

        IReadOnlyList<string> plan = engine.GetIncompletePrerequisitePlan("q1701");
        Assert.Equal(1700, plan.Count);
        Assert.Equal("q2", plan[0]);
        Assert.Equal("q1701", plan[plan.Count - 1]);
    }

    [Fact]
    public void EnforcesTraversalLimit()
    {
        PlannerTopologyIndex topology = BuildTopology(
            ("q0", Array.Empty<string>(), new[] { "q1" }),
            ("q1", new[] { "q0" }, new[] { "q2" }),
            ("q2", new[] { "q1" }, Array.Empty<string>()));
        PlannerClientIndex state = BuildState(("q0", 1), ("q1", 1), ("q2", 1));
        PlannerQueryEngine engine = new PlannerQueryEngine(topology, state, 1);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => engine.GetIncompletePrerequisitePlan("q2"));
        Assert.Contains("traversal limit", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static PlannerTopologyIndex BuildTopology(params (string Id, string[] Prerequisites, string[] Dependents)[] definitions)
    {
        Dictionary<string, PlannerTopologyQuest> quests = new Dictionary<string, PlannerTopologyQuest>(StringComparer.Ordinal);
        foreach ((string id, string[] prerequisites, string[] dependents) in definitions)
            quests[id] = new PlannerTopologyQuest(id, null, null, null, false, prerequisites, dependents, Array.Empty<string>());
        return new PlannerTopologyIndex(quests, new Dictionary<string, PlannerTopologyItem>(StringComparer.Ordinal));
    }

    private static PlannerClientIndex BuildState(params (string Id, int Disposition)[] definitions)
    {
        Dictionary<string, PlannerQuestClientState> quests = new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal);
        foreach ((string id, int disposition) in definitions)
            quests[id] = new PlannerQuestClientState(id, disposition, 0, false, false);
        return new PlannerClientIndex(1, quests, new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal));
    }
}
