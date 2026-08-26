namespace SPTQuestPlanner;

public enum PlannerQuestDelayTimingState
{
    NotDelayed = 0,
    PendingKnown = 1,
    ElapsedPendingRefresh = 2,
    TimingUnresolved = 3
}

public sealed record PlannerQuestDelayTiming(
    PlannerQuestDelayTimingState State,
    long? AvailableAtUnixSeconds,
    long? RemainingSeconds)
{
    public bool HasKnownRemainingTime => State == PlannerQuestDelayTimingState.PendingKnown && RemainingSeconds.HasValue;
}

public static class PlannerQuestDelayTimingBuilder
{
    // SPT QuestStatusEnum.AvailableAfter.
    private const int RawAvailableAfterStatus = 9;

    public static PlannerQuestDelayTiming Build(PlayerQuestState quest, long generatedAtUnixSeconds)
    {
        ArgumentNullException.ThrowIfNull(quest);

        if (quest.RawStatus != RawAvailableAfterStatus)
            return new PlannerQuestDelayTiming(PlannerQuestDelayTimingState.NotDelayed, quest.AvailableAfterUnixSeconds, null);

        if (!quest.AvailableAfterUnixSeconds.HasValue || quest.AvailableAfterUnixSeconds.Value <= 0)
            return new PlannerQuestDelayTiming(PlannerQuestDelayTimingState.TimingUnresolved, null, null);

        long availableAt = quest.AvailableAfterUnixSeconds.Value;
        if (generatedAtUnixSeconds <= 0)
            return new PlannerQuestDelayTiming(PlannerQuestDelayTimingState.TimingUnresolved, availableAt, null);

        long remaining = availableAt - generatedAtUnixSeconds;
        if (remaining > 0)
            return new PlannerQuestDelayTiming(PlannerQuestDelayTimingState.PendingKnown, availableAt, remaining);

        // The configured delay has elapsed according to the snapshot clock, but the profile still
        // reports raw AvailableAfter. Do not silently promote the quest to AvailableForStart;
        // the authoritative profile/server refresh owns that state transition.
        return new PlannerQuestDelayTiming(PlannerQuestDelayTimingState.ElapsedPendingRefresh, availableAt, 0);
    }
}
