using SPTQuestPlanner;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerTransportJsonTests
{
    [Fact]
    public void TopologyEnvelope_IsCamelCaseThroughNestedRecords()
    {
        PlannerTopologyEnvelope envelope = new(
            PlannerDataContract.SchemaVersion,
            new[] { new QuestNode("q1", "trader", "name", 5, false) },
            Array.Empty<PrerequisiteEdge>(),
            new[] { new ItemRequirement("q1", "c1", new[] { "tpl1" }, 2d, true, "Finish") },
            new[] { new QuestObjectiveFact("q1", "c1", "HandoverItem", "Finish", null, new[] { "tpl1" }, new[] { "woods" }, "woods", 2d) },
            new PlannerGraphValidation(Array.Empty<string>(), Array.Empty<PrerequisiteEdge>(), Array.Empty<IReadOnlyList<string>>()),
            Array.Empty<string>());

        string json = PlannerTransportJson.Serialize(envelope);

        Assert.Contains("\"schemaVersion\":9", json);
        Assert.Contains("\"questNodes\"", json);
        Assert.Contains("\"questId\":\"q1\"", json);
        Assert.Contains("\"itemRequirements\"", json);
        Assert.Contains("\"templateIds\":[\"tpl1\"]", json);
        Assert.Contains("\"questObjectives\"", json);
        Assert.DoesNotContain("\"SchemaVersion\"", json);
        Assert.DoesNotContain("\"QuestId\"", json);
    }
}
