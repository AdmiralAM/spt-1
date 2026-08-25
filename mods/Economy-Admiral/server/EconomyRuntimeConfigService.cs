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
        if (!File.Exists(configPath))
        {
            return cached = new EconomyConfig();
        }

        await using var stream = File.OpenRead(configPath);
        cached = await JsonSerializer.DeserializeAsync<EconomyConfig>(stream, JsonOptions, cancellationToken)
            ?? new EconomyConfig();
        return cached;
    }
}
