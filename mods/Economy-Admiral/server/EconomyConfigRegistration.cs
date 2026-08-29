using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
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
        var configDirectory = Path.Combine(assemblyDirectory, "config");
        var configPath = Path.Combine(configDirectory, "config.json");
        var defaultConfigPath = Path.Combine(configDirectory, "config.default.json");

        if (!File.Exists(configPath))
        {
            if (!File.Exists(defaultConfigPath))
                throw new FileNotFoundException("Economy Admiral config: neither user config.json nor packaged config.default.json exists.", defaultConfigPath);

            Directory.CreateDirectory(configDirectory);
            var defaultJson = await File.ReadAllTextAsync(defaultConfigPath, cancellationToken);
            var defaultConfig = EconomyConfigJsonLoader.Deserialize(defaultJson);
            EconomyConfigValidator.Validate(defaultConfig);

            var tempPath = configPath + ".first-run.tmp";
            try
            {
                await File.WriteAllTextAsync(tempPath, defaultJson.TrimEnd() + Environment.NewLine, cancellationToken);
                File.Move(tempPath, configPath, false);
            }
            catch (IOException) when (File.Exists(configPath))
            {
                // Another startup path won the first-run creation race. Preserve the existing user file.
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        var config = EconomyConfigJsonLoader.Deserialize(await File.ReadAllTextAsync(configPath, cancellationToken));
        EconomyConfigValidator.Validate(config);
        return config;
    }
}