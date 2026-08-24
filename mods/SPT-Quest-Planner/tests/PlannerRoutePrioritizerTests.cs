using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerRoutePrioritizerTests
{
    [Fact]
    public void FewerImmediateBlockersWinBeforeItemBurden()
    {
        PlannerRoutePrioritizer prioritizer = Create(
            quests: new[]
            {
                Quest("open", Array.Empty<string>()),
                Quest("pre", Array.Empty<string>(), new[] { "blocked" }),
                Quest("blocked", new[] { "pre" })
            },
            states: new[]
            {
                State("open", 2), State("pre", 2), State("blocked", 1)
            },
            requirements: new[]
            {
                Requirement("open", "open-item", "tpl-open", 5d, false)
            },
            items: Array.Empty<PlannerItemClientState>());

        IReadOnlyList<PlannerRoutePriority> ranked = prioritizer.Rank(new[] { "blocked", "open" });

        Assert.Equal("open", ranked[0].TargetQuestId);
        Assert.Equal(0, ranked[0].ImmediateBlockerCount);
        Assert.Equal(5d, ranked[0].TotalOutstanding);
        Assert.Equal("blocked", ranked[1].TargetQuestId);
        Assert.Equal(1, ranked[1].ImmediateBlockerCount);
    }

    [Fact]
    public void FullyOwnedWinsWhenBlockerCountIsEqual()
    {
        PlannerRoutePrioritizer prioritizer = Create(
            quests: new[] { Quest("owned", Array.Empty<string>()), Quest("missing", Array.Empty<string>()) },
            states: new[] { State("owned", 2), State("missing", 2) },
            requirements: new[]
            {
                Requirement("owned", "owned-item", "tpl-owned", 2d, false),
                Requirement("missing", "missing-item", "tpl-missing", 1d, false)
            },
            items: new[] { Item("tpl-owned", 2d, 0d) });

        IReadOnlyList<PlannerRoutePriority> ranked = prioritizer.Rank(new[] { "missing", "owned" });

        Assert.Equal("owned", ranked[0].TargetQuestId);
        Assert.True(ranked[0].FullyOwned);
        Assert.Equal(0d, ranked[0].TotalOutstanding);
        Assert.False(ranked[1].FullyOwned);
    }

    [Fact]
    public void LowerOutstandingThenLowerFirBurdenWin()
    {
        PlannerRoutePrioritizer prioritizer = Create(
            quests: new[]
            {
                Quest("less", Array.Empty<string>()),
                Quest("generic", Array.Empty<string>()),
                Quest("fir", Array.Empty<string>())
            },
            states: new[] { State("less", 2), State("generic", 2), State("fir", 2) },
            requirements: new[]
            {
                Requirement("less", "a", "tpl-less", 1d, false),
                Requirement("generic", "b", "tpl-generic", 2d, false),
                Requirement("fir", "c", "tpl-fir", 2d, true)
            },
            items: Array.Empty<PlannerItemClientState>());

        IReadOnlyList<PlannerRoutePriority> ranked = prioritizer.Rank(new[] { "fir", "generic", "less" });

        Assert.Equal("less", ranked[0].TargetQuestId);
        Assert.Equal("generic", ranked[1].TargetQuestId);
        Assert.Equal("fir", ranked[2].TargetQuestId);
        Assert.Equal(2d, ranked[2].FirOutstanding);
    }

    [Fact]
    public void CompletedTargetsAreAlwaysLastAndDuplicatesAreIgnored()
    {
        PlannerRoutePrioritizer prioritizer = Create(
            quests: new[] { Quest("active", Array.Empty<string>()), Quest("done", Array.Empty<string>()) },
            states: new[] { State("active", 4), State("done", 5) },
            requirements: Array.Empty<PlannerQuestItemRequirement>(),
            items: Array.Empty<PlannerItemClientState>());

        IReadOnlyList<PlannerRoutePriority> ranked = prioritizer.Rank(new[] { "done", "active", "active" });

        Assert.Equal(2, ranked.Count);
        Assert.Equal("active", ranked[0].TargetQuestId);
        Assert.Equal(1, ranked[0].Rank);
        Assert.Equal("done", ranked[1].TargetQuestId);
        Assert.Equal(2, ranked[1].Rank);
    }

    [Fact]
    public void EqualRoutesUseStableQuestIdTieBreak()
    {
        PlannerRoutePrioritizer prioritizer = Create(
            quests: new[] { Quest("zeta", Array.Empty<string>()), Quest("alpha", Array.Empty<string>()) },
            states: new[] { State("zeta", 2), State("alpha", 2) },
            requirements: Array.Empty<PlannerQuestItemRequirement>(),
            items: Array.Empty<PlannerItemClientState>());

        IReadOnlyList<PlannerRoutePriority> ranked = prioritizer.Rank(new[] { "zeta", "alpha" });

        Assert.Equal("alpha", ranked[0].TargetQuestId);
        Assert.Equal("zeta", ranked[1].TargetQuestId);
    }

    private static PlannerRoutePrioritizer Create(
        IEnumerable<PlannerTopologyQuest> quests,
        IEnumerable<PlannerQuestClientState> states,
        IEnumerable<PlannerQuestItemRequirement> requirements,
        IEnumerable<PlannerItemClientState> items)
    {
        Dictionary<string, PlannerTopologyQuest> questMap = quests.ToDictionary(q => q.QuestId, StringComparer.Ordinal);
        PlannerTopologyIndex topology = new PlannerTopologyIndex(
            questMap,
            new Dictionary<string, PlannerTopologyItem>(StringComparer.Ordinal));

        PlannerClientIndex state = new PlannerClientIndex(
            1,
            states.ToDictionary(q => q.QuestId, StringComparer.Ordinal),
            items.ToDictionary(i => i.TemplateId, StringComparer.Ordinal));

        Dictionary<string, IReadOnlyList<PlannerQuestItemRequirement>> byQuest = requirements
            .GroupBy(r => r.QuestId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PlannerQuestItemRequirement>)g.ToArray(), StringComparer.Ordinal);
        PlannerRequirementIndex requirementIndex = new PlannerRequirementIndex(byQuest);
        PlannerQueryEngine query = new PlannerQueryEngine(topology, state);
        PlannerPathItemPlanner itemPlanner = new PlannerPathItemPlanner(query, requirementIndex, state);
        return new PlannerRoutePrioritizer(query, itemPlanner, state);
    }

    private static PlannerTopologyQuest Quest(
        string id,
        IReadOnlyList<string> prerequisites,
        IReadOnlyList<string>? dependents = null)
    {
        return new PlannerTopologyQuest(
            id,
            "trader",
            id,
            null,
            false,
            prerequisites,
            dependents ?? Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static PlannerQuestClientState State(string id, int disposition)
    {
        return new PlannerQuestClientState(id, disposition, 0, true, disposition != 1);
    }

    private static PlannerQuestItemRequirement Requirement(
        string questId,
        string conditionId,
        string templateId,
        double count,
        bool fir)
    {
        return new PlannerQuestItemRequirement(
            questId,
            conditionId,
            new[] { templateId },
            count,
            fir,
            "AvailableForFinish");
    }

    private static PlannerItemClientState Item(string templateId, double total, double fir)
    {
        return new PlannerItemClientState(templateId, 0d, 0d, total, fir, 0d, 0d);
    }
}
