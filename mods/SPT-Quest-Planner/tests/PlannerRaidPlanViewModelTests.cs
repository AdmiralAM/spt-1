using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerRaidPlanViewModelTests
{
    [Fact]
    public void BuildsRankedCardsAndBoundsObjectivePayload()
    {
        PlannerRaidPreparation ready = new(
            new[] { new PlannerRaidBringNeed("marker", 1d, 1d, 0d, new[] { "q1" }) },
            Array.Empty<PlannerRaidUnresolvedBringNeed>());
        PlannerRaidPlan first = new(
            "Customs",
            new[] { "q1", "q2" },
            new[]
            {
                new PlannerRaidObjective("q1", "a", PlannerRaidObjectiveKind.Kill, "Kills", "Customs", Array.Empty<string>(), false, 5d, 3d),
                new PlannerRaidObjective("q2", "b", PlannerRaidObjectiveKind.Visit, "VisitPlace", "Customs", Array.Empty<string>(), false),
                new PlannerRaidObjective("q2", "c", PlannerRaidObjectiveKind.Extract, "Extract", "Customs", Array.Empty<string>(), false)
            },
            ready);
        PlannerRaidPlan second = new(
            "Woods",
            new[] { "q3" },
            Array.Empty<PlannerRaidObjective>(),
            new PlannerRaidPreparation(Array.Empty<PlannerRaidBringNeed>(), Array.Empty<PlannerRaidUnresolvedBringNeed>()));
        PlannerRaidPlanCollection collection = new(
            1234,
            PlannerRaidPlanRankingMode.ReadyFirst,
            new[] { first, second });

        PlannerRaidPlanViewModel view = PlannerRaidPlanViewModelBuilder.Build(collection, maxObjectivesPerCard: 2);

        Assert.Equal(2, view.LocationCount);
        Assert.Equal("Customs", view.TopRecommendation!.LocationId);
        Assert.Equal(1, view.Cards[0].Rank);
        Assert.Equal(2, view.Cards[0].Objectives.Count);
        Assert.True(view.Cards[0].PreparationReady);
        Assert.Single(view.Cards[0].BringNeeds);
        Assert.Equal(2d, view.Cards[0].KnownRemainingWork);
    }
}
