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
    public void BuildForTarget_LeavesAlternativeTemplateConditionUnresolved()
    {
        var topology = new PlannerTopologyIndex(
            new Dictionary<string, PlannerTopologyQuest>(StringComparer.Ordinal)
            {
                ["q1"] = new PlannerTopologyQuest("q1", null, null, null, false, Array.Empty<string>(), Array.Empty<string>(), new[] { "tpl-a", "tpl-b" })
            },
            new Dictionary<string, PlannerTopologyItem>(StringComparer.Ordinal));
        var state = new PlannerClientIndex(
            1,
            new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal)
            {
                ["q1"] = new PlannerQuestClientState("q1", 2, 1, true, true)
            },
            new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal));

        string json = """
        {
          "itemRequirements": [
            { "questId": "q1", "conditionId": "alt", "templateIds": ["tpl-a", "tpl-b"], "requiredCount": 1, "foundInRaid": false, "phase": "Finish" }
          ]
        }
        """;

        var planner = new PlannerPathItemPlanner(
            new PlannerQueryEngine(topology, state),
            PlannerRequirementIndexBuilder.Build(json),
            state);
        PlannerPathItemPlan plan = planner.BuildForTarget("q1");

        Assert.False(plan.IsExact);
        Assert.Empty(plan.ExactNeeds);
        PlannerQuestItemRequirement unresolved = Assert.Single(plan.UnresolvedAlternatives);
        Assert.Equal(new[] { "tpl-a", "tpl-b" }, unresolved.TemplateIds);
        Assert.Equal(1, unresolved.RequiredCount);
    }
}
