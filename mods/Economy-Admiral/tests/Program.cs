using System.Text.Json;
using SPTEconomy;

var failures = new List<string>();

void ExpectValid(string name, EconomyConfig config)
{
    try
    {
        EconomyConfigValidator.Validate(config);
    }
    catch (Exception exception)
    {
        failures.Add($"{name}: expected valid, got {exception.GetType().Name}: {exception.Message}");
    }
}

void ExpectInvalid(string name, EconomyConfig config)
{
    try
    {
        EconomyConfigValidator.Validate(config);
        failures.Add($"{name}: expected InvalidOperationException, validation passed");
    }
    catch (InvalidOperationException)
    {
    }
    catch (Exception exception)
    {
        failures.Add($"{name}: expected InvalidOperationException, got {exception.GetType().Name}: {exception.Message}");
    }
}

void ExpectJsonInvalid(string name, string json)
{
    try
    {
        _ = JsonSerializer.Deserialize<EconomyConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        failures.Add($"{name}: expected JsonException, deserialization passed");
    }
    catch (JsonException)
    {
    }
    catch (Exception exception)
    {
        failures.Add($"{name}: expected JsonException, got {exception.GetType().Name}: {exception.Message}");
    }
}

ExpectValid("defaults", new EconomyConfig());
ExpectValid("exceptional manual rarity", new EconomyConfig
{
    ManualOverrides = new Dictionary<string, ManualItemOverride>(StringComparer.Ordinal)
    {
        ["fixture-template"] = new() { Rarity = "Exceptional", Note = "fixture" },
    },
});
ExpectValid("nested report path", new EconomyConfig { ReportRelativePath = "reports/custom/economy.json" });

ExpectInvalid("empty report path", new EconomyConfig { ReportRelativePath = " " });
ExpectInvalid("relative path traversal", new EconomyConfig { ReportRelativePath = "reports/../outside.json" });
ExpectInvalid("backslash path traversal", new EconomyConfig { ReportRelativePath = @"reports\..\outside.json" });
ExpectInvalid("rooted report path", new EconomyConfig { ReportRelativePath = Path.GetFullPath("outside.json") });

ExpectInvalid("zero rarity threshold", new EconomyConfig
{
    Rarity = new RarityThresholds { CommonMinSources = 8, UncommonMinSources = 4, RareMinSources = 0 },
});
ExpectInvalid("equal rarity thresholds", new EconomyConfig
{
    Rarity = new RarityThresholds { CommonMinSources = 4, UncommonMinSources = 4, RareMinSources = 2 },
});
ExpectInvalid("inverted rarity thresholds", new EconomyConfig
{
    Rarity = new RarityThresholds { CommonMinSources = 4, UncommonMinSources = 8, RareMinSources = 2 },
});

ExpectInvalid("zero reward multiple", new EconomyConfig
{
    CustomAuditPolicy = new AuditPolicy { QuestRewardVsVanillaMedianWarnMultiple = 0 },
});
ExpectInvalid("negative reward multiple", new EconomyConfig
{
    CustomAuditPolicy = new AuditPolicy { HighXpLowDepthWarnMultiple = -1 },
});
ExpectInvalid("NaN policy", new EconomyConfig
{
    CustomAuditPolicy = new AuditPolicy { HighStandingLowDepthWarnMultiple = double.NaN },
});
ExpectInvalid("infinite policy", new EconomyConfig
{
    CustomAuditPolicy = new AuditPolicy { RestartableHighItemValueWarnMultiple = double.PositiveInfinity },
});
ExpectInvalid("negative structural weight", new EconomyConfig
{
    CustomAuditPolicy = new AuditPolicy { ObjectiveConditionWeight = -0.01 },
});
ExpectInvalid("negative structural cap", new EconomyConfig
{
    CustomAuditPolicy = new AuditPolicy { MaxObjectiveContribution = -1 },
});
ExpectInvalid("zero trader source threshold", new EconomyConfig
{
    CustomAuditPolicy = new AuditPolicy { DuplicateTraderSourcesWarnCount = 0 },
});

ExpectInvalid("unsupported manual rarity", new EconomyConfig
{
    ManualOverrides = new Dictionary<string, ManualItemOverride>(StringComparer.Ordinal)
    {
        ["fixture-template"] = new() { Rarity = "VeryRare" },
    },
});
ExpectInvalid("empty manual override id", new EconomyConfig
{
    ManualOverrides = new Dictionary<string, ManualItemOverride>(StringComparer.Ordinal)
    {
        [""] = new() { Rarity = "Rare" },
    },
});

ExpectJsonInvalid("unsupported mode enum", "{\"mode\":\"Dangerous\"}");
ExpectJsonInvalid("unsupported preset enum", "{\"preset\":\"Nightmare\"}");
ExpectJsonInvalid("malformed json", "{\"mode\":\"Audit\"");

if (failures.Count > 0)
{
    Console.Error.WriteLine("Economy Admiral config validation harness FAILED:");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($"- {failure}");
    }
    return 1;
}

Console.WriteLine("Economy Admiral config validation harness PASS");
return 0;
