using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerClientIndexTests
{
    [Fact]
    public void BuildsQuestItemConditionAndInventoryLookupIndexesFromStatePayload()
    {
        const string json = "{\"schemaVersion\":9,\"generatedAtUnixSeconds\":1234,\"player\":{\"taskConditionCounters\":{\"counter-a\":{\"counterId\":\"counter-a\",\"type\":\"Elimination\",\"value\":7,\"sourceQuestId\":\"quest-a\"}}},\"inventory\":{\"byTemplate\":{\"markerTpl\":{\"templateId\":\"markerTpl\",\"total\":4,\"foundInRaid\":1}}},\"evaluation\":{\"quests\":{\"quest-a\":{\"questId\":\"quest-a\",\"disposition\":4,\"profileState\":2,\"levelGateSatisfied\":true,\"prerequisitesSatisfied\":true}}},\"outstandingItems\":[{\"templateId\":\"tpl-a\",\"currentRequired\":5,\"futureRequired\":3,\"ownedTotal\":2,\"ownedFoundInRaid\":1,\"currentOutstanding\":3,\"futureOutstandingAfterCurrent\":3}]}";

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

        PlannerConditionProgress progress = Assert.IsType<PlannerConditionProgress>(index.GetConditionProgress("counter-a"));
        Assert.Equal(7d, progress.Value);
        Assert.Equal("Elimination", progress.Type);
        Assert.Equal("quest-a", progress.SourceQuestId);

        PlannerOwnedItem marker = Assert.IsType<PlannerOwnedItem>(index.GetOwnedItem("markerTpl"));
        Assert.Equal(4d, marker.Total);
        Assert.Equal(1d, marker.FoundInRaid);
    }
}
