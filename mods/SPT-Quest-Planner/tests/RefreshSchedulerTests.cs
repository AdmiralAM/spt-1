using System;
using System.Collections.Concurrent;
using System.Threading;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class RefreshSchedulerTests
{
    [Fact]
    public void RapidRequestsAreCoalescedIntoOneRefresh()
    {
        using ManualResetEventSlim fired = new(false);
        ConcurrentQueue<string> reasons = new();
        using PlannerRefreshScheduler scheduler = new(
            reason =>
            {
                reasons.Enqueue(reason);
                fired.Set();
            },
            TimeSpan.FromMilliseconds(50));

        scheduler.Request("inventory-1");
        scheduler.Request("inventory-2");
        scheduler.Request("quest-accepted");

        Assert.True(fired.Wait(TimeSpan.FromSeconds(2)));
        Thread.Sleep(100);
        Assert.Single(reasons);
        Assert.True(reasons.TryPeek(out string? reason));
        Assert.Equal("quest-accepted", reason);
    }
}
