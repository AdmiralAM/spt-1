using SPTQuestPlanner.Client;

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
                ["q1"] = new PlannerQuestClientState("q1", 2, 1, true, true),
                ["q2"] = new PlannerQuestClientState("q2", 1, 1, true, false)
            },
            new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal)
            {
                ["tpl-a"] = new PlannerItemClientState("tpl-a", 0, 0, 4, 2, 0, 0)
            });

        string json = """
        {
          "itemRequirements": [
            { "questId": "q1", "conditionId": "c1", "templateIds": ["tpl-a"], "requiredCount": 3, "foundInRaid": true, "phase": "Finish" },
            { "questId": "q2", "conditionId": "c2", "templateIds": ["tpl-a"], "requiredCount": 2, "foundInRaid": false, "phase": "Finish" }
          ]
        }
        """;

        var query = new PlannerQueryEngine(topology, state);
        var planner = new PlannerPathItemPlanner(query, PlannerRequirementIndexBuilder.Build(json), state);
        PlannerPathItemPlan plan = planner.BuildForTarget("q2");

        Assert.True(plan.IsExact);
        Assert.Equal(2, plan.ExactNeeds.Count);
        Assert.Empty(plan.AlternativeNeeds);

        PlannerPathItemNeed fir = Assert.Single(plan.ExactNeeds.Where(x => x.FoundInRaid));
        Assert.Equal(3, fir.Required);
        Assert.Equal(2, fir.OwnedEligible);
        Assert.Equal(1, fir.Outstanding);

        PlannerPathItemNeed generic = Assert.Single(plan.ExactNeeds.Where(x => !x.FoundInRaid));
        Assert.Equal(2, generic.Required);
        Assert.Equal(2, generic.OwnedEligible);
        Assert.Equal(0, generic.Outstanding);
    }

    [Fact]
    public void BuildForTarget_AllocatesAlternativeAcrossAcceptedTemplates()
    {
        PlannerPathItemPlan plan = BuildSingleQuestPlan(
            """
            {
              "itemRequirements": [
                { "questId": "q1", "conditionId": "alt", "templateIds": ["tpl-a", "tpl-b"], "requiredCount": 3, "foundInRaid": false, "phase": "Finish" }
              ]
            }
            """,
            new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal)
            {
                ["tpl-a"] = Item("tpl-a", 1, 0),
                ["tpl-b"] = Item("tpl-b", 2, 0)
            });

        Assert.True(plan.IsExact);
        Assert.True(plan.IsFullyOwned);
        Assert.Empty(plan.ExactNeeds);
        PlannerAlternativeItemNeed alternative = Assert.Single(plan.AlternativeNeeds);
        Assert.Equal(3, alternative.Requirement.RequiredCount);
        Assert.Equal(3, alternative.OwnedAllocated);
        Assert.Equal(0, alternative.Outstanding);
        Assert.Equal(2, alternative.Allocations.Count);
        Assert.Equal(1, Assert.Single(alternative.Allocations.Where(x => x.TemplateId == "tpl-a")).Allocated);
        Assert.Equal(2, Assert.Single(alternative.Allocations.Where(x => x.TemplateId == "tpl-b")).Allocated);
    }

    [Fact]
    public void BuildForTarget_ReassignsOverlappingAlternativesToMaximizeSatisfiedQuantity()
    {
        PlannerPathItemPlan plan = BuildSingleQuestPlan(
            """
            {
              "itemRequirements": [
                { "questId": "q1", "conditionId": "flex", "templateIds": ["tpl-a", "tpl-b"], "requiredCount": 2, "foundInRaid": false, "phase": "Finish" },
                { "questId": "q1", "conditionId": "only-a", "templateIds": ["tpl-a"], "requiredCount": 2, "foundInRaid": false, "phase": "Finish" }
              ]
            }
            """,
            new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal)
            {
                ["tpl-a"] = Item("tpl-a", 2, 0),
                ["tpl-b"] = Item("tpl-b", 2, 0)
            });

        Assert.True(plan.IsFullyOwned);
        PlannerPathItemNeed exact = Assert.Single(plan.ExactNeeds);
        Assert.Equal("tpl-a", exact.TemplateId);
        Assert.Equal(2, exact.OwnedEligible);
        Assert.Equal(0, exact.Outstanding);

        PlannerAlternativeItemNeed flex = Assert.Single(plan.AlternativeNeeds);
        Assert.Equal(2, flex.OwnedAllocated);
        Assert.Equal(0, flex.Outstanding);
        PlannerTemplateAllocation allocation = Assert.Single(flex.Allocations);
        Assert.Equal("tpl-b", allocation.TemplateId);
        Assert.Equal(2, allocation.Allocated);
    }

    [Fact]
    public void BuildForTarget_ReservesFirStockForFirAlternativeBeforeGenericNeed()
    {
        PlannerPathItemPlan plan = BuildSingleQuestPlan(
            """
            {
              "itemRequirements": [
                { "questId": "q1", "conditionId": "fir-alt", "templateIds": ["tpl-a", "tpl-b"], "requiredCount": 2, "foundInRaid": true, "phase": "Finish" },
                { "questId": "q1", "conditionId": "generic-a", "templateIds": ["tpl-a"], "requiredCount": 2, "foundInRaid": false, "phase": "Finish" }
              ]
            }
            """,
            new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal)
            {
                ["tpl-a"] = Item("tpl-a", 3, 1),
                ["tpl-b"] = Item("tpl-b", 1, 1)
            });

        Assert.True(plan.IsFullyOwned);
        PlannerAlternativeItemNeed fir = Assert.Single(plan.AlternativeNeeds);
        Assert.Equal(2, fir.OwnedAllocated);
        Assert.Equal(0, fir.Outstanding);
        Assert.Equal(2, fir.Allocations.Sum(x => x.Allocated));

        PlannerPathItemNeed generic = Assert.Single(plan.ExactNeeds);
        Assert.False(generic.FoundInRaid);
        Assert.Equal(2, generic.OwnedEligible);
        Assert.Equal(0, generic.Outstanding);
    }

    [Fact]
    public void BuildForTarget_ReportsExactOutstandingForAlternativeShortage()
    {
        PlannerPathItemPlan plan = BuildSingleQuestPlan(
            """
            {
              "itemRequirements": [
                { "questId": "q1", "conditionId": "alt", "templateIds": ["tpl-a", "tpl-b"], "requiredCount": 5, "foundInRaid": false, "phase": "Finish" }
              ]
            }
            """,
            new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal)
            {
                ["tpl-a"] = Item("tpl-a", 1, 0),
                ["tpl-b"] = Item("tpl-b", 2, 0)
            });

        Assert.False(plan.IsFullyOwned);
        PlannerAlternativeItemNeed alternative = Assert.Single(plan.AlternativeNeeds);
        Assert.Equal(5, alternative.Requirement.RequiredCount);
        Assert.Equal(3, alternative.OwnedAllocated);
        Assert.Equal(2, alternative.Outstanding);
    }

    private static PlannerPathItemPlan BuildSingleQuestPlan(string requirementJson, IReadOnlyDictionary<string, PlannerItemClientState> items)
    {
        var topology = new PlannerTopologyIndex(
            new Dictionary<string, PlannerTopologyQuest>(StringComparer.Ordinal)
            {
                ["q1"] = new PlannerTopologyQuest("q1", null, null, null, false, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>())
            },
            new Dictionary<string, PlannerTopologyItem>(StringComparer.Ordinal));
        var state = new PlannerClientIndex(
            1,
            new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal)
            {
                ["q1"] = new PlannerQuestClientState("q1", 2, 1, true, true)
            },
            items);
        return new PlannerPathItemPlanner(
            new PlannerQueryEngine(topology, state),
            PlannerRequirementIndexBuilder.Build(requirementJson),
            state).BuildForTarget("q1");
    }

    private static PlannerItemClientState Item(string templateId, double total, double fir)
    {
        return new PlannerItemClientState(templateId, 0, 0, total, fir, 0, 0);
    }
}
