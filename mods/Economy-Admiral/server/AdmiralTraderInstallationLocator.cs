using System.Text.Json;

namespace SPTEconomy;

public static class AdmiralTraderInstallationLocator
{
    public const string ExpectedModGuid = "com.admiralam.spt.admiraltrader";

    public static string? LocateFromEconomyAdmiralModPath(string economyAdmiralModPath)
    {
        if (string.IsNullOrWhiteSpace(economyAdmiralModPath))
        {
            throw new InvalidOperationException("Economy Admiral Admiral Trader locator: Economy Admiral mod path must not be empty.");
        }

        var fullModPath = Path.GetFullPath(economyAdmiralModPath);
        var modsRoot = Directory.GetParent(fullModPath)?.FullName
            ?? throw new InvalidOperationException("Economy Admiral Admiral Trader locator: cannot resolve user/mods root.");

        var matches = new List<string>();
        foreach (var directory in Directory.EnumerateDirectories(modsRoot))
        {
            var manifestPath = Path.Combine(directory, "manifests", "campaign-manifest.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            string? modGuid;
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
                modGuid = document.RootElement
                    .GetProperty("product")
                    .GetProperty("modGuid")
                    .GetString();
            }
            catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                continue;
            }

            if (string.Equals(modGuid, ExpectedModGuid, StringComparison.Ordinal))
            {
                matches.Add(directory);
            }
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException("Economy Admiral Admiral Trader locator: multiple installed mods claim the Admiral Trader modGuid.");
        }

        return matches.SingleOrDefault();
    }
}
