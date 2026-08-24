using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerClientIndexTests
{
    [Fact]
    public void BuildsQuestAndItemLookupIndexesFromStatePayload()
    {
        const string json = "{\"schemaVersion\":8,\"generatedAtUnixSeconds\":1234,\"evaluation\":{\"quests\":{\"quest-a\":{\"questId\":\"quest-a\",\"disposition\":4,\"profileState\":2,\"levelGateSatisfied\":true,\"prerequisitesSatisfied\":true}}},\"outstandingItems\":[{\"templateId\":\"tpl-a\",\"currentRequired\":5,\"futureRequired\":3,\"ownedTotal\":2,\"ownedFoundInRaid\":1,\"currentOutstanding\":3,\"futureOutstandingAfterCurrent\":3}]}";

        PlannerClientIndex index = PlannerClientIndexBuilder.Build(json);

        Assert.Equal(1234, index.GeneratedAtUnixSeconds);
        PlannerQuestClientState quest = Assert.IsType<PlannerQuestClientState>(index.GetQuest("quest-a"));
        Assert.Equal(4, quest.Disposition);
        Assert.True(quest.LevelGateSatisfied);
        Assert.True(quest.PrerequisitesSatisfied);

        PlannerItemClientState item = Assert.IsType<PlannerItemClientState>(index.GetItem("tpl-a"));
        Assert.Equal(5d, item.CurrentRequired);
        Assert.Equal(3d, item.FutureRequired);
        Assert.Equal(2d, item.OwnedTotal);
        Assert.Equal(1d, item.OwnedFoundInRaid);
        Assert.Equal(3d, item.CurrentOutstanding);
        Assert.Equal(3d, item.FutureOutstandingAfterCurrent);
    }
}
