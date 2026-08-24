using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerPathItemPlannerTests
{
    [Fact]
    public void BuildForTarget_AllocatesFirAndGenericWithoutDoubleCounting()
    {
        var topology = new PlannerTopologyIndex(
            new Dictionary<string, PlannerTopologyQuest>(StringComparer.Ordinal)
            {
                ["q1"] = new PlannerTopologyQuest("q1", null, null, null, false, Array.Empty<string>(), new[] { "q2" }, new[] { "tpl-a" }),
                ["q2"] = new PlannerTopologyQuest("q2", null, null, null, false, new[] { "q1" }, Array.Empty<string>(), new[] { "tpl-a" })
            },
            new Dictionary<string, PlannerTopologyItem>(StringComparer.Ordinal));

        var state = new PlannerClientIndex(
            1,
            new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal)
            {
                ["q1"] = new PlannerQuestClientState("q1", 4, 2, true, true),
                ["q2"] = new PlannerQuestClientState("q2", 4, 2, true, true)
            },
            new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal)
            {
                ["tpl-a"] = new PlannerItemClientState("tpl-a", 3d, 0d, 2d, 1d, 1d, 0d)
            });

        var requirements = new PlannerRequirementIndex(
            new Dictionary<string, IReadOnlyList<PlannerQuestItemRequirement>>(StringComparer.Ordinal)
            {
                ["q1"] = new[]
                {
                    new PlannerQuestItemRequirement("q1", "fir", new[] { "tpl-a" }, 1d, true, "Finish"),
                    new PlannerQuestItemRequirement("q1", "generic", new[] { "tpl-a" }, 2d, false, "Finish")
                },
                ["q2"] = Array.Empty<PlannerQuestItemRequirement>()
            });

        PlannerPathItemPlan result = Build(topology, state, requirements, "q2");

        Assert.Equal(2, result.ExactNeeds.Count);
        PlannerPathItemNeed fir = Assert.Single(result.ExactNeeds, need => need.FoundInRaid);
        PlannerPathItemNeed generic = Assert.Single(result.ExactNeeds, need => !need.FoundInRaid);

        Assert.Equal("tpl-a", fir.TemplateId);
        Assert.Equal(1d, fir.Required);
        Assert.Equal(1d, fir.OwnedEligible);
        Assert.Equal(0d, fir.Outstanding);

        Assert.Equal("tpl-a", generic.TemplateId);
        Assert.Equal(2d, generic.Required);
        Assert.Equal(1d, generic.OwnedEligible);
        Assert.Equal(1d, generic.Outstanding);

        Assert.Equal(3d, fir.Required + generic.Required);
        Assert.Equal(2d, fir.OwnedEligible + generic.OwnedEligible);
        Assert.Equal(1d, fir.Outstanding + generic.Outstanding);
    }

    [Fact]
    public void BuildForTarget_AlternativeRequirementUsesCombinedOwnedTemplates()
    {
        var topology = SingleQuestTopology("q1", "tpl-a", "tpl-b");
        var state = new PlannerClientIndex(
            1,
            new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal)
            {
                ["q1"] = new PlannerQuestClientState("q1", 4, 2, true, true)
            },
            new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal)
            {
                ["tpl-a"] = new PlannerItemClientState("tpl-a", 0d, 0d, 1d, 0d, 0d, 0d),
                ["tpl-b"] = new PlannerItemClientState("tpl-b", 0d, 0d, 2d, 0d, 0d, 0d)
            });
        var requirements = RequirementIndex("q1",
            new PlannerQuestItemRequirement("q1", "alt", new[] { "tpl-a", "tpl-b" }, 3d, false, "Finish"));

        PlannerPathItemPlan result = Build(topology, state, requirements, "q1");

        PlannerAlternativeItemNeed alternative = Assert.Single(result.AlternativeNeeds);
        Assert.Equal(3d, alternative.OwnedAllocated);
        Assert.Equal(0d, alternative.Outstanding);
        Assert.Equal(2, alternative.Allocations.Count);
    }

    [Fact]
    public void BuildForTarget_OverlappingAlternativesRedistributeForMaximumCoverage()
    {
        var topology = SingleQuestTopology("q1", "tpl-a", "tpl-b");
        var state = new PlannerClientIndex(
            1,
            new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal)
            {
                ["q1"] = new PlannerQuestClientState("q1", 4, 2, true, true)
            },
            new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal)
            {
                ["tpl-a"] = new PlannerItemClientState("tpl-a", 0d, 0d, 2d, 0d, 0d, 0d),
                ["tpl-b"] = new PlannerItemClientState("tpl-b", 0d, 0d, 2d, 0d, 0d, 0d)
            });
        var requirements = RequirementIndex("q1",
            new PlannerQuestItemRequirement("q1", "flex", new[] { "tpl-a", "tpl-b" }, 2d, false, "Finish"),
            new PlannerQuestItemRequirement("q1", "fixed", new[] { "tpl-a" }, 2d, false, "Finish"));

        PlannerPathItemPlan result = Build(topology, state, requirements, "q1");

        Assert.All(result.ExactNeeds, need => Assert.Equal(0d, need.Outstanding));
        PlannerAlternativeItemNeed alternative = Assert.Single(result.AlternativeNeeds);
        Assert.Equal(0d, alternative.Outstanding);
        Assert.Contains(alternative.Allocations, allocation => allocation.TemplateId == "tpl-b" && allocation.Allocated == 2d);
    }

    [Fact]
    public void BuildForTarget_FirStockIsReservedBeforeGenericRequirements()
    {
        var topology = SingleQuestTopology("q1", "tpl-a", "tpl-b");
        var state = new PlannerClientIndex(
            1,
            new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal)
            {
                ["q1"] = new PlannerQuestClientState("q1", 4, 2, true, true)
            },
            new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal)
            {
                ["tpl-a"] = new PlannerItemClientState("tpl-a", 0d, 0d, 2d, 2d, 0d, 0d),
                ["tpl-b"] = new PlannerItemClientState("tpl-b", 0d, 0d, 2d, 0d, 0d, 0d)
            });
        var requirements = RequirementIndex("q1",
            new PlannerQuestItemRequirement("q1", "fir", new[] { "tpl-a" }, 2d, true, "Finish"),
            new PlannerQuestItemRequirement("q1", "generic", new[] { "tpl-a", "tpl-b" }, 2d, false, "Finish"));

        PlannerPathItemPlan result = Build(topology, state, requirements, "q1");

        PlannerPathItemNeed fir = Assert.Single(result.ExactNeeds);
        Assert.Equal(0d, fir.Outstanding);
        PlannerAlternativeItemNeed generic = Assert.Single(result.AlternativeNeeds);
        Assert.Equal(0d, generic.Outstanding);
        Assert.Contains(generic.Allocations, allocation => allocation.TemplateId == "tpl-b" && allocation.Allocated == 2d);
    }

    [Fact]
    public void BuildForTarget_AlternativeShortageReportsExactOutstanding()
    {
        var topology = SingleQuestTopology("q1", "tpl-a", "tpl-b");
        var state = new PlannerClientIndex(
            1,
            new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal)
            {
                ["q1"] = new PlannerQuestClientState("q1", 4, 2, true, true)
            },
            new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal)
            {
                ["tpl-a"] = new PlannerItemClientState("tpl-a", 0d, 0d, 1d, 0d, 0d, 0d),
                ["tpl-b"] = new PlannerItemClientState("tpl-b", 0d, 0d, 0d, 0d, 0d, 0d)
            });
        var requirements = RequirementIndex("q1",
            new PlannerQuestItemRequirement("q1", "alt", new[] { "tpl-a", "tpl-b" }, 3d, false, "Finish"));

        PlannerPathItemPlan result = Build(topology, state, requirements, "q1");

        PlannerAlternativeItemNeed alternative = Assert.Single(result.AlternativeNeeds);
        Assert.Equal(1d, alternative.OwnedAllocated);
        Assert.Equal(2d, alternative.Outstanding);
    }

    private static PlannerPathItemPlan Build(
        PlannerTopologyIndex topology,
        PlannerClientIndex state,
        PlannerRequirementIndex requirements,
        string targetQuestId)
    {
        var query = new PlannerQueryEngine(topology, state);
        var planner = new PlannerPathItemPlanner(query, requirements, state);
        return planner.BuildForTarget(targetQuestId);
    }

    private static PlannerTopologyIndex SingleQuestTopology(string questId, params string[] templates)
    {
        return new PlannerTopologyIndex(
            new Dictionary<string, PlannerTopologyQuest>(StringComparer.Ordinal)
            {
                [questId] = new PlannerTopologyQuest(questId, null, null, null, false, Array.Empty<string>(), Array.Empty<string>(), templates)
            },
            templates.ToDictionary(
                id => id,
                id => new PlannerTopologyItem(id, new[] { questId }),
                StringComparer.Ordinal));
    }

    private static PlannerRequirementIndex RequirementIndex(string questId, params PlannerQuestItemRequirement[] requirements)
    {
        return new PlannerRequirementIndex(
            new Dictionary<string, IReadOnlyList<PlannerQuestItemRequirement>>(StringComparer.Ordinal)
            {
                [questId] = requirements
            });
    }
}
