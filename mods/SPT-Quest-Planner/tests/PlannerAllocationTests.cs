using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerAllocationTests
{
    [Fact]
    public void CurrentFirIsReservedBeforeGenericAndFuture()
    {
        AggregatedItemRequirement requirement = new(
            "tpl",
            CurrentFirRequired: 2,
            CurrentNonFirRequired: 2,
            FutureFirRequired: 2,
            FutureNonFirRequired: 2,
            CurrentQuestIds: new HashSet<string> { "q1" },
            FutureQuestIds: new HashSet<string> { "q2" });

        InventoryProjection inventory = new(
            new Dictionary<string, OwnedItemCount>
            {
                ["tpl"] = new("tpl", Total: 5, FoundInRaid: 3)
            },
            Array.Empty<string>());

        OutstandingItemRequirement result = Assert.Single(
            InventoryProjectionExtractor.CalculateOutstanding(new[] { requirement }, inventory));

        Assert.Equal(0, result.CurrentFirOutstanding);
        Assert.Equal(0, result.CurrentNonFirOutstanding);
        Assert.Equal(1, result.FutureFirOutstandingAfterCurrent);
        Assert.Equal(2, result.FutureNonFirOutstandingAfterCurrent);
        Assert.Equal(3, result.FutureOutstandingAfterCurrent);
    }

    [Fact]
    public void NonFirStockIsConsumedBeforeFirForGenericRequirement()
    {
        AggregatedItemRequirement requirement = new(
            "tpl",
            CurrentFirRequired: 1,
            CurrentNonFirRequired: 3,
            FutureFirRequired: 0,
            FutureNonFirRequired: 0,
            CurrentQuestIds: new HashSet<string> { "q" },
            FutureQuestIds: new HashSet<string>());

        InventoryProjection inventory = new(
            new Dictionary<string, OwnedItemCount>
            {
                ["tpl"] = new("tpl", Total: 4, FoundInRaid: 2)
            },
            Array.Empty<string>());

        OutstandingItemRequirement result = Assert.Single(
            InventoryProjectionExtractor.CalculateOutstanding(new[] { requirement }, inventory));

        Assert.Equal(0, result.CurrentFirOutstanding);
        Assert.Equal(0, result.CurrentNonFirOutstanding);
    }
}
