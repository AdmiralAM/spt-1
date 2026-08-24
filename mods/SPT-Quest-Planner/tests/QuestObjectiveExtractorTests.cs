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

        Assert.Contains(result.Objectives, value =>
            value.QuestId == "q1" && value.ConditionId == "kill" && value.ParentConditionId == "counter-root" && value.QuestLocationHint == "Customs");
        Assert.Contains(result.Objectives, value =>
            value.ConditionId == "loc" && value.LocationHints.Contains("customs-id"));
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
}
