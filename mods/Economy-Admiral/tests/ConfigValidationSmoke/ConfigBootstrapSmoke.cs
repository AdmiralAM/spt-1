using SPTEconomy;

internal static class ConfigBootstrapSmoke
{
    internal static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "economy-admiral-config-bootstrap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var defaultPath = Path.Combine(root, "config.default.json");
            var userPath = Path.Combine(root, "config.json");
            await File.WriteAllTextAsync(defaultPath, "{\"mode\":\"Enforce\",\"preset\":\"Normal\"}");

            var first = await EconomyConfigBootstrap.LoadOrCreateAsync(root);
            if (first.Mode != EconomyMode.Enforce || first.Preset != EconomyPreset.Normal || !File.Exists(userPath))
                throw new InvalidOperationException("Config bootstrap did not create the Normal user config on first install.");
            Console.WriteLine("PASS first install creates user config from packaged default");

            const string userJson = "{\"mode\":\"Audit\",\"preset\":\"Hard\"}";
            await File.WriteAllTextAsync(userPath, userJson);
            await File.WriteAllTextAsync(defaultPath, "{\"mode\":\"Enforce\",\"preset\":\"Easy\"}");

            var updated = await EconomyConfigBootstrap.LoadOrCreateAsync(root);
            var persisted = await File.ReadAllTextAsync(userPath);
            if (updated.Mode != EconomyMode.Audit || updated.Preset != EconomyPreset.Hard || persisted != userJson)
                throw new InvalidOperationException("Config bootstrap overwrote existing user settings during update simulation.");
            Console.WriteLine("PASS update preserves existing user config when packaged default changes");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
