using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerGraphTests
{
    [Fact]
    public void LongLinearChainRemainsReachableAndAcyclic()
    {
        const int count = 1702;
        QuestNode[] nodes = Enumerable.Range(0, count)
            .Select(i => new QuestNode($"q{i}", null, null, null, false))
            .ToArray();

        PrerequisiteEdge[] edges = Enumerable.Range(0, count - 1)
            .Select(i => new PrerequisiteEdge(
                $"q{i}",
                $"q{i + 1}",
                new HashSet<QuestState> { QuestState.Success }))
            .ToArray();

        var (graph, validation) = PlannerGraph.Build(nodes, edges);

        Assert.True(validation.IsValid);
        Assert.Empty(validation.Cycles);
        Assert.Empty(validation.DanglingEdges);
        Assert.Equal(count - 1, graph.GetReachableDependents("q0").Count);
    }

    [Fact]
    public void CycleIsReported()
    {
        QuestNode[] nodes =
        {
            new("a", null, null, null, false),
            new("b", null, null, null, false),
            new("c", null, null, null, false)
        };

        PrerequisiteEdge[] edges =
        {
            new("a", "b", new HashSet<QuestState> { QuestState.Success }),
            new("b", "c", new HashSet<QuestState> { QuestState.Success }),
            new("c", "a", new HashSet<QuestState> { QuestState.Success })
        };

        var (_, validation) = PlannerGraph.Build(nodes, edges);

        Assert.False(validation.IsValid);
        Assert.NotEmpty(validation.Cycles);
    }

    [Fact]
    public void MissingPrerequisiteTargetIsReportedAsDangling()
    {
        QuestNode[] nodes = { new("existing", null, null, null, false) };
        PrerequisiteEdge[] edges =
        {
            new("missing", "existing", new HashSet<QuestState> { QuestState.Success })
        };

        var (_, validation) = PlannerGraph.Build(nodes, edges);

        Assert.Single(validation.DanglingEdges);
    }
}
