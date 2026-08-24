using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace SPTQuestPlanner;

internal static class SptObjectReader
{
    public static object? Get(object? source, params string[] names)
    {
        if (source is null) return null;
        if (source is JsonElement json)
        {
            if (json.ValueKind != JsonValueKind.Object) return null;
            foreach (JsonProperty property in json.EnumerateObject())
                foreach (string name in names)
                    if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return property.Value;
            return null;
        }

        if (source is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                string key = entry.Key?.ToString() ?? string.Empty;
                foreach (string name in names)
                    if (key.Equals(name, StringComparison.OrdinalIgnoreCase)) return entry.Value;
            }
        }

        Type type = source.GetType();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        foreach (string name in names)
        {
            PropertyInfo? property = type.GetProperties(flags).FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && p.GetIndexParameters().Length == 0);
            if (property != null)
            {
                try { return property.GetValue(source); } catch { }
            }
            FieldInfo? field = type.GetFields(flags).FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (field != null)
            {
                try { return field.GetValue(source); } catch { }
            }
        }
        return null;
    }

    public static IEnumerable<object> Values(object? source)
    {
        if (source is null) yield break;
        if (source is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Array)
                foreach (JsonElement value in json.EnumerateArray()) yield return value;
            else if (json.ValueKind == JsonValueKind.Object)
                foreach (JsonProperty property in json.EnumerateObject()) yield return property.Value;
            yield break;
        }
        if (source is string) yield break;
        if (source is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
                if (entry.Value != null) yield return entry.Value;
            yield break;
        }
        if (source is IEnumerable enumerable)
        {
            foreach (object? value in enumerable)
                if (value != null) yield return value;
        }
    }

    public static IEnumerable<KeyValuePair<string, object>> Entries(object? source)
    {
        if (source is null) yield break;
        if (source is JsonElement json && json.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in json.EnumerateObject()) yield return new(property.Name, property.Value);
            yield break;
        }
        if (source is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
                if (entry.Value != null) yield return new(entry.Key?.ToString() ?? string.Empty, entry.Value);
        }
    }

    public static string? String(object? value)
    {
        if (value is null) return null;
        if (value is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.String) return json.GetString();
            if (json.ValueKind == JsonValueKind.Number || json.ValueKind == JsonValueKind.True || json.ValueKind == JsonValueKind.False) return json.ToString();
            return null;
        }
        return value as string ?? value.ToString();
    }

    public static int? Int(object? value)
    {
        if (value is null) return null;
        if (value is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Number && json.TryGetInt32(out int n)) return n;
            return int.TryParse(json.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : null;
        }
        try { return Convert.ToInt32(value, CultureInfo.InvariantCulture); } catch { return null; }
    }

    public static long? Long(object? value)
    {
        if (value is null) return null;
        if (value is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Number && json.TryGetInt64(out long n)) return n;
            return long.TryParse(json.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : null;
        }
        try { return Convert.ToInt64(value, CultureInfo.InvariantCulture); } catch { return null; }
    }

    public static double? Double(object? value)
    {
        if (value is null) return null;
        if (value is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Number && json.TryGetDouble(out double n)) return n;
            return double.TryParse(json.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out n) ? n : null;
        }
        try { return Convert.ToDouble(value, CultureInfo.InvariantCulture); } catch { return null; }
    }

    public static bool? Bool(object? value)
    {
        if (value is null) return null;
        if (value is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.True) return true;
            if (json.ValueKind == JsonValueKind.False) return false;
            return bool.TryParse(json.ToString(), out bool parsed) ? parsed : null;
        }
        if (value is bool boolean) return boolean;
        return bool.TryParse(value.ToString(), out bool result) ? result : null;
    }
}
