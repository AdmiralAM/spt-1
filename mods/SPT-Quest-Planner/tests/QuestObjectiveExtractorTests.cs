using SPTQuestPlanner;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class QuestObjectiveExtractorTests
{
    [Fact]
    public void ExtractsQuestLevelLocationAndNestedCounterFacts()
    {
        var raw = new
        {
            q1 = new
            {
                _id = "q1",
                location = "Customs",
                conditions = new
                {
                    AvailableForStart = Array.Empty<object>(),
                    AvailableForFinish = new object[]
                    {
                        new
                        {
                            id = "counter-root",
                            conditionType = "CounterCreator",
                            value = 5,
                            counter = new
                            {
                                conditions = new object[]
                                {
                                    new { id = "kill", conditionType = "Kills", target = "Savage" },
                                    new { id = "loc", conditionType = "Location", target = new [] { "customs-id" } }
                                }
                            }
                        }
                    }
                }
            }
        };

        QuestObjectiveExtractionResult result = QuestObjectiveExtractor.Extract(raw);

        QuestObjectiveFact kill = Assert.Single(result.Objectives.Where(value => value.ConditionId == "kill"));
        Assert.Equal("q1", kill.QuestId);
        Assert.Equal("counter-root", kill.ParentConditionId);
        Assert.Equal("Customs", kill.QuestLocationHint);
        Assert.Equal(5d, kill.RequiredValue);
        Assert.Contains(result.Objectives, value =>
            value.ConditionId == "loc" && value.LocationHints.Contains("customs-id") && value.RequiredValue == 5d);
    }

    [Fact]
    public void UnwrapsSptStyleListOrTAndMongoIdValues()
    {
        var raw = new
        {
            q1 = new
            {
                _id = new FakeMongoId("quest-mongo"),
                location = "any",
                conditions = new
                {
                    AvailableForStart = Array.Empty<object>(),
                    AvailableForFinish = new object[]
                    {
                        new
                        {
                            id = new FakeMongoId("condition-mongo"),
                            conditionType = "Location",
                            target = new FakeListOrT<string>(new List<string> { "bigmap", "Woods" }, null)
                        },
                        new
                        {
                            id = new FakeMongoId("single-target"),
                            conditionType = "Kills",
                            target = new FakeListOrT<string>(null, "Savage")
                        }
                    }
                }
            }
        };

        QuestObjectiveExtractionResult result = QuestObjectiveExtractor.Extract(raw);

        QuestObjectiveFact location = Assert.Single(result.Objectives, value => value.ConditionId == "condition-mongo");
        Assert.Equal("quest-mongo", location.QuestId);
        Assert.Equal(new[] { "bigmap", "Woods" }, location.Targets);
        Assert.Contains("bigmap", location.LocationHints);
        Assert.Contains("Woods", location.LocationHints);

        QuestObjectiveFact kill = Assert.Single(result.Objectives, value => value.ConditionId == "single-target");
        Assert.Equal(new[] { "Savage" }, kill.Targets);
    }

    [Fact]
    public void ChildThresholdOverridesParentThresholdWhenExplicit()
    {
        var raw = new
        {
            q1 = new
            {
                _id = "q1",
                conditions = new
                {
                    AvailableForStart = Array.Empty<object>(),
                    AvailableForFinish = new object[]
                    {
                        new
                        {
                            id = "counter-root",
                            conditionType = "CounterCreator",
                            value = 10,
                            counter = new
                            {
                                conditions = new object[]
                                {
                                    new { id = "child", conditionType = "Kills", value = 3, target = "Savage" }
                                }
                            }
                        }
                    }
                }
            }
        };

        QuestObjectiveFact child = Assert.Single(QuestObjectiveExtractor.Extract(raw).Objectives, value => value.ConditionId == "child");
        Assert.Equal(3d, child.RequiredValue);
    }

    [Fact]
    public void AnyQuestLocationIsNotPromotedToSpecificLocation()
    {
        var raw = new
        {
            q1 = new
            {
                _id = "q1",
                location = "any",
                conditions = new
                {
                    AvailableForStart = Array.Empty<object>(),
                    AvailableForFinish = new object[]
                    {
                        new { id = "kill", conditionType = "Kills", target = "Savage" }
                    }
                }
            }
        };

        QuestObjectiveExtractionResult result = QuestObjectiveExtractor.Extract(raw);
        QuestObjectiveFact objective = Assert.Single(result.Objectives);
        Assert.Null(objective.QuestLocationHint);
        Assert.Empty(objective.LocationHints);
    }

    private readonly struct FakeMongoId
    {
        private readonly string value;
        public FakeMongoId(string value) { this.value = value; }
        public override string ToString() { return value; }
    }

    private sealed class FakeListOrT<T>
    {
        public FakeListOrT(List<T> list, T item)
        {
            List = list;
            Item = item;
        }
        public List<T> List { get; private set; }
        public T Item { get; private set; }
    }
}
