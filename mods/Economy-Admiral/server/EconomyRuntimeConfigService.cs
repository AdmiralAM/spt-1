using System.Text.Json;
using System.Text.Json.Serialization;
using Path = System.IO.Path;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;

namespace SPTEconomy;

[Injectable]
public sealed class EconomyRuntimeConfigService(ModHelper modHelper)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private EconomyConfig? cached;

    public async Task<EconomyConfig> GetAsync(CancellationToken cancellationToken)
    {
        if (cached is not null)
        {
            return cached;
        }

        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(EconomyRuntimeConfigService).Assembly);
        var configPath = Path.Combine(modPath, "config", "config.json");
        EconomyConfig config;

        if (!File.Exists(configPath))
        {
            config = new EconomyConfig();
        }
        else
        {
            try
            {
                var json = await File.ReadAllTextAsync(configPath, cancellationToken);
                ValidateEnumTokenTypes(json);
                config = JsonSerializer.Deserialize<EconomyConfig>(json, JsonOptions)
                    ?? throw new InvalidOperationException("Economy Admiral config: config.json deserialized to null.");
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException("Economy Admiral config: config.json is invalid or contains an unknown/unsupported value.", exception);
            }
        }

        EconomyConfigValidator.Validate(config);
        cached = config;
        return cached;
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
