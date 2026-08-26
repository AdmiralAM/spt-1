using SPTEconomy;

static void MustPass(string name, EconomyConfig config)
{
    try
    {
        EconomyConfigValidator.Validate(config);
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        throw new InvalidOperationException($"Expected config '{name}' to pass but it failed: {exception.Message}", exception);
    }
}

static void MustFail(string name, EconomyConfig config)
{
    try
    {
        EconomyConfigValidator.Validate(config);
    }
    catch (InvalidOperationException)
    {
        Console.WriteLine($"PASS {name}");
        return;
    }
    throw new InvalidOperationException($"Expected config '{name}' to fail validation.");
}

static void JsonMustPass(string name, string json)
{
    try
    {
        var config = EconomyConfigJsonLoader.Deserialize(json);
        EconomyConfigValidator.Validate(config);
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        throw new InvalidOperationException($"Expected JSON fixture '{name}' to pass but it failed: {exception.Message}", exception);
    }
}

static void JsonMustFail(string name, string json)
{
    try
    {
        var config = EconomyConfigJsonLoader.Deserialize(json);
        EconomyConfigValidator.Validate(config);
    }
    catch (InvalidOperationException)
    {
        Console.WriteLine($"PASS {name}");
        return;
    }
    throw new InvalidOperationException($"Expected JSON fixture '{name}' to fail validation.");
}

MustPass("defaults", new EconomyConfig());
MustPass("opt-in bounded item stack normalization", new EconomyConfig { EnableItemRewardStackNormalization = true });
foreach (var preset in Enum.GetValues<EconomyPreset>()) MustPass($"preset {preset}", new EconomyConfig { Preset = preset });
MustPass("supported exceptional override", new EconomyConfig
{
    ManualOverrides = new Dictionary<string, ManualItemOverride>(StringComparer.Ordinal)
    {
        ["fixture-template"] = new() { Rarity = "Exceptional" },
    },
});

MustFail("unimplemented repeated raid loot decay", new EconomyConfig { RepeatedRaidLootDecay = true });
MustFail("empty report path", new EconomyConfig { ReportRelativePath = " " });
MustFail("rooted report path", new EconomyConfig { ReportRelativePath = Path.GetFullPath("outside.json") });
MustFail("parent traversal slash", new EconomyConfig { ReportRelativePath = "reports/../outside.json" });
MustFail("parent traversal backslash", new EconomyConfig { ReportRelativePath = "reports\\..\\outside.json" });
MustFail("zero rare threshold", new EconomyConfig { Rarity = new() { CommonMinSources = 8, UncommonMinSources = 4, RareMinSources = 0 } });
MustFail("unordered rarity thresholds", new EconomyConfig { Rarity = new() { CommonMinSources = 4, UncommonMinSources = 4, RareMinSources = 2 } });
MustFail("unsupported manual rarity", new EconomyConfig
{
    ManualOverrides = new Dictionary<string, ManualItemOverride>(StringComparer.Ordinal)
    {
        ["fixture-template"] = new() { Rarity = "VeryRare" },
    },
});
MustFail("empty manual override id", new EconomyConfig
{
    ManualOverrides = new Dictionary<string, ManualItemOverride>(StringComparer.Ordinal)
    {
        [""] = new() { Rarity = "Rare" },
    },
});
MustFail("zero warning multiple", new EconomyConfig { CustomAuditPolicy = new() { QuestRewardVsVanillaMedianWarnMultiple = 0 } });
MustFail("negative warning multiple", new EconomyConfig { CustomAuditPolicy = new() { HighXpLowDepthWarnMultiple = -1 } });
MustFail("nan policy value", new EconomyConfig { CustomAuditPolicy = new() { RestartableHighXpWarnMultiple = double.NaN } });
MustFail("positive infinity policy value", new EconomyConfig { CustomAuditPolicy = new() { LowDepthMaxRelativeMultiple = double.PositiveInfinity } });
MustFail("negative structural weight", new EconomyConfig { CustomAuditPolicy = new() { ObjectiveConditionWeight = -0.01 } });
MustFail("zero duplicate trader threshold", new EconomyConfig { CustomAuditPolicy = new() { DuplicateTraderSourcesWarnCount = 0 } });
MustFail("null rarity object", new EconomyConfig { Rarity = null! });
MustFail("null policy object", new EconomyConfig { CustomAuditPolicy = null! });
MustFail("null overrides object", new EconomyConfig { ManualOverrides = null! });

JsonMustPass("minimal JSON", "{}");
JsonMustPass("case-insensitive known keys", "{\"MODE\":\"Audit\",\"PRESET\":\"Normal\"}");
JsonMustPass("opt-in bounded item stack JSON", "{\"enableItemRewardStackNormalization\":true}");
foreach (var preset in new[] { "Easy", "Normal", "Hard", "Custom" }) JsonMustPass($"JSON preset {preset}", $"{{\"preset\":\"{preset}\"}}");
JsonMustFail("enabled repeated raid loot decay", "{\"repeatedRaidLootDecay\":true}");
JsonMustFail("numeric mode", "{\"mode\":1}");
JsonMustFail("numeric preset", "{\"preset\":2}");
JsonMustFail("unknown mode string", "{\"mode\":\"Explode\"}");
JsonMustFail("unknown preset string", "{\"preset\":\"Nightmare\"}");
JsonMustFail("unknown top-level property", "{\"mode\":\"Audit\",\"mysterySetting\":true}");
JsonMustFail("duplicate top-level key", "{\"mode\":\"Audit\",\"MODE\":\"Off\"}");
JsonMustFail("duplicate nested key", "{\"rarity\":{\"commonMinSources\":8,\"COMMONMINSOURCES\":9}}");
JsonMustFail("non-object root", "[]");
JsonMustFail("null rarity JSON", "{\"rarity\":null}");
JsonMustFail("null policy JSON", "{\"customAuditPolicy\":null}");
JsonMustFail("null overrides JSON", "{\"manualOverrides\":null}");
JsonMustFail("null override entry JSON", "{\"manualOverrides\":{\"fixture-template\":null}}");

Console.WriteLine("Economy Admiral config validation smoke PASS");
