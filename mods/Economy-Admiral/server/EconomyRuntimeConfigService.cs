using System.Text.Json;
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
                await using var stream = File.OpenRead(configPath);
                config = await JsonSerializer.DeserializeAsync<EconomyConfig>(stream, JsonOptions, cancellationToken)
                    ?? throw new InvalidOperationException("Economy Admiral config: config.json deserialized to null.");
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException("Economy Admiral config: config.json is invalid JSON or contains an unsupported enum/value type.", exception);
            }
        }

        EconomyConfigValidator.Validate(config);
        cached = config;
        return cached;
    }
}
