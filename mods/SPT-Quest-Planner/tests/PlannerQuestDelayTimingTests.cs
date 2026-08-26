using SPTQuestPlanner;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerQuestDelayTimingTests
{
    [Fact]
    public void ProfileProjection_PreservesAvailableAfterAndStatusTimersSeparately()
    {
        Dictionary<string, object> quest = new()
        {
            ["qid"] = "delayed",
            ["status"] = 9,
            ["startTime"] = 1000L,
            ["statusTimer"] = 1111L,
            ["availableAfter"] = 2000L,
            ["statusTimers"] = new Dictionary<string, object>
            {
                ["Success"] = 1500L,
                ["AvailableAfter"] = 1501L
            }
        };
        Dictionary<string, object> profile = new()
        {
            ["Info"] = new Dictionary<string, object> { ["Level"] = 25 },
            ["Quests"] = new object[] { quest }
        };

        PlayerProjection projection = ProfileProjectionExtractor.Extract(profile);
        PlayerQuestState state = projection.QuestStates["delayed"];

        Assert.Equal(9, state.RawStatus);
        Assert.Equal(1111L, state.StatusTimer);
        Assert.Equal(2000L, state.AvailableAfterUnixSeconds);
        Assert.Equal(1500L, state.StatusTimers[4]);
        Assert.Equal(1501L, state.StatusTimers[9]);
    }

    [Fact]
    public void DelayTiming_ReportsKnownRemainingTimeOnlyFromAbsoluteProfileTimestamp()
    {
        PlayerQuestState quest = new("delayed", QuestState.Locked, 9, null, null)
        {
            AvailableAfterUnixSeconds = 2000L
        };

        PlannerQuestDelayTiming timing = PlannerQuestDelayTimingBuilder.Build(quest, 1700L);

        Assert.Equal(PlannerQuestDelayTimingState.PendingKnown, timing.State);
        Assert.Equal(300L, timing.RemainingSeconds);
        Assert.True(timing.HasKnownRemainingTime);
    }

    [Fact]
    public void DelayTiming_DoesNotPromoteElapsedProfileStateLocally()
    {
        PlayerQuestState quest = new("delayed", QuestState.Locked, 9, null, null)
        {
            AvailableAfterUnixSeconds = 2000L
        };

        PlannerQuestDelayTiming timing = PlannerQuestDelayTimingBuilder.Build(quest, 2005L);

        Assert.Equal(PlannerQuestDelayTimingState.ElapsedPendingRefresh, timing.State);
        Assert.Equal(0L, timing.RemainingSeconds);
        Assert.False(timing.HasKnownRemainingTime);
    }

    [Fact]
    public void DelayTiming_AbstainsWhenProfileHasDelayedStatusWithoutTimestamp()
    {
        PlayerQuestState quest = new("delayed", QuestState.Locked, 9, null, null);

        PlannerQuestDelayTiming timing = PlannerQuestDelayTimingBuilder.Build(quest, 1700L);

        Assert.Equal(PlannerQuestDelayTimingState.TimingUnresolved, timing.State);
        Assert.Null(timing.RemainingSeconds);
    }

    [Fact]
    public void DelayTiming_IgnoresStaleAvailableAfterOnNonDelayedQuestState()
    {
        PlayerQuestState quest = new("ready", QuestState.Available, 1, null, null)
        {
            AvailableAfterUnixSeconds = 2000L
        };

        PlannerQuestDelayTiming timing = PlannerQuestDelayTimingBuilder.Build(quest, 1700L);

        Assert.Equal(PlannerQuestDelayTimingState.NotDelayed, timing.State);
        Assert.Null(timing.RemainingSeconds);
    }
}
