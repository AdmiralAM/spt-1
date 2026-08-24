using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerRaidPlanCollectionTests
{
    [Fact]
    public void BuildsPlansPreparationAndRankingInOnePass()
    {
        PlannerLocationObjective customs = new(
            "q-customs", "visit", "VisitPlace", "Finish", null,
            Array.Empty<string>(), new[] { "Customs" }, PlannerObjectiveKind.Visit);
        PlannerLocationObjective woods = new(
            "q-woods", "beacon", "PlaceBeacon", "Finish", null,
            new[] { "markerTpl" }, new[] { "Woods" }, PlannerObjectiveKind.Plant, 2d);

        PlannerLocationIndex locations = new(
            new Dictionary<string, PlannerLocationBucket>(StringComparer.OrdinalIgnoreCase)
            {
                ["Customs"] = new PlannerLocationBucket("Customs", new[] { customs }),
                ["Woods"] = new PlannerLocationBucket("Woods", new[] { woods })
            },
            Array.Empty<PlannerLocationObjective>());

        PlannerClientIndex state = State(
            123,
            new[] { Quest("q-customs"), Quest("q-woods") },
            new[] { new PlannerOwnedItem("markerTpl", 0d, 0d) });

        PlannerRaidPlanCollection collection = PlannerRaidPlanCollectionBuilder.Build(locations, state);

        Assert.Equal(123, collection.GeneratedAtUnixSeconds);
        Assert.Equal(2, collection.LocationCount);
        Assert.Equal(1, collection.ReadyLocationCount);
        Assert.Equal(2, collection.TotalQuestCount);
        Assert.Equal("Customs", collection.Plans[0].LocationId);
        Assert.True(collection.Plans[0].PreparationReady);
        Assert.False(collection.GetLocation("Woods").PreparationReady);
        Assert.Equal(1, collection.GetLocation("Woods").MissingBringTemplateCount);
    }

    [Fact]
    public void QuestDensityFirstModeFlowsThroughCollectionBuilder()
    {
        PlannerLocationObjective customs = Objective("q1", "a", "Customs");
        PlannerLocationObjective woodsA = Objective("q2", "b", "Woods");
        PlannerLocationObjective woodsB = Objective("q3", "c", "Woods");

        PlannerLocationIndex locations = new(
            new Dictionary<string, PlannerLocationBucket>(StringComparer.OrdinalIgnoreCase)
            {
                ["Customs"] = new PlannerLocationBucket("Customs", new[] { customs }),
                ["Woods"] = new PlannerLocationBucket("Woods", new[] { woodsA, woodsB })
            },
            Array.Empty<PlannerLocationObjective>());

        PlannerClientIndex state = State(7, new[] { Quest("q1"), Quest("q2"), Quest("q3") }, Array.Empty<PlannerOwnedItem>());

        PlannerRaidPlanCollection collection = PlannerRaidPlanCollectionBuilder.Build(
            locations,
            state,
            PlannerRaidPlanRankingMode.QuestDensityFirst);

        Assert.Equal(PlannerRaidPlanRankingMode.QuestDensityFirst, collection.RankingMode);
        Assert.Equal("Woods", collection.Plans[0].LocationId);
        Assert.Equal(2, collection.Plans[0].QuestCount);
    }

    [Fact]
    public void AvailableQuestsRemainOptInAtCollectionBoundary()
    {
        PlannerLocationIndex locations = new(
            new Dictionary<string, PlannerLocationBucket>(StringComparer.OrdinalIgnoreCase)
            {
                ["Customs"] = new PlannerLocationBucket("Customs", new[] { Objective("q1", "a", "Customs") })
            },
            Array.Empty<PlannerLocationObjective>());

        PlannerClientIndex state = new(
            1,
            new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal)
            {
                ["q1"] = new PlannerQuestClientState("q1", 3, 1, true, true)
            },
            new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal));

        Assert.Empty(PlannerRaidPlanCollectionBuilder.Build(locations, state).Plans);
        Assert.Single(PlannerRaidPlanCollectionBuilder.Build(locations, state, includeAvailable: true).Plans);
    }

    private static PlannerLocationObjective Objective(string questId, string conditionId, string location)
    {
        return new PlannerLocationObjective(
            questId, conditionId, "VisitPlace", "Finish", null,
            Array.Empty<string>(), new[] { location }, PlannerObjectiveKind.Visit);
    }

    private static PlannerQuestClientState Quest(string id)
    {
        return new PlannerQuestClientState(id, 4, 2, true, true);
    }

    private static PlannerClientIndex State(long generated, IEnumerable<PlannerQuestClientState> quests, IEnumerable<PlannerOwnedItem> owned)
    {
        return new PlannerClientIndex(
            generated,
            quests.ToDictionary(value => value.QuestId, value => value, StringComparer.Ordinal),
            new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal),
            null,
            owned.ToDictionary(value => value.TemplateId, value => value, StringComparer.Ordinal));
    }
}
