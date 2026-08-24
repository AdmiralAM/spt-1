using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerDisplayNamesTests
{
    [Theory]
    [InlineData("bigmap", "Customs")]
    [InlineData("56f40101d2720b2a4d8b45d6", "Customs")]
    [InlineData("5704e3c2d2720bac5b8b4567", "Woods")]
    [InlineData("5714dbc024597771384a510d", "Interchange")]
    [InlineData("653e6760052c01c1c805532f", "Ground Zero")]
    [InlineData("6733700029c367a3d40b02af", "Labyrinth")]
    [InlineData("sandbox", "Ground Zero")]
    [InlineData("tarkovstreets", "Streets of Tarkov")]
    [InlineData("laboratory", "The Lab")]
    [InlineData(PlannerRaidOpportunityBuilder.AnyLocationId, "Any location")]
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
