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
        PlannerLocationBucket customs = index.GetLocation("customs");
        Assert.NotNull(customs);
        PlannerLocationObjective kill = Assert.Single(customs.Objectives, value => value.ConditionId == "kill");
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
        PlannerLocationBucket woods = index.GetLocation("woods");
        Assert.NotNull(woods);
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

    [Theory]
    [InlineData("FindItem", PlannerObjectiveKind.FindItem)]
    [InlineData("HandoverItem", PlannerObjectiveKind.HandoverItem)]
    [InlineData("PlaceBeacon", PlannerObjectiveKind.Plant)]
    [InlineData("LeaveItemAtLocation", PlannerObjectiveKind.Plant)]
    [InlineData("ExitStatus", PlannerObjectiveKind.Extract)]
    public void KnownConditionTypesMapToExplicitObjectiveKinds(string conditionType, PlannerObjectiveKind expected)
    {
        string json = "{\"schemaVersion\":9,\"questObjectives\":[{" +
            "\"questId\":\"q\",\"conditionId\":\"c\",\"conditionType\":\"" + conditionType +
            "\",\"phase\":\"Finish\",\"parentConditionId\":null,\"targets\":[],\"locationHints\":[],\"questLocationHint\":null}]}";

        PlannerLocationIndex index = PlannerLocationIndexBuilder.Build(json);
        PlannerLocationObjective objective = Assert.Single(index.GlobalObjectives);
        Assert.Equal(expected, objective.Kind);
    }

    [Fact]
    public void UnknownCustomConditionRemainsOther()
    {
        const string json = """
        {
          "schemaVersion": 9,
          "questObjectives": [
            {
              "questId": "q-custom",
              "conditionId": "custom",
              "conditionType": "ModdedObjectiveX",
              "phase": "Finish",
              "parentConditionId": null,
              "targets": [],
              "locationHints": [],
              "questLocationHint": null
            }
          ]
        }
        """;

        PlannerLocationObjective objective = Assert.Single(PlannerLocationIndexBuilder.Build(json).GlobalObjectives);
        Assert.Equal(PlannerObjectiveKind.Other, objective.Kind);
    }
}
