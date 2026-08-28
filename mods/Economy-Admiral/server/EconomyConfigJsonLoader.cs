using System.Text.Json;
using System.Text.Json.Serialization;

namespace SPTEconomy;

public static class EconomyConfigJsonLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static EconomyConfig Deserialize(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Economy Admiral config root must be a JSON object.");
            }

            ValidateNoDuplicateProperties(document.RootElement, "config");
            ValidateStringProperty(document.RootElement, "mode");
            ValidateStringProperty(document.RootElement, "preset");

            return JsonSerializer.Deserialize<EconomyConfig>(json, JsonOptions)
                ?? throw new InvalidOperationException("Economy Admiral config: config.json deserialized to null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "Economy Admiral config: config.json is invalid or contains an unknown/unsupported value.",
                exception
            );
        }
    }

    private static void ValidateNoDuplicateProperties(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new JsonException($"Economy Admiral config: duplicate property '{property.Name}' at '{path}'.");
                }

                ValidateNoDuplicateProperties(property.Value, $"{path}.{property.Name}");
            }
            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                ValidateNoDuplicateProperties(item, $"{path}[{index}]");
                index++;
            }
        }
    }

    private static void ValidateStringProperty(JsonElement root, string propertyName)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind != JsonValueKind.String)
            {
                throw new JsonException($"Economy Admiral config: '{propertyName}' must be a string enum value.");
            }

            return;
        }
    }
}
