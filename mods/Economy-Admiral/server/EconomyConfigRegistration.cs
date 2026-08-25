using System.Reflection;
using System.Text.Json;
using SPTarkov.Server.Core.DI;

namespace SPTEconomy;

public sealed class EconomyConfigRegistration : IOnDIConstruct
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

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
                throw new InvalidOperationException(
                    "Economy Admiral config: config.json is invalid JSON or contains an unsupported enum/value type.",
                    exception
                );
            }
        }

        EconomyConfigValidator.Validate(config);
        return config;
    }
}
