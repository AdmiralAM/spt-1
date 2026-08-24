using System.Text.Json;

namespace SPTQuestPlanner;

public sealed record PlayerQuestState(
    string QuestId,
    QuestState State,
    int RawStatus,
    long? StartTime,
    long? StatusTimer);

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
        JsonElement root = JsonSerializer.SerializeToElement(profile);
        List<string> warnings = new();
        Dictionary<string, PlayerQuestState> states = new(StringComparer.Ordinal);
        Dictionary<string, PlayerTaskConditionCounter> counters = ExtractTaskConditionCounters(root, warnings);

        int? level = null;
        if (TryGetPropertyInsensitive(root, "Info", out JsonElement info))
            level = GetInt(info, "Level");

        if (!TryGetPropertyInsensitive(root, "Quests", out JsonElement quests) || quests.ValueKind != JsonValueKind.Array)
        {
            warnings.Add("PMC profile has no Quests array");
            return new PlayerProjection(level, states, counters, warnings);
        }

        foreach (JsonElement quest in quests.EnumerateArray())
        {
            if (quest.ValueKind != JsonValueKind.Object) continue;

            string? questId = GetString(quest, "qid") ?? GetString(quest, "Qid") ?? GetString(quest, "questId");
            if (string.IsNullOrWhiteSpace(questId))
            {
                warnings.Add("PMC quest-state entry without qid skipped");
                continue;
            }

            int rawStatus = GetInt(quest, "status") ?? -1;
            QuestState state = rawStatus >= 0 ? QuestExtractor.MapQuestStatus(rawStatus) : QuestState.Unknown;
            if (rawStatus < 0)
                warnings.Add($"PMC quest {questId}: missing status");

            states[questId] = new PlayerQuestState(
                questId,
                state,
                rawStatus,
                GetLong(quest, "startTime"),
                GetLong(quest, "statusTimer"));
        }

        return new PlayerProjection(level, states, counters, warnings);
    }

    private static Dictionary<string, PlayerTaskConditionCounter> ExtractTaskConditionCounters(JsonElement root, List<string> warnings)
    {
        Dictionary<string, PlayerTaskConditionCounter> counters = new(StringComparer.Ordinal);
        if (!TryGetPropertyInsensitive(root, "TaskConditionCounters", out JsonElement raw)) return counters;
        if (raw.ValueKind != JsonValueKind.Object)
        {
            warnings.Add("PMC TaskConditionCounters is not an object");
            return counters;
        }

        foreach (JsonProperty property in raw.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object) continue;
            string counterId = GetString(property.Value, "id") ?? property.Name;
            if (string.IsNullOrWhiteSpace(counterId)) continue;
            counters[counterId] = new PlayerTaskConditionCounter(
                counterId,
                GetString(property.Value, "type"),
                GetDouble(property.Value, "value") ?? 0d,
                GetString(property.Value, "sourceId"));
        }

        return counters;
    }

    public static IReadOnlyDictionary<QuestState, int> CountStates(
        IEnumerable<QuestNode> nodes,
        PlayerProjection projection)
    {
        Dictionary<QuestState, int> counts = new();
        foreach (QuestNode node in nodes)
        {
            QuestState state = projection.GetState(node.QuestId);
            counts[state] = counts.TryGetValue(state, out int count) ? count + 1 : 1;
        }
        return counts;
    }

    private static string? GetString(JsonElement element, string name)
    {
        if (!TryGetPropertyInsensitive(element, name, out JsonElement value)) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static int? GetInt(JsonElement element, string name)
    {
        if (!TryGetPropertyInsensitive(element, name, out JsonElement value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number)) return number;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number)) return number;
        return null;
    }

    private static long? GetLong(JsonElement element, string name)
    {
        if (!TryGetPropertyInsensitive(element, name, out JsonElement value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number)) return number;
        if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out number)) return number;
        return null;
    }

    private static double? GetDouble(JsonElement element, string name)
    {
        if (!TryGetPropertyInsensitive(element, name, out JsonElement value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number)) return number;
        if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out number)) return number;
        return null;
    }

    private static bool TryGetPropertyInsensitive(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
