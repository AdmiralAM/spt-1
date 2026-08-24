using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerRaidObjectiveTests
{
    [Theory]
    [InlineData("Kills", PlannerRaidObjectiveKind.Kill)]
    [InlineData("VisitPlace", PlannerRaidObjectiveKind.Visit)]
    [InlineData("PlaceBeacon", PlannerRaidObjectiveKind.Plant)]
    [InlineData("LeaveItemAtLocation", PlannerRaidObjectiveKind.Plant)]
    [InlineData("FindItem", PlannerRaidObjectiveKind.Find)]
    [InlineData("ExitStatus", PlannerRaidObjectiveKind.Extract)]
    [InlineData("HandoverItem", PlannerRaidObjectiveKind.Bring)]
    [InlineData("CustomModCondition", PlannerRaidObjectiveKind.Other)]
    public void KnownConditionTypesMapConservatively(string conditionType, PlannerRaidObjectiveKind expected)
    {
        Assert.Equal(expected, PlannerRaidObjectiveNormalizer.Classify(conditionType));
    }

    [Fact]
    public void NormalizerPreservesLocationTargetsAndGlobalFlag()
    {
        PlannerLocationObjective objective = new(
            "q1",
            "c1",
            "Kills",
            "Finish",
            null,
            new[] { "Savage" },
            new[] { "bigmap" },
            PlannerObjectiveKind.Kill);

        PlannerRaidObjective normalized = PlannerRaidObjectiveNormalizer.Normalize(objective, "bigmap");

        Assert.Equal(PlannerRaidObjectiveKind.Kill, normalized.Kind);
        Assert.Equal("bigmap", normalized.LocationId);
        Assert.False(normalized.Global);
        Assert.Equal(new[] { "Savage" }, normalized.Targets);
    }

    [Fact]
    public void UnknownConditionRemainsOtherInsteadOfBeingGuessed()
    {
        PlannerLocationObjective objective = new(
            "q1", "c1", "ModSpecificThing", "Finish", null,
            Array.Empty<string>(), Array.Empty<string>(), PlannerObjectiveKind.Other);

        PlannerRaidObjective normalized = PlannerRaidObjectiveNormalizer.Normalize(objective, "woods");

        Assert.Equal(PlannerRaidObjectiveKind.Other, normalized.Kind);
        Assert.True(normalized.Global);
    }
}
