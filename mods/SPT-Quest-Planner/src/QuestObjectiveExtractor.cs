using System.Collections;
using System.Globalization;
using System.Reflection;

namespace SPTQuestPlanner;

public sealed record QuestObjectiveFact(
    string QuestId,
    string ConditionId,
    string ConditionType,
    string Phase,
    string? ParentConditionId,
    IReadOnlyList<string> Targets,
    IReadOnlyList<string> LocationHints,
    string? QuestLocationHint,
    double? RequiredValue);

public sealed record QuestObjectiveExtractionResult(
    IReadOnlyList<QuestObjectiveFact> Objectives,
    IReadOnlyList<string> Warnings);

public static class QuestObjectiveExtractor
{
    private const int MaxConditionDepth = 12;
    private const int MaxWrapperDepth = 8;
    private static readonly string[] WrapperMemberNames = { "Item", "List", "Value", "Values", "Items", "Data" };

    public static QuestObjectiveExtractionResult Extract(object rawQuests)
    {
        List<QuestObjectiveFact> objectives = new();
        List<string> warnings = new();

        foreach (object quest in EnumerateQuestObjects(rawQuests))
        {
            string? questId = GetString(quest, "_id") ?? GetString(quest, "id");
            if (string.IsNullOrWhiteSpace(questId)) continue;

            string? questLocation = NormalizeLocationHint(GetString(quest, "location"));
            object? conditions = GetMemberValue(quest, "conditions");
            if (conditions is null) continue;

            ExtractPhase(questId, questLocation, conditions, "AvailableForStart", "Start", objectives, warnings);
            ExtractPhase(questId, questLocation, conditions, "AvailableForFinish", "Finish", objectives, warnings);
        }

        return new QuestObjectiveExtractionResult(objectives, warnings.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static void ExtractPhase(
        string questId,
        string? questLocation,
        object conditions,
        string propertyName,
        string phase,
        List<QuestObjectiveFact> output,
        List<string> warnings)
    {
        foreach (object condition in EnumerateValues(GetMemberValue(conditions, propertyName)))
            ExtractCondition(questId, questLocation, condition, phase, null, null, 0, output, warnings);
    }

    private static void ExtractCondition(
        string questId,
        string? questLocation,
        object condition,
        string phase,
        string? parentConditionId,
        double? parentRequiredValue,
        int depth,
        List<QuestObjectiveFact> output,
        List<string> warnings)
    {
        if (depth > MaxConditionDepth)
        {
            warnings.Add($"Quest {questId}: condition nesting exceeded {MaxConditionDepth}; deeper objective data skipped");
            return;
        }

        string conditionId = GetString(condition, "id") ?? string.Empty;
        string conditionType = GetString(condition, "conditionType") ?? GetString(condition, "type") ?? string.Empty;
        IReadOnlyList<string> targets = GetStringList(condition, "target");
        IReadOnlyList<string> locationHints = ExtractLocationHints(condition, conditionType, targets);
        double? ownRequiredValue = GetNumber(condition, "value");
        double? effectiveRequiredValue = ownRequiredValue ?? parentRequiredValue;

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
                questLocation,
                effectiveRequiredValue));
        }

        object? counter = GetMemberValue(condition, "counter");
        if (counter is not null)
        {
            foreach (object child in EnumerateValues(GetMemberValue(counter, "conditions")))
                ExtractCondition(questId, questLocation, child, phase, conditionId, effectiveRequiredValue, depth + 1, output, warnings);
        }

