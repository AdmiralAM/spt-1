using System.Collections.Generic;
using SPTQuestPlanner;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class QuestExtractorDictionaryKeyTests
{
    private sealed record FakeMongoId(string Value)
    {
        public override string ToString() => Value;
    }

    [Fact]
    public void Extract_DictionaryWithNonStringKeys_DoesNotSerializeDictionaryKeys()
    {
        var quests = new Dictionary<FakeMongoId, object>
        {
            [new FakeMongoId("quest-key")] = new
            {
                _id = "quest-id",
                traderId = "trader-id",
                QuestName = "Quest name",
                conditions = new
                {
                    AvailableForStart = new object[0],
                    AvailableForFinish = new object[0]
                }
            }
        };

        QuestExtractionResult result = QuestExtractor.Extract(quests);

        Assert.Single(result.Nodes);
        Assert.Equal("quest-id", result.Nodes[0].QuestId);
        Assert.Empty(result.Warnings);
    }
}
