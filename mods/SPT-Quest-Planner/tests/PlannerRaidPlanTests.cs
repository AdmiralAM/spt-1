using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerRaidPlanTests
{
    [Fact]
    public void ObjectiveExposesCurrentRequiredAndRemainingProgress()
    {
        PlannerRaidObjective objective = new(
            "q1", "kill", PlannerRaidObjectiveKind.Kill, "Kills", "Customs",
            Array.Empty<string>(), false, 5d, 3d);

        Assert.True(objective.HasProgress);
        Assert.Equal(5d, objective.RequiredValue);
        Assert.Equal(3d, objective.CurrentValue);
        Assert.Equal(2d, objective.RemainingValue);
    }

    [Fact]
    public void RemainingProgressIsClampedAtZero()
    {
        PlannerRaidObjective objective = new(
            "q1", "kill", PlannerRaidObjectiveKind.Kill, "Kills", "Customs",
            Array.Empty<string>(), false, 5d, 8d);

        Assert.Equal(0d, objective.RemainingValue);
    }

    [Fact]
    public void RaidPlanAggregatesKnownRemainingWorkAndPreparation()
    {
        PlannerRaidOpportunity opportunity = new(
            "Customs",
            new[] { "q1", "q2" },
            Array.Empty<PlannerLocationObjective>(),
            new[]
            {
                new PlannerRaidObjective("q1", "kill", PlannerRaidObjectiveKind.Kill, "Kills", "Customs", Array.Empty<string>(), false, 5d, 3d),
                new PlannerRaidObjective("q2", "beacon", PlannerRaidObjectiveKind.Plant, "PlaceBeacon", "Customs", new[] { "tpl-marker" }, false, 2d, 1d)
            },
            2,
            0);

        PlannerClientIndex state = new(
            1,
            new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal),
            new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal),
            new Dictionary<string, PlannerConditionProgress>(StringComparer.Ordinal),
            new Dictionary<string, PlannerOwnedItem>(StringComparer.Ordinal)
            {
                ["tpl-marker"] = new PlannerOwnedItem("tpl-marker", 1d, 0d)
            });

        PlannerRaidPlan plan = PlannerRaidPlanBuilder.Build(opportunity, state);

        Assert.Equal("Customs", plan.LocationId);
        Assert.Equal(2, plan.QuestCount);
        Assert.Equal(2, plan.ObjectiveCount);
        Assert.Equal(2, plan.KnownProgressObjectiveCount);
        Assert.Equal(3d, plan.KnownRemainingWork);
        Assert.True(plan.PreparationReady);
        PlannerRaidBringNeed bring = Assert.Single(plan.Preparation.ExactNeeds);
        Assert.Equal("tpl-marker", bring.TemplateId);
        Assert.Equal(1d, bring.Required);
        Assert.Equal(1d, bring.Owned);
        Assert.Equal(0d, bring.Missing);
    }
}
