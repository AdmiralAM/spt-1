using System.Text.Json;

namespace SPTQuestPlanner;

public sealed record QuestObjectiveFact(
    string QuestId,
    string ConditionId,
    string ConditionType,
    string Phase,
    string? ParentConditionId,
    IReadOnlyList<string> Targets,
    IReadOnlyList<string> LocationHints,
    string? QuestLocationHint);

public sealed record QuestObjectiveExtractionResult(
    IReadOnlyList<QuestObjectiveFact> Objectives,
    IReadOnlyList<string> Warnings);

public static class QuestObjectiveExtractor
{
    private const int MaxConditionDepth = 12;

    public static QuestObjectiveExtractionResult Extract(object rawQuests)
    {
        JsonElement root = JsonSerializer.SerializeToElement(rawQuests);
        List<QuestObjectiveFact> objectives = new();
        List<string> warnings = new();

        foreach (JsonElement quest in EnumerateQuestObjects(root))
        {
            string? questId = GetString(quest, "_id") ?? GetString(quest, "id");
            if (string.IsNullOrWhiteSpace(questId)) continue;

            string? questLocation = NormalizeLocationHint(GetString(quest, "location"));
            if (!TryGetPropertyInsensitive(quest, "conditions", out JsonElement conditions)) continue;

            ExtractPhase(questId, questLocation, conditions, "AvailableForStart", "Start", objectives, warnings);
            ExtractPhase(questId, questLocation, conditions, "AvailableForFinish", "Finish", objectives, warnings);
        }

        return new QuestObjectiveExtractionResult(objectives, warnings.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static void ExtractPhase(
        string questId,
        string? questLocation,
        JsonElement conditions,
        string propertyName,
        string phase,
        List<QuestObjectiveFact> output,
        List<string> warnings)
    {
        if (!TryGetPropertyInsensitive(conditions, propertyName, out JsonElement roots) || roots.ValueKind != JsonValueKind.Array)
            return;

        foreach (JsonElement condition in roots.EnumerateArray())
            ExtractCondition(questId, questLocation, condition, phase, null, 0, output, warnings);
    }

    private static void ExtractCondition(
        string questId,
        string? questLocation,
        JsonElement condition,
        string phase,
        string? parentConditionId,
        int depth,
        List<QuestObjectiveFact> output,
        List<string> warnings)
    {
        if (condition.ValueKind != JsonValueKind.Object) return;
        if (depth > MaxConditionDepth)
        {
            warnings.Add($"Quest {questId}: condition nesting exceeded {MaxConditionDepth}; deeper objective data skipped");
            return;
        }

        string conditionId = GetString(condition, "id") ?? string.Empty;
        string conditionType = GetString(condition, "conditionType") ?? GetString(condition, "type") ?? string.Empty;
        IReadOnlyList<string> targets = GetStringList(condition, "target");
        IReadOnlyList<string> locationHints = ExtractLocationHints(condition, conditionType, targets);

        // Level and Quest prerequisite conditions are graph metadata, not raid objectives.
        if (!conditionType.Equals("Level", StringComparison.OrdinalIgnoreCase) &&
            !conditionType.Equals("Quest", StringComparison.OrdinalIgnoreCase))
        {
            output.Add(new QuestObjectiveFact(
                questId,
                conditionId,
                conditionType,
                phase,
                parentConditionId,
                targets,
                locationHints,
                questLocation));
        }

        // CounterCreator and modded conditions commonly nest objective conditions inside counter.conditions.
        if (TryGetPropertyInsensitive(condition, "counter", out JsonElement counter) && counter.ValueKind == JsonValueKind.Object &&
            TryGetPropertyInsensitive(counter, "conditions", out JsonElement nested) && nested.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in nested.EnumerateArray())
                ExtractCondition(questId, questLocation, child, phase, conditionId, depth + 1, output, warnings);
        }

        // Be tolerant of custom quest mods that place nested conditions directly under `conditions`.
        if (TryGetPropertyInsensitive(condition, "conditions", out JsonElement directNested) && directNested.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in directNested.EnumerateArray())
                ExtractCondition(questId, questLocation, child, phase, conditionId, depth + 1, output, warnings);
        }
    }

    private static IReadOnlyList<string> ExtractLocationHints(
        JsonElement condition,
        string conditionType,
        IReadOnlyList<string> targets)
    {
        HashSet<string> hints = new(StringComparer.OrdinalIgnoreCase);
        AddLocationProperty(condition, "location", hints);
        AddLocationProperty(condition, "locationId", hints);
        AddLocationProperty(condition, "locationIds", hints);
        AddLocationProperty(condition, "locations", hints);

        // In SPT/EFT quest data, an explicit Location condition may encode its location(s) in target.
        // Do not apply this rule to arbitrary condition types because target can also be zone/item/bot IDs.
        if (conditionType.Equals("Location", StringComparison.OrdinalIgnoreCase))
        {
            foreach (string target in targets)
            {
                string? normalized = NormalizeLocationHint(target);
                if (normalized is not null) hints.Add(normalized);
            }
        }

        return hints.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void AddLocationProperty(JsonElement condition, string propertyName, HashSet<string> output)
    {
        if (!TryGetPropertyInsensitive(condition, propertyName, out JsonElement value)) return;
        if (value.ValueKind == JsonValueKind.String)
        {
            string? normalized = NormalizeLocationHint(value.GetString());
            if (normalized is not null) output.Add(normalized);
            return;
        }

        if (value.ValueKind != JsonValueKind.Array) return;
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) continue;
            string? normalized = NormalizeLocationHint(item.GetString());
            if (normalized is not null) output.Add(normalized);
        }
    }

    private static string? NormalizeLocationHint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string normalized = value.Trim();
        if (normalized.Equals("any", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("anywhere", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("none", StringComparison.OrdinalIgnoreCase))
            return null;
        return normalized;
    }

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

    private static string? GetString(JsonElement element, string name)
    {
        if (!TryGetPropertyInsensitive(element, name, out JsonElement value)) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
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
