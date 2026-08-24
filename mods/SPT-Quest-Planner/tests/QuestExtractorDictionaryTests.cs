using System.Collections.Generic;
using SPTQuestPlanner;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class QuestExtractorDictionaryTests
    {
        private readonly record struct FakeMongoId(string Value);

        [Fact]
        public void Extract_DictionaryWithNonStringKey_DoesNotSerializeKeys()
        {
            Dictionary<FakeMongoId, object> raw = new()
            {
                [new FakeMongoId("abc")] = new
                {
                    _id = "quest-a",
                    traderId = "trader-a",
                    QuestName = "Quest A",
                    conditions = new
                    {
                        AvailableForStart = System.Array.Empty<object>(),
                        AvailableForFinish = System.Array.Empty<object>()
                    }
                }
            };

            QuestExtractionResult result = QuestExtractor.Extract(raw);

            Assert.Single(result.Nodes);
            Assert.Equal("quest-a", result.Nodes[0].QuestId);
            Assert.Empty(result.Prerequisites);
        }
    }
}
