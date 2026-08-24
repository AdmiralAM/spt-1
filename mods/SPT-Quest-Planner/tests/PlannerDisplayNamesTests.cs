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
    [InlineData("55f2d3fd4bdc2d5f408b4567", "Factory (Day)")]
    [InlineData("59fc81d786f774390775787e", "Factory (Night)")]
    [InlineData("factory4_day", "Factory (Day)")]
    [InlineData("factory4_night", "Factory (Night)")]
    [InlineData("653e6760052c01c1c805532f", "Ground Zero")]
    [InlineData("6733700029c367a3d40b02af", "Labyrinth")]
    [InlineData("sandbox", "Ground Zero (Level ≤ 20)")]
    [InlineData("sandbox_high", "Ground Zero (Level > 20)")]
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

    [Theory]
    [InlineData("AnyPmc", "PMC")]
    [InlineData("Savage", "Scav")]
    [InlineData("Usec", "USEC")]
    [InlineData("Bear", "BEAR")]
    [InlineData("bigmap", "Customs")]
    public void SemanticTargetsUseReadableLabels(string target, string expected)
    {
        Assert.Equal(expected, PlannerDisplayNames.Target(target));
    }

    [Fact]
    public void ObjectiveActionCombinesVerbAndSemanticTarget()
    {
        PlannerRaidObjective objective = new PlannerRaidObjective(
            "q1",
            "c1",
            PlannerRaidObjectiveKind.Kill,
            "Kills",
            "bigmap",
            new[] { "AnyPmc" },
            false);

        Assert.Equal("Kill PMC", PlannerDisplayNames.ObjectiveAction(objective));
    }

    [Fact]
    public void ObjectiveActionUsesLocalizedItemNameWhenAvailable()
    {
        PlannerLocaleIndex locale = new PlannerLocaleIndex(
            "en",
            new System.Collections.Generic.Dictionary<string, string>(),
            new System.Collections.Generic.Dictionary<string, string> { ["tpl-1"] = "Secure Flash Drive" });
        PlannerRaidObjective objective = new PlannerRaidObjective(
            "q1",
            "c1",
            PlannerRaidObjectiveKind.Find,
            "FindItem",
            "bigmap",
            new[] { "tpl-1" },
            false);

        Assert.Equal("Find / Retrieve Secure Flash Drive", PlannerDisplayNames.ObjectiveAction(objective, locale));
    }

    [Fact]
    public void RuntimeTypeTargetsAreSuppressed()
    {
        Assert.Equal(string.Empty, PlannerDisplayNames.Target("SPTarkov.Server.Core.Utils.Json.ListOrT`1[System.String]"));
    }
}
