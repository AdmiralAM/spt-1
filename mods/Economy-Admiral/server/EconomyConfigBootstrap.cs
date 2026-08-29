namespace SPTEconomy;

public static class EconomyConfigBootstrap
{
    public static async Task<EconomyConfig> LoadOrCreateAsync(string configDirectory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(configDirectory))
            throw new ArgumentException("Economy Admiral config directory must not be empty.", nameof(configDirectory));

        var configPath = Path.Combine(configDirectory, "config.json");
        var defaultConfigPath = Path.Combine(configDirectory, "config.default.json");

        if (!File.Exists(configPath))
        {
            if (!File.Exists(defaultConfigPath))
                throw new FileNotFoundException("Economy Admiral config: neither user config.json nor packaged config.default.json exists.", defaultConfigPath);

            var defaultJson = await File.ReadAllTextAsync(defaultConfigPath, cancellationToken);
            var defaultConfig = EconomyConfigJsonLoader.Deserialize(defaultJson);
            EconomyConfigValidator.Validate(defaultConfig);

            Directory.CreateDirectory(configDirectory);
            var tempPath = configPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllTextAsync(tempPath, defaultJson.TrimEnd() + Environment.NewLine, cancellationToken);
                File.Move(tempPath, configPath, false);
            }
            catch (IOException) when (File.Exists(configPath))
            {
                // Another first-start path created the user config. Never overwrite it.
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