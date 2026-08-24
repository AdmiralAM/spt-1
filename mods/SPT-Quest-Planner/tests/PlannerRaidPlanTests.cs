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
    public void RaidPlanAggregatesKnownRemainingWork()
    {
        PlannerRaidOpportunity opportunity = new(
            "Customs",
            new[] { "q1", "q2" },
            Array.Empty<PlannerLocationObjective>(),
            new[]
            {
                new PlannerRaidObjective("q1", "kill", PlannerRaidObjectiveKind.Kill, "Kills", "Customs", Array.Empty<string>(), false, 5d, 3d),
                new PlannerRaidObjective("q2", "visit", PlannerRaidObjectiveKind.Visit, "VisitPlace", "Customs", Array.Empty<string>(), false)
            },
            2,
            0);

        PlannerRaidPlan plan = PlannerRaidPlanBuilder.Build(opportunity);

        Assert.Equal("Customs", plan.LocationId);
        Assert.Equal(2, plan.QuestCount);
        Assert.Equal(2, plan.ObjectiveCount);
        Assert.Equal(1, plan.KnownProgressObjectiveCount);
        Assert.Equal(2d, plan.KnownRemainingWork);
    }
}
