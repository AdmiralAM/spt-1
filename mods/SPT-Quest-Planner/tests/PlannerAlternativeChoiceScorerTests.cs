using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerAlternativeChoiceScorerTests
{
    [Fact]
    public void Rank_PrefersCandidateWithoutExactOutstandingConflict()
    {
        PlannerQuestItemRequirement requirement = new(
            "quest-a",
            "condition-a",
            new[] { "tpl-a", "tpl-b" },
            2d,
            false,
            "AvailableForFinish");
        PlannerAlternativeItemNeed alternative = new(
            requirement,
            0d,
            2d,
            System.Array.Empty<PlannerTemplateAllocation>());
        PlannerPathItemPlan plan = new(
            new[]
            {
                new PlannerPathItemNeed("tpl-a", 3d, 1d, 2d, false)
            },
            new[] { alternative });

        var ranked = new PlannerAlternativeChoiceScorer().Rank(alternative, plan);

        Assert.Equal("tpl-b", ranked[0].TemplateId);
        Assert.Equal(0d, ranked[0].ExactConflictOutstanding);
        Assert.Equal("tpl-a", ranked[1].TemplateId);
        Assert.Equal(2d, ranked[1].ExactConflictOutstanding);
    }

    [Fact]
    public void Rank_PrefersExistingAllocationWhenConflictIsEqual()
    {
        PlannerQuestItemRequirement requirement = new(
            "quest-a",
            "condition-a",
            new[] { "tpl-a", "tpl-b" },
            3d,
            false,
            "AvailableForFinish");
        PlannerAlternativeItemNeed alternative = new(
            requirement,
            1d,
            2d,
            new[] { new PlannerTemplateAllocation("tpl-b", 1d) });
        PlannerPathItemPlan plan = new(
            System.Array.Empty<PlannerPathItemNeed>(),
            new[] { alternative });

        var ranked = new PlannerAlternativeChoiceScorer().Rank(alternative, plan);

        Assert.Equal("tpl-b", ranked[0].TemplateId);
        Assert.Equal(1d, ranked[0].AlreadyAllocated);
        Assert.Equal(1, ranked[0].Rank);
    }

    [Fact]
    public void Rank_IsDeterministicForEquivalentCandidates()
    {
        PlannerQuestItemRequirement requirement = new(
            "quest-a",
            "condition-a",
            new[] { "tpl-c", "tpl-a", "tpl-b" },
            1d,
            true,
            "AvailableForFinish");
        PlannerAlternativeItemNeed alternative = new(
            requirement,
            0d,
            1d,
            System.Array.Empty<PlannerTemplateAllocation>());
        PlannerPathItemPlan plan = new(
            System.Array.Empty<PlannerPathItemNeed>(),
            new[] { alternative });

        var ranked = new PlannerAlternativeChoiceScorer().Rank(alternative, plan);

        Assert.Equal(new[] { "tpl-a", "tpl-b", "tpl-c" }, System.Linq.Enumerable.Select(ranked, x => x.TemplateId));
    }
}
