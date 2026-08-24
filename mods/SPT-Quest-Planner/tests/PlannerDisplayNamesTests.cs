using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerDisplayNamesTests
{
    [Theory]
    [InlineData("bigmap", "Customs")]
    [InlineData("sandbox", "Ground Zero")]
    [InlineData("tarkovstreets", "Streets of Tarkov")]
    [InlineData("laboratory", "The Lab")]
    public void KnownLocationIdsUseReadableNames(string locationId, string expected)
    {
        Assert.Equal(expected, PlannerDisplayNames.Location(locationId));
    }

    [Fact]
    public void UnknownLocationIdFallsBackWithoutLosingInformation()
    {
        Assert.Equal("modded_map_x", PlannerDisplayNames.Location("modded_map_x"));
    }

    [Theory]
    [InlineData(PlannerRaidObjectiveKind.Kill, "Kill")]
    [InlineData(PlannerRaidObjectiveKind.Visit, "Visit")]
    [InlineData(PlannerRaidObjectiveKind.Plant, "Plant / Mark")]
    [InlineData(PlannerRaidObjectiveKind.Find, "Find / Retrieve")]
    [InlineData(PlannerRaidObjectiveKind.Extract, "Extract / Survive")]
    public void ObjectiveKindsUseReadableLabels(PlannerRaidObjectiveKind kind, string expected)
    {
        Assert.Equal(expected, PlannerDisplayNames.Objective(kind));
    }
}
