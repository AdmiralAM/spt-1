using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerSelectionSnapshotTests
{
    [Fact]
    public void EmptySelectionReportsNoActiveChoices()
    {
        PlannerSelectionSnapshot snapshot = new(42, null, null);

        Assert.Equal(42, snapshot.CacheRevision);
        Assert.False(snapshot.HasActiveRaidPlan);
        Assert.False(snapshot.HasProgressionTarget);
    }

    [Fact]
    public void SnapshotCarriesIndependentRaidAndProgressionChoices()
    {
        PlannerSelectionSnapshot snapshot = new(7, "Reserve", "quest-42");

        Assert.True(snapshot.HasActiveRaidPlan);
        Assert.True(snapshot.HasProgressionTarget);
        Assert.Equal("Reserve", snapshot.ActiveLocationId);
        Assert.Equal("quest-42", snapshot.ProgressionTargetQuestId);
    }
}
