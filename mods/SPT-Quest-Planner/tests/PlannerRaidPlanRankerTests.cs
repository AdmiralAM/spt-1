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
    public void QuestDensityFirstPreservesQuestCountAsPrimaryCriterion()
    {
        PlannerRaidPlan ready = Plan("Customs", 4, ready: true, missingTemplates: 0);
        PlannerRaidPlan blocked = Plan("Woods", 6, ready: false, missingTemplates: 2);

        IReadOnlyList<PlannerRaidPlan> ranked = PlannerRaidPlanRanker.Rank(
            new[] { ready, blocked }, PlannerRaidPlanRankingMode.QuestDensityFirst);

        Assert.Equal("Woods", ranked[0].LocationId);
    }

    [Fact]
    public void ReadyFirstUsesMissingTemplateCountBeforeQuestDensity()
    {
        PlannerRaidPlan fewerMissing = Plan("Customs", 2, ready: false, missingTemplates: 1);
        PlannerRaidPlan moreMissing = Plan("Woods", 7, ready: false, missingTemplates: 3);

        IReadOnlyList<PlannerRaidPlan> ranked = PlannerRaidPlanRanker.Rank(new[] { moreMissing, fewerMissing });

        Assert.Equal("Customs", ranked[0].LocationId);
    }

    private static PlannerRaidPlan Plan(string location, int questCount, bool ready, int missingTemplates)
    {
        string[] quests = Enumerable.Range(0, questCount).Select(i => location + "-q" + i).ToArray();
        PlannerRaidPreparation preparation;
        if (ready)
        {
            preparation = new PlannerRaidPreparation(Array.Empty<PlannerRaidBringNeed>(), Array.Empty<PlannerRaidUnresolvedBringNeed>());
        }
        else
        {
            PlannerRaidBringNeed[] needs = Enumerable.Range(0, missingTemplates)
                .Select(i => new PlannerRaidBringNeed("tpl-" + i, 1d, 0d, 1d, quests))
                .ToArray();
            preparation = new PlannerRaidPreparation(needs, Array.Empty<PlannerRaidUnresolvedBringNeed>());
        }

        return new PlannerRaidPlan(location, quests, Array.Empty<PlannerRaidObjective>(), preparation);
    }
}
