namespace SPTQuestPlanner;

public sealed record PlayerQuestState(
    string QuestId,
    QuestState State,
    int RawStatus,
    long? StartTime,
    long? StatusTimer)
{
    // SPT stores delayed quest availability as an absolute Unix timestamp on the quest status.
    // Keep the legacy scalar StatusTimer field for compatibility, but do not use it as the
    // authoritative delayed-unlock timestamp.
    public long? AvailableAfterUnixSeconds { get; init; }

    // Exact per-status transition timestamps when exposed by the profile. Keys are raw
    // QuestStatus values; values are Unix timestamps. This is provenance/debug evidence,
    // not a replacement for AvailableAfterUnixSeconds.
    public IReadOnlyDictionary<int, long> StatusTimers { get; init; } =
        new Dictionary<int, long>();
}

public sealed record PlayerTaskConditionCounter(
    string CounterId,
    string? Type,
    double Value,
    string? SourceQuestId);

public sealed record PlayerProjection(
    int? Level,
    IReadOnlyDictionary<string, PlayerQuestState> QuestStates,
    IReadOnlyDictionary<string, PlayerTaskConditionCounter> TaskConditionCounters,
    IReadOnlyList<string> Warnings)
{
    public QuestState GetState(string questId) =>
        QuestStates.TryGetValue(questId, out var state) ? state.State : QuestState.Locked;
}

public static class ProfileProjectionExtractor
{
    public static PlayerProjection Extract(object profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        List<string> warnings = new();
        Dictionary<string, PlayerQuestState> states = new(StringComparer.Ordinal);
        Dictionary<string, PlayerTaskConditionCounter> counters = ExtractTaskConditionCounters(profile, warnings);

        int? level = SptObjectReader.Int(SptObjectReader.Get(SptObjectReader.Get(profile, "Info"), "Level"));
        object? quests = SptObjectReader.Get(profile, "Quests");
        bool sawQuest = false;
        foreach (object quest in SptObjectReader.Values(quests))
        {
            sawQuest = true;
            string? questId = SptObjectReader.String(SptObjectReader.Get(quest, "qid", "Qid", "questId"));
            if (string.IsNullOrWhiteSpace(questId))
            {
                warnings.Add("PMC quest-state entry without qid skipped");
                continue;
            }

            int rawStatus = SptObjectReader.Int(SptObjectReader.Get(quest, "status")) ?? -1;
            QuestState state = rawStatus >= 0 ? QuestExtractor.MapQuestStatus(rawStatus) : QuestState.Unknown;
            if (rawStatus < 0) warnings.Add($"PMC quest {questId}: missing status");

            long? legacyStatusTimer = SptObjectReader.Long(SptObjectReader.Get(quest, "statusTimer"));
            long? availableAfter = SptObjectReader.Long(SptObjectReader.Get(quest, "availableAfter", "AvailableAfter"));
            IReadOnlyDictionary<int, long> statusTimers = ExtractStatusTimers(
                SptObjectReader.Get(quest, "statusTimers", "StatusTimers"));

            states[questId] = new PlayerQuestState(
                questId,
                state,
                rawStatus,
                SptObjectReader.Long(SptObjectReader.Get(quest, "startTime")),
                legacyStatusTimer)
            {
                AvailableAfterUnixSeconds = availableAfter,
                StatusTimers = statusTimers
            };
        }

        if (!sawQuest && quests is null) warnings.Add("PMC profile has no Quests array");
        return new PlayerProjection(level, states, counters, warnings.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static IReadOnlyDictionary<int, long> ExtractStatusTimers(object? raw)
    {
        Dictionary<int, long> timers = new();
        if (raw is null) return timers;

        foreach (KeyValuePair<string, object> entry in SptObjectReader.Entries(raw))
        {
            int? rawStatus = ParseRawQuestStatusKey(entry.Key);
            if (!rawStatus.HasValue) continue;
            long? timestamp = SptObjectReader.Long(entry.Value);
            if (timestamp.HasValue) timers[rawStatus.Value] = timestamp.Value;
        }
        return timers;
    }

    private static int? ParseRawQuestStatusKey(string key)
    {
        if (int.TryParse(key, out int numeric) && numeric >= 0 && numeric <= 9) return numeric;
        return key?.Trim().ToLowerInvariant() switch
        {
            "locked" => 0,
            "availableforstart" => 1,
            "started" => 2,
            "availableforfinish" => 3,
            "success" => 4,
            "fail" => 5,
            "failrestartable" => 6,
            "markedasfailed" => 7,
            "expired" => 8,
            "availableafter" => 9,
            _ => null
        };
    }

    private static Dictionary<string, PlayerTaskConditionCounter> ExtractTaskConditionCounters(object profile, List<string> warnings)
    {
        Dictionary<string, PlayerTaskConditionCounter> counters = new(StringComparer.Ordinal);
        object? raw = SptObjectReader.Get(profile, "TaskConditionCounters");
        if (raw is null) return counters;

        bool sawEntry = false;
        foreach (KeyValuePair<string, object> entry in SptObjectReader.Entries(raw))
        {
            sawEntry = true;
            string counterId = SptObjectReader.String(SptObjectReader.Get(entry.Value, "id")) ?? entry.Key;
            if (string.IsNullOrWhiteSpace(counterId)) continue;
            counters[counterId] = new PlayerTaskConditionCounter(
                counterId,
                SptObjectReader.String(SptObjectReader.Get(entry.Value, "type")),
                SptObjectReader.Double(SptObjectReader.Get(entry.Value, "value")) ?? 0d,
                SptObjectReader.String(SptObjectReader.Get(entry.Value, "sourceId")));
        }

        if (!sawEntry)
        {
            foreach (object value in SptObjectReader.Values(raw))
            {
                string? counterId = SptObjectReader.String(SptObjectReader.Get(value, "id"));
                if (string.IsNullOrWhiteSpace(counterId)) continue;
                counters[counterId] = new PlayerTaskConditionCounter(
                    counterId,
                    SptObjectReader.String(SptObjectReader.Get(value, "type")),
                    SptObjectReader.Double(SptObjectReader.Get(value, "value")) ?? 0d,
                    SptObjectReader.String(SptObjectReader.Get(value, "sourceId")));
            }
        }
        return counters;
    }

    public static IReadOnlyDictionary<QuestState, int> CountStates(IEnumerable<QuestNode> nodes, PlayerProjection projection)
    {
        Dictionary<QuestState, int> counts = new();
        foreach (QuestNode node in nodes)
        {
            QuestState state = projection.GetState(node.QuestId);
            counts[state] = counts.TryGetValue(state, out int count) ? count + 1 : 1;
        }
        return counts;
    }
}
