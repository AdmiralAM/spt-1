using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerQuestLabelsTests
{
    [Fact]
    public void Resolve_UsesReadableQuestName()
    {
        var topology = new PlannerTopologyIndex(
            new Dictionary<string, PlannerTopologyQuest>(StringComparer.Ordinal)
            {
                ["q1"] = new PlannerTopologyQuest("q1", "trader", "Debut", null, false, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>())
            },
            new Dictionary<string, PlannerTopologyItem>(StringComparer.Ordinal));

        Assert.Equal("Debut", PlannerQuestLabels.Resolve(topology, "q1"));
    }

    [Fact]
    public void Resolve_FallsBackForTechnicalKey()
    {
        const string questId = "665d79148caecc924f38301a";
        var topology = new PlannerTopologyIndex(
            new Dictionary<string, PlannerTopologyQuest>(StringComparer.Ordinal)
            {
                [questId] = new PlannerTopologyQuest(questId, "trader", questId, null, false, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>())
            },
            new Dictionary<string, PlannerTopologyItem>(StringComparer.Ordinal));

        Assert.Equal(questId, PlannerQuestLabels.Resolve(topology, questId));
    }

    [Fact]
    public void Resolve_UnknownQuestUsesId()
    {
        Assert.Equal("q-modded", PlannerQuestLabels.Resolve(null, "q-modded"));
    }
}
