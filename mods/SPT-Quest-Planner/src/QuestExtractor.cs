using System.Globalization;
using System.Text.Json;

namespace SPTQuestPlanner;

public sealed record QuestExtractionResult(
    IReadOnlyList<QuestNode> Nodes,
    IReadOnlyList<PrerequisiteEdge> Prerequisites,
    IReadOnlyList<ItemRequirement> ItemRequirements,
    IReadOnlyList<string> Warnings);

public static class QuestExtractor
{
    public static QuestExtractionResult Extract(object rawQuests)
    {
        JsonElement root = JsonSerializer.SerializeToElement(rawQuests);
        List<QuestNode> nodes = new();
        List<PrerequisiteEdge> prerequisites = new();
        List<ItemRequirement> items = new();
        List<string> warnings = new();

        foreach (JsonElement quest in EnumerateQuestObjects(root))
        {
            string? questId = GetString(quest, "_id") ?? GetString(quest, "id");
            if (string.IsNullOrWhiteSpace(questId))
            {
                warnings.Add("Quest entry without _id/id skipped");
                continue;
            }

            string? traderId = GetString(quest, "traderId");
            string? name = GetString(quest, "QuestName") ?? GetString(quest, "name");
            int? minimumLevel = null;

            if (TryGetPropertyInsensitive(quest, "conditions", out JsonElement conditions))
            {
                if (TryGetPropertyInsensitive(conditions, "AvailableForStart", out JsonElement startConditions))
                {
                    foreach (JsonElement condition in EnumerateArray(startConditions))
                    {
                        string type = GetString(condition, "conditionType") ?? string.Empty;
                        if (type.Equals("Level", StringComparison.OrdinalIgnoreCase))
                        {
                            double? value = GetNumber(condition, "value");
                            if (value is not null && value >= 0)
                                minimumLevel = Math.Max(minimumLevel ?? 0, (int)Math.Ceiling(value.Value));
                        }
                        else if (type.Equals("Quest", StringComparison.OrdinalIgnoreCase))
                        {
                            string? sourceQuestId = GetString(condition, "target");
                            if (string.IsNullOrWhiteSpace(sourceQuestId))
                            {
                                warnings.Add($"Quest {questId}: prerequisite condition without target skipped");
                                continue;
                            }

                            HashSet<QuestState> acceptedStates = new();
                            if (TryGetPropertyInsensitive(condition, "status", out JsonElement statusElement))
                            {
                                foreach (JsonElement status in EnumerateArray(statusElement))
                                {
                                    if (status.TryGetInt32(out int rawStatus))
                                        acceptedStates.Add(MapQuestStatus(rawStatus));
                                }
                            }

                            prerequisites.Add(new PrerequisiteEdge(
                                sourceQuestId,
                                questId,
                                acceptedStates,
                                GetString(condition, "id")));
                        }
                    }
                }

                ExtractItemRequirements(questId, conditions, "AvailableForStart", "Start", items, warnings);
                ExtractItemRequirements(questId, conditions, "AvailableForFinish", "Finish", items, warnings);
            }

            nodes.Add(new QuestNode(
                questId,
                traderId,
                name,
                minimumLevel,
                Repeatable: false));
        }

        return new QuestExtractionResult(nodes, prerequisites, items, warnings);
    }

    private static void ExtractItemRequirements(
        string questId,
        JsonElement conditions,
        string propertyName,
        string phase,
        List<ItemRequirement> items,
        List<string> warnings)
    {
        if (!TryGetPropertyInsensitive(conditions, propertyName, out JsonElement conditionArray)) return;

        foreach (JsonElement condition in EnumerateArray(conditionArray))
        {
            string type = GetString(condition, "conditionType") ?? string.Empty;
            if (!type.Equals("HandoverItem", StringComparison.OrdinalIgnoreCase) &&
                !type.Equals("FindItem", StringComparison.OrdinalIgnoreCase))
                continue;

            string conditionId = GetString(condition, "id") ?? string.Empty;
            IReadOnlyList<string> targets = GetStringList(condition, "target");
            if (targets.Count == 0)
            {
                warnings.Add($"Quest {questId}: {type} condition {conditionId} has no target");
                continue;
            }

            double requiredCount = GetNumber(condition, "value") ?? 1d;
            bool foundInRaid = GetBool(condition, "onlyFoundInRaid") ?? false;
            items.Add(new ItemRequirement(
                questId,
                conditionId,
                targets,
                requiredCount,
                foundInRaid,
                phase));
        }
    }

    public static QuestState MapQuestStatus(int status) => status switch
    {
        0 => QuestState.Locked,
        1 => QuestState.Available,
        2 => QuestState.Started,
        3 => QuestState.Started,
        4 => QuestState.Success,
        5 => QuestState.Failed,
        6 => QuestState.Failed,
        7 => QuestState.Failed,
        8 => QuestState.Failed,
        9 => QuestState.Available,
        _ => QuestState.Unknown
    };

    private static IEnumerable<JsonElement> EnumerateQuestObjects(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement element in root.EnumerateArray())
                if (element.ValueKind == JsonValueKind.Object) yield return element;
            yield break;
        }

        if (root.ValueKind != JsonValueKind.Object) yield break;

        foreach (JsonProperty property in root.EnumerateObject())
            if (property.Value.ValueKind == JsonValueKind.Object) yield return property.Value;
    }

    private static IEnumerable<JsonElement> EnumerateArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array) yield break;
        foreach (JsonElement child in element.EnumerateArray()) yield return child;
    }

    private static string? GetString(JsonElement element, string name)
    {
        if (!TryGetPropertyInsensitive(element, name, out JsonElement value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static IReadOnlyList<string> GetStringList(JsonElement element, string name)
    {
        if (!TryGetPropertyInsensitive(element, name, out JsonElement value)) return Array.Empty<string>();
        if (value.ValueKind == JsonValueKind.String)
            return value.GetString() is { Length: > 0 } one ? new[] { one } : Array.Empty<string>();
        if (value.ValueKind != JsonValueKind.Array) return Array.Empty<string>();

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToArray();
    }

    private static double? GetNumber(JsonElement element, string name)
    {
        if (!TryGetPropertyInsensitive(element, name, out JsonElement value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number)) return number;
        if (value.ValueKind == JsonValueKind.String &&
            double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number)) return number;
        return null;
    }

    private static bool? GetBool(JsonElement element, string name)
    {
        if (!TryGetPropertyInsensitive(element, name, out JsonElement value)) return null;
        if (value.ValueKind == JsonValueKind.True) return true;
        if (value.ValueKind == JsonValueKind.False) return false;
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
