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

    [Fact]
    public void RankingAvailabilityAndWorkspaceSettingsAreExplicitAndStable()
    {
        PlannerRaidPlanUiState state = new();

        Assert.Equal(PlannerRaidPlanRankingMode.ReadyFirst, state.RankingMode);
        Assert.Equal(PlannerWorkspaceMode.RaidPlanner, state.WorkspaceMode);
        Assert.False(state.IncludeAvailable);

        state.SetRankingMode(PlannerRaidPlanRankingMode.QuestDensityFirst);
        state.SetWorkspaceMode(PlannerWorkspaceMode.Progression);
        state.SetIncludeAvailable(true);

        Assert.Equal(PlannerRaidPlanRankingMode.QuestDensityFirst, state.RankingMode);
        Assert.Equal(PlannerWorkspaceMode.Progression, state.WorkspaceMode);
        Assert.True(state.IncludeAvailable);
    }

    [Fact]
    public void ActiveRaidPlanPersistsIndependentlyFromPreviewSelection()
    {
        PlannerRaidPlanUiState state = new();
        state.SelectLocation("Customs");
        state.ActivateSelected();
        state.SelectLocation("Woods");

        Assert.Equal("Customs", state.ActiveLocationId);
        Assert.Equal("Woods", state.SelectedLocationId);
        Assert.True(state.HasActivePlan);
    }

    [Fact]
    public void ActivatingLocationAlsoSelectsItForImmediatePreview()
    {
        PlannerRaidPlanUiState state = new();
        state.SelectLocation("Woods");

        state.ActivateLocation("Reserve");

        Assert.Equal("Reserve", state.ActiveLocationId);
        Assert.Equal("Reserve", state.SelectedLocationId);
        Assert.Equal("Reserve", state.ResolveActivePlan(ViewModel("Reserve", "Woods")).LocationId);
    }

    [Fact]
    public void ProgressionTargetCanBeSelectedAndCleared()
    {
        PlannerRaidPlanUiState state = new();

        state.SelectProgressionTarget("quest-1");
        Assert.True(state.HasProgressionTarget);
        Assert.Equal("quest-1", state.ProgressionTargetQuestId);

        state.ClearProgressionTarget();
        Assert.False(state.HasProgressionTarget);
        Assert.Null(state.ProgressionTargetQuestId);
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
            0d,
            System.Array.Empty<PlannerRaidObjective>(),
            System.Array.Empty<PlannerRaidBringNeed>())).ToArray();
        return new PlannerRaidPlanViewModel(1, PlannerRaidPlanRankingMode.ReadyFirst, cards);
    }
}