        foreach (object child in EnumerateValues(GetMemberValue(condition, "conditions")))
            ExtractCondition(questId, questLocation, child, phase, conditionId, effectiveRequiredValue, depth + 1, output, warnings);
    }

    private static IReadOnlyList<string> ExtractLocationHints(
        object condition,
        string conditionType,
        IReadOnlyList<string> targets)
    {
        HashSet<string> hints = new(StringComparer.OrdinalIgnoreCase);
        AddLocationProperty(condition, "location", hints);
        AddLocationProperty(condition, "locationId", hints);
        AddLocationProperty(condition, "locationIds", hints);
        AddLocationProperty(condition, "locations", hints);

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

    private static void AddLocationProperty(object condition, string propertyName, HashSet<string> output)
    {
        object? value = GetMemberValue(condition, propertyName);
        if (value is null) return;

        foreach (object item in EnumerateValues(value))
        {
            string? normalized = NormalizeLocationHint(ScalarString(item));
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
        if (LooksLikeTypeName(normalized) || normalized.Length > 128) return null;
        return normalized;
    }

    private static bool LooksLikeTypeName(string value)
    {
        return value.IndexOf("SPTarkov.", StringComparison.OrdinalIgnoreCase) >= 0 ||
               value.IndexOf("System.", StringComparison.OrdinalIgnoreCase) >= 0 ||
               value.IndexOf("ListOrT", StringComparison.OrdinalIgnoreCase) >= 0 ||
               value.IndexOf('`') >= 0 || value.IndexOf('[') >= 0 || value.IndexOf(']') >= 0;
    }

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

    private static IEnumerable<object> EnumerateValues(object? value) => EnumerateValues(value, 0);

    private static IEnumerable<object> EnumerateValues(object? value, int depth)
    {
        if (value is null || depth > MaxWrapperDepth) yield break;
        if (value is string)
        {
            yield return value;
            yield break;
        }
        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
                if (entry.Value is not null)
                    foreach (object nested in EnumerateValues(entry.Value, depth + 1)) yield return nested;
            yield break;
        }
        if (value is IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
                if (item is not null)
                    foreach (object nested in EnumerateValues(item, depth + 1)) yield return nested;
            yield break;
        }

        Type type = value.GetType();
        bool wrapperLike = type.Name.IndexOf("ListOrT", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           type.Namespace?.IndexOf("Utils.Json", StringComparison.OrdinalIgnoreCase) >= 0;
        if (wrapperLike)
        {
            for (int i = 0; i < WrapperMemberNames.Length; i++)
            {
                object? nested = GetMemberValue(value, WrapperMemberNames[i]);
                if (nested is null || ReferenceEquals(nested, value)) continue;
                bool produced = false;
                foreach (object item in EnumerateValues(nested, depth + 1))
                {
                    produced = true;
                    yield return item;
                }
                if (produced) yield break;
            }
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

    private static IReadOnlyList<string> GetStringList(object instance, string name)
    {
        object? value = GetMemberValue(instance, name);
        if (value is null) return Array.Empty<string>();

        List<string> result = new();
        foreach (object item in EnumerateValues(value))
        {
            string? converted = ScalarString(item);
            if (!string.IsNullOrWhiteSpace(converted) && !LooksLikeTypeName(converted)) result.Add(converted);
        }
        return result.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string? GetString(object instance, string name)
    {
        object? value = GetMemberValue(instance, name);
        if (value is null) return null;
        foreach (object item in EnumerateValues(value))
        {
            string? converted = ScalarString(item);
            if (!string.IsNullOrWhiteSpace(converted) && !LooksLikeTypeName(converted)) return converted;
        }
        return null;
    }

    private static string? ScalarString(object? value)
    {
        if (value is null) return null;
        if (value is string text) return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        Type type = value.GetType();
        bool scalar = type.IsPrimitive || type.IsEnum || type.IsValueType ||
                      type.Name.IndexOf("MongoId", StringComparison.OrdinalIgnoreCase) >= 0;
        if (!scalar) return null;
        string? converted = Convert.ToString(value, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(converted) ? null : converted.Trim();
    }

    private static double? GetNumber(object instance, string name)
    {
        object? value = GetMemberValue(instance, name);
        if (value is null) return null;
        foreach (object item in EnumerateValues(value))
        {
            try { return Convert.ToDouble(item, CultureInfo.InvariantCulture); }
            catch
            {
                if (double.TryParse(ScalarString(item), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                    return parsed;
            }
        }
        return null;
    }
}
