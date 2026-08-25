using Path = System.IO.Path;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;

namespace SPTEconomy;

[Injectable]
public sealed class EconomyRuntimeConfigService(ModHelper modHelper)
{
    private EconomyConfig? cached;

    public async Task<EconomyConfig> GetAsync(CancellationToken cancellationToken)
    {
        if (cached is not null)
        {
            return cached;
        }

        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(EconomyRuntimeConfigService).Assembly);
        var configPath = Path.Combine(modPath, "config", "config.json");
        var config = File.Exists(configPath)
            ? EconomyConfigJsonLoader.Deserialize(await File.ReadAllTextAsync(configPath, cancellationToken))
            : new EconomyConfig();

        EconomyConfigValidator.Validate(config);
        cached = config;
        return cached;
    }
}
