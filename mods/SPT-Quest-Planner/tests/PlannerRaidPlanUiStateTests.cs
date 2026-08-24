using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerRaidPlanUiStateTests
{
    [Fact]
    public void DefaultsSelectionToTopCardAndPreservesExistingLocation()
    {
        PlannerRaidPlanUiState state = new();
        PlannerRaidPlanViewModel first = ViewModel("Customs", "Woods");

        PlannerRaidPlanCard selected = state.ResolveSelection(first);

        Assert.Equal("Customs", selected.LocationId);
        Assert.Equal("Customs", state.SelectedLocationId);

        state.SelectLocation("Woods");
        selected = state.ResolveSelection(first);
        Assert.Equal("Woods", selected.LocationId);

        PlannerRaidPlanViewModel reordered = ViewModel("Shoreline", "Woods");
        selected = state.ResolveSelection(reordered);
        Assert.Equal("Woods", selected.LocationId);
    }

    [Fact]
    public void MissingSelectionFallsBackToCurrentTopCard()
    {
        PlannerRaidPlanUiState state = new();
        state.SelectLocation("Factory");

        PlannerRaidPlanCard selected = state.ResolveSelection(ViewModel("Streets", "Customs"));

        Assert.Equal("Streets", selected.LocationId);
        Assert.Equal("Streets", state.SelectedLocationId);
    }

    [Fact]
    public void EmptyViewModelClearsSelection()
    {
        PlannerRaidPlanUiState state = new();
        state.SelectLocation("Customs");

        PlannerRaidPlanCard selected = state.ResolveSelection(new PlannerRaidPlanViewModel(
            1,
            PlannerRaidPlanRankingMode.ReadyFirst,
            System.Array.Empty<PlannerRaidPlanCard>()));

        Assert.Null(selected);
        Assert.Null(state.SelectedLocationId);
    }

    private static PlannerRaidPlanViewModel ViewModel(params string[] locations)
    {
        PlannerRaidPlanCard[] cards = locations.Select((location, index) => new PlannerRaidPlanCard(
            index + 1,
            location,
            1,
            1,
            true,
            0,
            0,
            System.Array.Empty<PlannerRaidObjective>(),
            System.Array.Empty<PlannerRaidBringNeed>())).ToArray();
        return new PlannerRaidPlanViewModel(1, PlannerRaidPlanRankingMode.ReadyFirst, cards);
    }
}
