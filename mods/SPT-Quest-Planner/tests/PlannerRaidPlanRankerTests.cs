using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerRaidPlanRankerTests
{
    [Fact]
    public void ReadyFirstPrefersPreparedRaidOverDenserBlockedRaid()
    {
        PlannerRaidPlan ready = Plan("Customs", 4, ready: true, missingTemplates: 0);
        PlannerRaidPlan blocked = Plan("Woods", 6, ready: false, missingTemplates: 2);

        IReadOnlyList<PlannerRaidPlan> ranked = PlannerRaidPlanRanker.Rank(new[] { blocked, ready });

        Assert.Equal("Customs", ranked[0].LocationId);
        Assert.Equal("Woods", ranked[1].LocationId);
    }

    [Fact]
    public void QuestDensityFirstPreservesQuestCountAsPrimaryCriterionForSpecificMaps()
    {
        PlannerRaidPlan ready = Plan("Customs", 4, ready: true, missingTemplates: 0);
        PlannerRaidPlan blocked = Plan("Woods", 6, ready: false, missingTemplates: 2);

        IReadOnlyList<PlannerRaidPlan> ranked = PlannerRaidPlanRanker.Rank(
            new[] { ready, blocked }, PlannerRaidPlanRankingMode.QuestDensityFirst);

        Assert.Equal("Woods", ranked[0].LocationId);
    }

    [Theory]
    [InlineData(PlannerRaidPlanRankingMode.ReadyFirst)]
    [InlineData(PlannerRaidPlanRankingMode.QuestDensityFirst)]
    public void AnyLocationPlanRemainsSupplementalEvenWhenDenser(PlannerRaidPlanRankingMode mode)
    {
        PlannerRaidPlan specific = Plan("Customs", 2, ready: false, missingTemplates: 2);
        PlannerRaidPlan any = Plan(PlannerRaidOpportunityBuilder.AnyLocationId, 30, ready: true, missingTemplates: 0);

        IReadOnlyList<PlannerRaidPlan> ranked = PlannerRaidPlanRanker.Rank(new[] { any, specific }, mode);

        Assert.Equal("Customs", ranked[0].LocationId);
        Assert.Equal(PlannerRaidOpportunityBuilder.AnyLocationId, ranked[1].LocationId);
    }

    [Fact]
    public void ReadyFirstUsesMissingTemplateCountBeforeQuestDensity()
    {
        PlannerRaidPlan fewerMissing = Plan("Customs", 2, ready: false, missingTemplates: 1);
        PlannerRaidPlan moreMissing = Plan("Woods", 7, ready: false, missingTemplates: 3);

        IReadOnlyList<PlannerRaidPlan> ranked = PlannerRaidPlanRanker.Rank(new[] { moreMissing, fewerMissing });

        Assert.Equal("Customs", ranked[0].LocationId);
    }

    [Fact]
    public void ReadyFirstUsesUnresolvedPreparationCountBeforeQuestDensity()
    {
        PlannerRaidPlan fewerChecks = Plan("Customs", 2, ready: false, missingTemplates: 0, unresolved: 1);
        PlannerRaidPlan moreChecks = Plan("Woods", 7, ready: false, missingTemplates: 0, unresolved: 4);

        IReadOnlyList<PlannerRaidPlan> ranked = PlannerRaidPlanRanker.Rank(new[] { moreChecks, fewerChecks });

        Assert.Equal("Customs", ranked[0].LocationId);
        Assert.Equal(1, ranked[0].UnresolvedPreparationCount);
    }

    private static PlannerRaidPlan Plan(string location, int questCount, bool ready, int missingTemplates, int unresolved = 0)
    {
        string[] quests = Enumerable.Range(0, questCount).Select(i => location + "-q" + i).ToArray();
        PlannerRaidBringNeed[] needs = ready
            ? Array.Empty<PlannerRaidBringNeed>()
            : Enumerable.Range(0, missingTemplates)
                .Select(i => new PlannerRaidBringNeed("tpl-" + i, 1d, 0d, 1d, quests))
                .ToArray();
        PlannerRaidUnresolvedBringNeed[] checks = ready
            ? Array.Empty<PlannerRaidUnresolvedBringNeed>()
            : Enumerable.Range(0, unresolved)
                .Select(i => new PlannerRaidUnresolvedBringNeed(quests[0], "cond-" + i, "PlaceBeacon", new[] { "a", "b" }, 1d))
                .ToArray();
        PlannerRaidPreparation preparation = new PlannerRaidPreparation(needs, checks);
        return new PlannerRaidPlan(location, quests, Array.Empty<PlannerRaidObjective>(), preparation);
    }
}
