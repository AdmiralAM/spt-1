using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerLocationIndexTests
{
    [Fact]
    public void NestedLocationConstraintIsInheritedBySiblingRaidObjective()
    {
        const string json = """
        {
          "schemaVersion": 9,
          "questObjectives": [
            {
              "questId": "q1",
              "conditionId": "kill",
              "conditionType": "Kills",
              "phase": "Finish",
              "parentConditionId": "counter",
              "targets": ["Savage"],
              "locationHints": [],
              "questLocationHint": null
            },
            {
              "questId": "q1",
              "conditionId": "loc",
              "conditionType": "Location",
              "phase": "Finish",
              "parentConditionId": "counter",
              "targets": ["customs"],
              "locationHints": ["customs"],
              "questLocationHint": null
            }
          ]
        }
        """;

        PlannerLocationIndex index = PlannerLocationIndexBuilder.Build(json);
        PlannerLocationBucket customs = Assert.NotNull(index.GetLocation("customs"));
        PlannerLocationObjective kill = Assert.Single(customs.Objectives.Where(value => value.ConditionId == "kill"));
        Assert.Equal(PlannerObjectiveKind.Kill, kill.Kind);
        Assert.Contains("customs", kill.LocationIds, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void QuestLevelLocationProvidesFallback()
    {
        const string json = """
        {
          "schemaVersion": 9,
          "questObjectives": [
            {
              "questId": "q2",
              "conditionId": "visit",
              "conditionType": "VisitPlace",
              "phase": "Finish",
              "parentConditionId": null,
              "targets": ["zone-a"],
              "locationHints": [],
              "questLocationHint": "Woods"
            }
          ]
        }
        """;

        PlannerLocationIndex index = PlannerLocationIndexBuilder.Build(json);
        PlannerLocationBucket woods = Assert.NotNull(index.GetLocation("woods"));
        Assert.Single(woods.Objectives);
        Assert.Equal(PlannerObjectiveKind.Visit, woods.Objectives[0].Kind);
    }

    [Fact]
    public void ObjectiveWithoutLocationRemainsGlobalRatherThanBeingCopiedToEveryMap()
    {
        const string json = """
        {
          "schemaVersion": 9,
          "questObjectives": [
            {
              "questId": "q3",
              "conditionId": "kill-anywhere",
              "conditionType": "Kills",
              "phase": "Finish",
              "parentConditionId": null,
              "targets": ["Savage"],
              "locationHints": [],
              "questLocationHint": null
            }
          ]
        }
        """;

        PlannerLocationIndex index = PlannerLocationIndexBuilder.Build(json);
        Assert.Empty(index.Locations);
        PlannerLocationObjective objective = Assert.Single(index.GlobalObjectives);
        Assert.Equal("q3", objective.QuestId);
    }
}
