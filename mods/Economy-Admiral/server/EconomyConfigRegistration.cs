using System.Reflection;
using SPTarkov.Server.Core.DI;

namespace SPTEconomy;

public sealed class EconomyConfigRegistration : IOnDIConstruct
{
    public static async Task OnDIConstructAsync(IServiceCollection serviceCollection, CancellationToken cancellationToken)
    {
        var config = await LoadValidatedConfigAsync(cancellationToken);
        serviceCollection.AddSingleton(config);
    }

    private static async Task<EconomyConfig> LoadValidatedConfigAsync(CancellationToken cancellationToken)
    {
        var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? throw new InvalidOperationException("Economy Admiral config: unable to resolve assembly directory.");
        var configPath = Path.Combine(assemblyDirectory, "config", "config.json");

        var config = File.Exists(configPath)
            ? EconomyConfigJsonLoader.Deserialize(await File.ReadAllTextAsync(configPath, cancellationToken))
            : new EconomyConfig();

        EconomyConfigValidator.Validate(config);
        return config;
    }
}
