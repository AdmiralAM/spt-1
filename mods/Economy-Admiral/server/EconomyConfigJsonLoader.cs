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
            ValidateEnumTokenTypes(json);
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

    private static void ValidateEnumTokenTypes(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Economy Admiral config root must be a JSON object.");
        }

        ValidateStringProperty(document.RootElement, "mode");
        ValidateStringProperty(document.RootElement, "preset");
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
