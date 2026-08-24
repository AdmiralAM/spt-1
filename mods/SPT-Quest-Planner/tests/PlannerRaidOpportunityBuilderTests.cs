using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerRaidOpportunityBuilderTests
{
    [Fact]
    public void RanksLocationByNumberOfActiveQuestOpportunities()
    {
        PlannerLocationObjective customsA = Objective("q1", "a", "Customs");
        PlannerLocationObjective customsB = Objective("q2", "b", "Customs");
        PlannerLocationObjective woods = Objective("q3", "c", "Woods");
        PlannerLocationIndex locations = new(
            new Dictionary<string, PlannerLocationBucket>(StringComparer.OrdinalIgnoreCase)
            {
                ["Customs"] = new PlannerLocationBucket("Customs", new[] { customsA, customsB }),
                ["Woods"] = new PlannerLocationBucket("Woods", new[] { woods })
            },
            Array.Empty<PlannerLocationObjective>());
        PlannerClientIndex state = State(
            Quest("q1", 4),
            Quest("q2", 4),
            Quest("q3", 4));

        IReadOnlyList<PlannerRaidOpportunity> result = PlannerRaidOpportunityBuilder.Build(locations, state);

        Assert.Equal("Customs", result[0].LocationId);
        Assert.Equal(2, result[0].QuestCount);
        Assert.Equal(2, result[0].VisitCount);
        Assert.Equal("Woods", result[1].LocationId);
    }

    [Fact]
    public void AvailableQuestIsExcludedUnlessExplicitlyRequested()
    {
        PlannerLocationIndex locations = new(
            new Dictionary<string, PlannerLocationBucket>(StringComparer.OrdinalIgnoreCase)
            {
                ["Customs"] = new PlannerLocationBucket("Customs", new[] { Objective("q1", "a", "Customs") })
            },
            Array.Empty<PlannerLocationObjective>());
        PlannerClientIndex state = State(Quest("q1", 3));

        Assert.Empty(PlannerRaidOpportunityBuilder.Build(locations, state));
        Assert.Single(PlannerRaidOpportunityBuilder.Build(locations, state, includeAvailable: true));
    }

    [Fact]
    public void GlobalActiveObjectiveIsAddedToEveryRelevantSpecificLocation()
    {
        PlannerLocationObjective globalKill = new(
            "qGlobal", "kill-any", "Kills", "Finish", null,
            new[] { "Savage" }, Array.Empty<string>(), PlannerObjectiveKind.Kill);
        PlannerLocationIndex locations = new(
            new Dictionary<string, PlannerLocationBucket>(StringComparer.OrdinalIgnoreCase)
            {
                ["Customs"] = new PlannerLocationBucket("Customs", new[] { Objective("qCustoms", "visit", "Customs") }),
                ["Woods"] = new PlannerLocationBucket("Woods", new[] { Objective("qWoods", "visit", "Woods") })
            },
            new[] { globalKill });
        PlannerClientIndex state = State(
            Quest("qGlobal", 4),
            Quest("qCustoms", 4),
            Quest("qWoods", 4));

        IReadOnlyList<PlannerRaidOpportunity> result = PlannerRaidOpportunityBuilder.Build(locations, state);

        Assert.Equal(2, result.Count);
        Assert.All(result, value => Assert.Contains(value.Objectives, objective => objective.QuestId == "qGlobal"));
        Assert.All(result, value => Assert.Equal(1, value.GlobalObjectiveCount));
        Assert.All(result, value => Assert.Equal(1, value.KillCount));
        Assert.All(result, value => Assert.Contains(value.RaidObjectives, objective => objective.QuestId == "qGlobal" && objective.Global));
    }

    [Fact]
    public void ConstraintAndCounterContainerAreNotShownAsRaidTasks()
    {
        PlannerLocationObjective constraint = new(
            "q1", "loc", "Location", "Finish", "counter", new[] { "Customs" }, new[] { "Customs" }, PlannerObjectiveKind.LocationConstraint);
        PlannerLocationObjective container = new(
            "q1", "counter", "CounterCreator", "Finish", null, Array.Empty<string>(), new[] { "Customs" }, PlannerObjectiveKind.Other);
        PlannerLocationObjective kill = new(
            "q1", "kill", "Kills", "Finish", "counter", new[] { "Savage" }, new[] { "Customs" }, PlannerObjectiveKind.Kill);
        PlannerLocationIndex locations = new(
            new Dictionary<string, PlannerLocationBucket>(StringComparer.OrdinalIgnoreCase)
            {
                ["Customs"] = new PlannerLocationBucket("Customs", new[] { constraint, container, kill })
            },
            Array.Empty<PlannerLocationObjective>());

        PlannerRaidOpportunity opportunity = Assert.Single(PlannerRaidOpportunityBuilder.Build(locations, State(Quest("q1", 4))));
        PlannerLocationObjective only = Assert.Single(opportunity.Objectives);
        Assert.Equal("kill", only.ConditionId);
        Assert.Equal(1, opportunity.KillCount);
        Assert.Equal(0, opportunity.OtherCount);
    }

    [Fact]
    public void GlobalHandoverIsNotCopiedIntoRaidPlans()
    {
        PlannerLocationObjective handover = new(
            "qHandover", "hand", "HandoverItem", "Finish", null,
            new[] { "tpl" }, Array.Empty<string>(), PlannerObjectiveKind.HandoverItem);
        PlannerLocationIndex locations = new(
            new Dictionary<string, PlannerLocationBucket>(StringComparer.OrdinalIgnoreCase)
            {
                ["Customs"] = new PlannerLocationBucket("Customs", new[] { Objective("qCustoms", "visit", "Customs") })
            },
            new[] { handover });

        PlannerRaidOpportunity opportunity = Assert.Single(PlannerRaidOpportunityBuilder.Build(
            locations,
            State(Quest("qCustoms", 4), Quest("qHandover", 4))));

        Assert.DoesNotContain(opportunity.Objectives, objective => objective.QuestId == "qHandover");
        Assert.DoesNotContain(opportunity.RaidObjectives, objective => objective.QuestId == "qHandover");
        Assert.Equal(0, opportunity.GlobalObjectiveCount);
    }

    [Fact]
    public void GlobalFindItemRemainsRaidActionable()
    {
        PlannerLocationObjective find = new(
            "qFind", "find", "FindItem", "Finish", null,
            new[] { "tpl" }, Array.Empty<string>(), PlannerObjectiveKind.FindItem);
        PlannerLocationIndex locations = new(
            new Dictionary<string, PlannerLocationBucket>(StringComparer.OrdinalIgnoreCase)
            {
                ["Woods"] = new PlannerLocationBucket("Woods", new[] { Objective("qWoods", "visit", "Woods") })
            },
            new[] { find });

        PlannerRaidOpportunity opportunity = Assert.Single(PlannerRaidOpportunityBuilder.Build(
            locations,
            State(Quest("qWoods", 4), Quest("qFind", 4))));

        Assert.Contains(opportunity.Objectives, objective => objective.QuestId == "qFind");
        Assert.Equal(1, opportunity.GlobalObjectiveCount);
        Assert.Equal(1, opportunity.FindCount);
    }

    [Fact]
    public void CompletedCounterObjectiveIsExcludedUsingParentCounterProgress()
    {
        PlannerLocationObjective kill = new(
            "qKill", "kill-child", "Kills", "Finish", "kill-counter",
            new[] { "Savage" }, new[] { "Customs" }, PlannerObjectiveKind.Kill, 5d);
        PlannerLocationIndex locations = new(
            new Dictionary<string, PlannerLocationBucket>(StringComparer.OrdinalIgnoreCase)
            {
                ["Customs"] = new PlannerLocationBucket("Customs", new[] { kill })
            },
            Array.Empty<PlannerLocationObjective>());
        PlannerConditionProgress completed = new("kill-counter", "Elimination", 5d, "qKill");
        PlannerClientIndex state = StateWithProgress(new[] { Quest("qKill", 4) }, completed);

        Assert.Empty(PlannerRaidOpportunityBuilder.Build(locations, state));
    }

    [Fact]
    public void PartialCounterObjectiveRemainsInRaidPlan()
    {
        PlannerLocationObjective kill = new(
            "qKill", "kill-child", "Kills", "Finish", "kill-counter",
            new[] { "Savage" }, new[] { "Customs" }, PlannerObjectiveKind.Kill, 5d);
        PlannerLocationIndex locations = new(
            new Dictionary<string, PlannerLocationBucket>(StringComparer.OrdinalIgnoreCase)
            {
                ["Customs"] = new PlannerLocationBucket("Customs", new[] { kill })
            },
            Array.Empty<PlannerLocationObjective>());
        PlannerConditionProgress partial = new("kill-counter", "Elimination", 4d, "qKill");
        PlannerClientIndex state = StateWithProgress(new[] { Quest("qKill", 4) }, partial);

        PlannerRaidOpportunity opportunity = Assert.Single(PlannerRaidOpportunityBuilder.Build(locations, state));
        Assert.Single(opportunity.Objectives);
        Assert.Equal(1, opportunity.KillCount);
    }

    private static PlannerLocationObjective Objective(string questId, string conditionId, string location)
    {
        return new PlannerLocationObjective(
            questId, conditionId, "VisitPlace", "Finish", null,
            Array.Empty<string>(), new[] { location }, PlannerObjectiveKind.Visit);
    }

    private static PlannerQuestClientState Quest(string id, int disposition)
    {
        return new PlannerQuestClientState(id, disposition, 2, true, true);
    }

    private static PlannerClientIndex State(params PlannerQuestClientState[] quests)
    {
        return StateWithProgress(quests);
    }

    private static PlannerClientIndex StateWithProgress(
        IEnumerable<PlannerQuestClientState> quests,
        params PlannerConditionProgress[] progress)
    {
        return new PlannerClientIndex(
            1,
            quests.ToDictionary(value => value.QuestId, value => value, StringComparer.Ordinal),
            new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal),
            progress.ToDictionary(value => value.CounterId, value => value, StringComparer.Ordinal));
    }
}
