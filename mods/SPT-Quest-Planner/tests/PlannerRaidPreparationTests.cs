using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerRaidPreparationTests
{
    [Fact]
    public void PlaceBeaconUsesRemainingCountAndInventoryOwnership()
    {
        PlannerRaidObjective objective = new(
            "q1", "place", PlannerRaidObjectiveKind.Plant, "PlaceBeacon", "Customs",
            new[] { "markerTpl" }, false, 3d, 1d);
        PlannerRaidPlan plan = new("Customs", new[] { "q1" }, new[] { objective });
        PlannerClientIndex state = State(new PlannerOwnedItem("markerTpl", 1d, 0d));

        PlannerRaidPreparation result = PlannerRaidPreparationBuilder.Build(plan, state);

        PlannerRaidBringNeed need = Assert.Single(result.ExactNeeds);
        Assert.Equal("markerTpl", need.TemplateId);
        Assert.Equal(2d, need.Required);
        Assert.Equal(1d, need.Owned);
        Assert.Equal(1d, need.Missing);
        Assert.False(result.Ready);
    }

    [Fact]
    public void SameMarkerAcrossObjectivesIsAggregatedBeforeOwnershipCheck()
    {
        PlannerRaidPlan plan = new(
            "Customs",
            new[] { "q1", "q2" },
            new[]
            {
                new PlannerRaidObjective("q1", "a", PlannerRaidObjectiveKind.Plant, "PlaceBeacon", "Customs", new[] { "markerTpl" }, false, 1d, 0d),
                new PlannerRaidObjective("q2", "b", PlannerRaidObjectiveKind.Plant, "PlaceBeacon", "Customs", new[] { "markerTpl" }, false, 2d, 0d)
            });

        PlannerRaidBringNeed need = Assert.Single(PlannerRaidPreparationBuilder.Build(plan, State(new PlannerOwnedItem("markerTpl", 2d, 0d))).ExactNeeds);
        Assert.Equal(3d, need.Required);
        Assert.Equal(1d, need.Missing);
    }

    [Fact]
    public void LeaveItemAtLocationIsAProvenBringRequirement()
    {
        PlannerRaidPlan plan = new(
            "Woods",
            new[] { "q1" },
            new[] { new PlannerRaidObjective("q1", "leave", PlannerRaidObjectiveKind.Plant, "LeaveItemAtLocation", "Woods", new[] { "itemTpl" }, false, 2d, 0d) });

        PlannerRaidPreparation result = PlannerRaidPreparationBuilder.Build(plan, State(new PlannerOwnedItem("itemTpl", 2d, 0d)));
        Assert.True(result.Ready);
        Assert.Equal(2d, Assert.Single(result.ExactNeeds).Owned);
    }

    [Fact]
    public void MultipleAcceptedTargetsRemainUnresolvedInsteadOfBeingDoubleCounted()
    {
        PlannerRaidPlan plan = new(
            "Woods",
            new[] { "q1" },
            new[] { new PlannerRaidObjective("q1", "leave", PlannerRaidObjectiveKind.Plant, "LeaveItemAtLocation", "Woods", new[] { "a", "b" }, false, 2d, 0d) });

        PlannerRaidPreparation result = PlannerRaidPreparationBuilder.Build(plan, State());
        Assert.Empty(result.ExactNeeds);
        PlannerRaidUnresolvedBringNeed unresolved = Assert.Single(result.UnresolvedNeeds);
        Assert.Equal(2, unresolved.TemplateIds.Count);
        Assert.Equal(2d, unresolved.Required);
    }

    [Fact]
    public void UnprovenPlantTypeIsNotInventedAsBringRequirement()
    {
        PlannerRaidPlan plan = new(
            "Customs",
            new[] { "q1" },
            new[] { new PlannerRaidObjective("q1", "mark", PlannerRaidObjectiveKind.Plant, "MarkObject", "Customs", new[] { "zone-id" }, false, 1d, 0d) });

        PlannerRaidPreparation result = PlannerRaidPreparationBuilder.Build(plan, State());
        Assert.Empty(result.ExactNeeds);
        Assert.Empty(result.UnresolvedNeeds);
        Assert.True(result.Ready);
    }

    private static PlannerClientIndex State(params PlannerOwnedItem[] owned)
    {
        return new PlannerClientIndex(
            1,
            new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal),
            new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal),
            null,
            owned.ToDictionary(value => value.TemplateId, value => value, StringComparer.Ordinal));
    }
}
