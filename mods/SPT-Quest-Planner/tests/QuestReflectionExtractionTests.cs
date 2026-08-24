using SPTQuestPlanner;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class QuestReflectionExtractionTests
{
    private sealed class NonStringKey
    {
        private readonly string value;
        public NonStringKey(string value) { this.value = value; }
        public override string ToString() => value;
    }

    private sealed class QuestFixture
    {
        public string _id { get; init; } = string.Empty;
        public string traderId { get; init; } = string.Empty;
        public string QuestName { get; init; } = string.Empty;
        public string location { get; init; } = string.Empty;
        public object conditions { get; init; } = new object();

        // Deliberately impossible for default System.Text.Json dictionary-key serialization.
        // The extractor must never serialize the whole Quest object just to read known fields.
        public Dictionary<NonStringKey, string> DangerousMongoLikeDictionary { get; init; } = new();
    }

    [Fact]
    public void QuestExtractor_ReadsKnownFieldsWithoutSerializingWholeQuest()
    {
        QuestFixture quest = new()
        {
            _id = "q1",
            traderId = "trader",
            QuestName = "Reflection quest",
            conditions = new
            {
                AvailableForStart = new object[]
                {
                    new { conditionType = "Level", value = 12 },
                    new { conditionType = "Quest", target = "q0", status = new[] { 4 }, id = "pre" }
                },
                AvailableForFinish = new object[]
                {
                    new { conditionType = "HandoverItem", target = new[] { "tpl-a" }, value = 2, onlyFoundInRaid = true, id = "item" }
                }
            },
            DangerousMongoLikeDictionary = new Dictionary<NonStringKey, string>
            {
                [new NonStringKey("mongo")] = "value"
            }
        };

        Dictionary<NonStringKey, QuestFixture> raw = new() { [new NonStringKey("q1")] = quest };
        QuestExtractionResult result = QuestExtractor.Extract(raw);

        QuestNode node = Assert.Single(result.Nodes);
        Assert.Equal("q1", node.QuestId);
        Assert.Equal(12, node.MinimumLevel);
        Assert.Single(result.Prerequisites);
        ItemRequirement requirement = Assert.Single(result.ItemRequirements);
        Assert.Equal("tpl-a", Assert.Single(requirement.TemplateIds));
        Assert.Equal(2d, requirement.RequiredCount);
        Assert.True(requirement.FoundInRaid);
    }

    [Fact]
    public void ObjectiveExtractor_ReadsNestedObjectivesWithoutSerializingWholeQuest()
    {
        QuestFixture quest = new()
        {
            _id = "q2",
            location = "bigmap",
            conditions = new
            {
                AvailableForStart = Array.Empty<object>(),
                AvailableForFinish = new object[]
                {
                    new
                    {
                        conditionType = "CounterCreator",
                        id = "parent",
                        value = 3,
                        counter = new
                        {
                            conditions = new object[]
                            {
                                new { conditionType = "VisitPlace", id = "child", target = new[] { "zone-a" }, location = "bigmap" }
                            }
                        }
                    }
                }
            },
            DangerousMongoLikeDictionary = new Dictionary<NonStringKey, string>
            {
                [new NonStringKey("mongo")] = "value"
            }
        };

        Dictionary<NonStringKey, QuestFixture> raw = new() { [new NonStringKey("q2")] = quest };
        QuestObjectiveExtractionResult result = QuestObjectiveExtractor.Extract(raw);

        QuestObjectiveFact child = Assert.Single(result.Objectives, x => x.ConditionId == "child");
        Assert.Equal("q2", child.QuestId);
        Assert.Equal("parent", child.ParentConditionId);
        Assert.Equal(3d, child.RequiredValue);
        Assert.Contains("bigmap", child.LocationHints);
    }
}
