using System.Text;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerTopologyIndexTests
{
    [Fact]
    public void BuildsBidirectionalQuestAndItemIndexes()
    {
        const string json = "{\"schemaVersion\":8,\"questNodes\":[{\"questId\":\"q1\",\"traderId\":\"t1\",\"nameKey\":\"n1\",\"minimumLevel\":5,\"repeatable\":false},{\"questId\":\"q2\",\"traderId\":\"t1\",\"nameKey\":\"n2\",\"minimumLevel\":6,\"repeatable\":false}],\"prerequisites\":[{\"sourceQuestId\":\"q1\",\"targetQuestId\":\"q2\",\"acceptedSourceStates\":[4]}],\"itemRequirements\":[{\"questId\":\"q2\",\"conditionId\":\"c1\",\"templateIds\":[\"tpl-a\",\"tpl-b\"],\"requiredCount\":2,\"foundInRaid\":true,\"phase\":\"Finish\"}]}";

        PlannerTopologyIndex index = PlannerTopologyIndexBuilder.Build(json);

        Assert.Equal(2, index.Quests.Count);
        PlannerTopologyQuest q1 = Assert.IsType<PlannerTopologyQuest>(index.GetQuest("q1"));
        PlannerTopologyQuest q2 = Assert.IsType<PlannerTopologyQuest>(index.GetQuest("q2"));
        Assert.Equal(new[] { "q2" }, q1.DependentQuestIds);
        Assert.Equal(new[] { "q1" }, q2.PrerequisiteQuestIds);
        Assert.Equal(new[] { "tpl-a", "tpl-b" }, q2.RequiredTemplateIds);
        Assert.Equal(5, q1.MinimumLevel);

        PlannerTopologyItem item = Assert.IsType<PlannerTopologyItem>(index.GetItem("tpl-a"));
        Assert.Equal(new[] { "q2" }, item.QuestIds);
    }

    [Fact]
    public void DeduplicatesRepeatedEdgesAndTemplateLinks()
    {
        const string json = "{\"schemaVersion\":8,\"questNodes\":[{\"questId\":\"q1\",\"repeatable\":false},{\"questId\":\"q2\",\"repeatable\":false}],\"prerequisites\":[{\"sourceQuestId\":\"q1\",\"targetQuestId\":\"q2\"},{\"sourceQuestId\":\"q1\",\"targetQuestId\":\"q2\"}],\"itemRequirements\":[{\"questId\":\"q2\",\"templateIds\":[\"tpl-a\",\"tpl-a\"]}]}";

        PlannerTopologyIndex index = PlannerTopologyIndexBuilder.Build(json);

        Assert.Single(index.GetQuest("q1")!.DependentQuestIds);
        Assert.Single(index.GetQuest("q2")!.PrerequisiteQuestIds);
        Assert.Single(index.GetQuest("q2")!.RequiredTemplateIds);
        Assert.Single(index.GetItem("tpl-a")!.QuestIds);
    }

    [Fact]
    public void MaterializesQuestManiacScaleLinearChain()
    {
        const int count = 1702;
        StringBuilder json = new StringBuilder();
        json.Append("{\"schemaVersion\":8,\"questNodes\":[");
        for (int i = 0; i < count; i++)
        {
            if (i > 0) json.Append(',');
            json.Append("{\"questId\":\"q").Append(i).Append("\",\"repeatable\":false}");
        }
        json.Append("],\"prerequisites\":[");
        for (int i = 1; i < count; i++)
        {
            if (i > 1) json.Append(',');
            json.Append("{\"sourceQuestId\":\"q").Append(i - 1).Append("\",\"targetQuestId\":\"q").Append(i).Append("\"}");
        }
        json.Append("],\"itemRequirements\":[]}");

        PlannerTopologyIndex index = PlannerTopologyIndexBuilder.Build(json.ToString());

        Assert.Equal(count, index.Quests.Count);
        Assert.Empty(index.GetQuest("q0")!.PrerequisiteQuestIds);
        Assert.Equal(new[] { "q1" }, index.GetQuest("q0")!.DependentQuestIds);
        Assert.Equal(new[] { "q1700" }, index.GetQuest("q1701")!.PrerequisiteQuestIds);
        Assert.Empty(index.GetQuest("q1701")!.DependentQuestIds);
    }
}
