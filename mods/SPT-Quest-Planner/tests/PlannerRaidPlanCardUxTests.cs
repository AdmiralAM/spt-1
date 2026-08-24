using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerRaidPlanCardUxTests
{
    [Fact]
    public void ReadyCardProducesHumanReadablePreparationAndActionSummary()
    {
        PlannerRaidPlanCard card = new PlannerRaidPlanCard(
            1,
            "bigmap",
            4,
            7,
            true,
            0,
            0,
            0d,
            System.Array.Empty<PlannerRaidObjective>(),
            System.Array.Empty<PlannerRaidBringNeed>(),
            killObjectiveCount: 3,
            visitObjectiveCount: 2,
            plantObjectiveCount: 1,
            findObjectiveCount: 1,
            rankReason: "Ready now; then ranked by useful quest and raid-task density.");

        Assert.Equal("Ready", card.PreparationLabel);
        Assert.Contains("3 kills", card.ActionSummary);
        Assert.Contains("2 visits", card.ActionSummary);
        Assert.Contains("1 mark/plant", card.ActionSummary);
        Assert.Contains("1 find", card.ActionSummary);
        Assert.Contains("Ready now", card.RankReason);
    }

    [Fact]
    public void MissingAndAmbiguousPreparationAreNotPresentedAsReady()
    {
        PlannerRaidPlanCard card = new PlannerRaidPlanCard(
            2,
            "woods",
            2,
            2,
            false,
            1,
            2,
            0d,
            System.Array.Empty<PlannerRaidObjective>(),
            System.Array.Empty<PlannerRaidBringNeed>());

        Assert.Equal("Need 1 item type(s); check 2", card.PreparationLabel);
    }

    [Fact]
    public void BuilderCountsActionKindsFromFullPlanNotOnlyCompactObjectiveSlice()
    {
        PlannerRaidObjective[] objectives = new[]
        {
            Objective("q1", "c1", PlannerRaidObjectiveKind.Kill),
            Objective("q2", "c2", PlannerRaidObjectiveKind.Kill),
            Objective("q3", "c3", PlannerRaidObjectiveKind.Visit),
            Objective("q4", "c4", PlannerRaidObjectiveKind.Extract)
        };
        PlannerRaidPlan plan = new PlannerRaidPlan(
            "bigmap",
            new[] { "q1", "q2", "q3", "q4" },
            objectives,
            new PlannerRaidPreparation(System.Array.Empty<PlannerRaidBringNeed>(), System.Array.Empty<PlannerRaidUnresolvedBringNeed>()));
        PlannerRaidPlanCollection collection = new PlannerRaidPlanCollection(
            1,
            PlannerRaidPlanRankingMode.ReadyFirst,
            new[] { plan });

        PlannerRaidPlanCard card = Assert.Single(PlannerRaidPlanViewModelBuilder.Build(collection, maxObjectivesPerCard: 1).Cards);

        Assert.Single(card.Objectives);
        Assert.Equal(4, card.ObjectiveCount);
        Assert.Equal(2, card.KillObjectiveCount);
        Assert.Equal(1, card.VisitObjectiveCount);
        Assert.Equal(1, card.ExtractObjectiveCount);
        Assert.Contains("2 kills", card.ActionSummary);
    }

    private static PlannerRaidObjective Objective(string questId, string conditionId, PlannerRaidObjectiveKind kind)
    {
        return new PlannerRaidObjective(
            questId,
            conditionId,
            kind,
            kind.ToString(),
            "bigmap",
            System.Array.Empty<string>(),
            false);
    }
}
