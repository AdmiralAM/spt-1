using System.Collections;
using System.Globalization;
using System.Reflection;

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
        List<QuestNode> nodes = new();
        List<PrerequisiteEdge> prerequisites = new();
        List<ItemRequirement> items = new();
        List<string> warnings = new();

        foreach (object quest in EnumerateQuestObjects(rawQuests))
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
            HashSet<string> unsupportedStartConditionTypes = new(StringComparer.OrdinalIgnoreCase);

            object? conditions = GetMemberValue(quest, "conditions");
            if (conditions is not null)
            {
                object? startConditions = GetMemberValue(conditions, "AvailableForStart");
                foreach (object condition in EnumerateValues(startConditions))
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
                        foreach (object status in EnumerateValues(GetMemberValue(condition, "status")))
                        {
                            if (TryConvertInt(status, out int rawStatus))
                                acceptedStates.Add(MapQuestStatus(rawStatus));
                        }

                        int availableAfterSeconds = 0;
                        double? availableAfter = GetNumber(condition, "availableAfter");
                        if (availableAfter is not null && availableAfter > 0)
                            availableAfterSeconds = (int)Math.Min(int.MaxValue, Math.Ceiling(availableAfter.Value));

                        prerequisites.Add(new PrerequisiteEdge(
                            sourceQuestId,
                            questId,
                            acceptedStates,
                            GetString(condition, "id"),
                            availableAfterSeconds));
                    }
                    else
                    {
                        unsupportedStartConditionTypes.Add(string.IsNullOrWhiteSpace(type) ? "<missing>" : type);
                    }
                }

                ExtractItemRequirements(questId, conditions, "AvailableForStart", "Start", items, warnings);
                ExtractItemRequirements(questId, conditions, "AvailableForFinish", "Finish", items, warnings);
            }

            if (unsupportedStartConditionTypes.Count > 0)
            {
                warnings.Add(
                    $"Quest {questId}: AvailableForStart condition type(s) not modeled for hypothetical reachability: " +
                    string.Join(", ", unsupportedStartConditionTypes.Order(StringComparer.OrdinalIgnoreCase)));
            }

            nodes.Add(new QuestNode(
                questId,
                traderId,
                name,
                minimumLevel,
                Repeatable: false,
                StartConditionCoverageComplete: unsupportedStartConditionTypes.Count == 0));
        }

        return new QuestExtractionResult(nodes, prerequisites, items, warnings);
    }

    private static void ExtractItemRequirements(
        string questId,
        object conditions,
        string propertyName,
        string phase,
        List<ItemRequirement> items,
        List<string> warnings)
    {
        foreach (object condition in EnumerateValues(GetMemberValue(conditions, propertyName)))
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

    private static IEnumerable<object> EnumerateQuestObjects(object? rawQuests)
    {
        if (rawQuests is null) yield break;

        if (rawQuests is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
                if (entry.Value is not null) yield return entry.Value;
            yield break;
        }

        if (rawQuests is IEnumerable enumerable && rawQuests is not string)
        {
            foreach (object? entry in enumerable)
            {
                if (entry is null) continue;
                PropertyInfo? valueProperty = entry.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
                object? candidate = valueProperty is not null ? valueProperty.GetValue(entry) : entry;
                if (candidate is not null) yield return candidate;
            }
            yield break;
        }

        if (LooksLikeQuest(rawQuests))
        {
            yield return rawQuests;
            yield break;
        }

        Type containerType = rawQuests.GetType();
        foreach (PropertyInfo property in containerType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length != 0) continue;
            object? candidate;
            try { candidate = property.GetValue(rawQuests); }
            catch { continue; }
            if (candidate is not null && LooksLikeQuest(candidate)) yield return candidate;
        }

        foreach (FieldInfo field in containerType.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            object? candidate;
            try { candidate = field.GetValue(rawQuests); }
            catch { continue; }
            if (candidate is not null && LooksLikeQuest(candidate)) yield return candidate;
        }
    }

    private static bool LooksLikeQuest(object candidate) =>
        GetMemberValue(candidate, "_id") is not null || GetMemberValue(candidate, "id") is not null;

    private static IEnumerable<object> EnumerateValues(object? value)
    {
        if (value is null) yield break;
        if (value is string)
        {
            yield return value;
            yield break;
        }
        if (value is IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
                if (item is not null) yield return item;
            yield break;
        }
        yield return value;
    }

    private static object? GetMemberValue(object? instance, string name)
    {
        if (instance is null) return null;

        if (instance is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key?.ToString()?.Equals(name, StringComparison.OrdinalIgnoreCase) == true)
                    return entry.Value;
            }
        }

        Type type = instance.GetType();
        PropertyInfo? property = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && p.GetIndexParameters().Length == 0);
        if (property is not null)
        {
            try { return property.GetValue(instance); }
            catch { }
        }

        FieldInfo? field = type.GetFields(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (field is not null)
        {
            try { return field.GetValue(instance); }
            catch { }
        }

        return null;
    }

    private static string? GetString(object instance, string name)
    {
        object? value = GetMemberValue(instance, name);
        if (value is null) return null;
        if (value is string text) return string.IsNullOrWhiteSpace(text) ? null : text;
        string? converted = Convert.ToString(value, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(converted) ? null : converted;
    }

    private static IReadOnlyList<string> GetStringList(object instance, string name)
    {
        object? value = GetMemberValue(instance, name);
        if (value is null) return Array.Empty<string>();
        if (value is string one) return string.IsNullOrWhiteSpace(one) ? Array.Empty<string>() : new[] { one };

        List<string> result = new();
        foreach (object item in EnumerateValues(value))
        {
            string? converted = Convert.ToString(item, CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(converted)) result.Add(converted);
        }
        return result;
    }

    private static double? GetNumber(object instance, string name)
    {
        object? value = GetMemberValue(instance, name);
        if (value is null) return null;
        try { return Convert.ToDouble(value, CultureInfo.InvariantCulture); }
        catch
        {
            return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                ? parsed
                : null;
        }
    }

    private static bool? GetBool(object instance, string name)
    {
        object? value = GetMemberValue(instance, name);
        if (value is null) return null;
        if (value is bool boolean) return boolean;
        return bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out bool parsed) ? parsed : null;
    }

    private static bool TryConvertInt(object value, out int result)
    {
        try
        {
            result = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }
    }
}
