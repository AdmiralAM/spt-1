using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerRecommendationViewModelUxTests
{
    [Fact]
    public void ActiveOwnedQuestReadsAsReadyToPush()
    {
        PlannerRecommendationViewModel value = Recommendation(disposition: 4, fullyOwned: true);

        Assert.Equal("ACTIVE", value.StateLabel);
        Assert.Equal("Ready to push now", value.ActionSummary);
    }

    [Fact]
    public void AvailableOwnedQuestPromptsAcceptance()
    {
        PlannerRecommendationViewModel value = Recommendation(disposition: 3, fullyOwned: true);

        Assert.Equal("AVAILABLE", value.StateLabel);
        Assert.Equal("Accept quest; item burden ready", value.ActionSummary);
    }

    [Fact]
    public void BlockersTakePriorityOverItemBurdenInActionSummary()
    {
        PlannerRecommendationViewModel value = Recommendation(
            disposition: 4,
            fullyOwned: false,
            blockers: new[] { "q-blocker" },
            totalOutstanding: 3d,
            firOutstanding: 1d);

        Assert.Equal("Clear 1 blocker(s) first", value.ActionSummary);
    }

    [Fact]
    public void MissingItemsAreSummarizedWithFirBurden()
    {
        PlannerRecommendationViewModel value = Recommendation(
            disposition: 4,
            fullyOwned: false,
            totalOutstanding: 3d,
            firOutstanding: 1d);

        Assert.Equal("Need 3 item(s), FIR 1", value.ActionSummary);
    }

    private static PlannerRecommendationViewModel Recommendation(
        int disposition,
        bool fullyOwned,
        string[] blockers = null,
        double totalOutstanding = 0d,
        double firOutstanding = 0d)
    {
        blockers ??= System.Array.Empty<string>();
        return new PlannerRecommendationViewModel(
            1,
            "quest",
            "Quest",
            "trader",
            disposition,
            System.Array.Empty<string>(),
            blockers,
            blockers,
            System.Array.Empty<string>(),
            System.Array.Empty<string>(),
            1,
            totalOutstanding,
            firOutstanding,
            fullyOwned);
    }
}
