using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace SPTEconomy;

[Injectable(TypePriority = OnLoadOrder.Routers)]
public sealed class EconomySettingsRouter(JsonUtil jsonUtil, EconomySettingsRouterCallback callback)
    : StaticRouter(jsonUtil,
    [
        new RouteAction<EmptyRequestData>("/economy-admiral/settings/get", async (url, info, sessionId, output, cancellationToken) => await callback.GetAsync(url, info, sessionId, cancellationToken)),
        new RouteAction<EconomySettingsUpdateRequest>("/economy-admiral/settings/save", async (url, info, sessionId, output, cancellationToken) => await callback.SaveAsync(url, info, sessionId, cancellationToken)),
    ])
{
}

[Injectable]
public sealed class EconomySettingsRouterCallback(ModHelper modHelper, ISptLogger<EconomySettingsRouterCallback> logger)
{
    private static readonly JsonSerializerOptions ConfigJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static string SerializeResponse<T>(T value) => JsonSerializer.Serialize(value, ConfigJsonOptions);

    public ValueTask<string> GetAsync(string url, EmptyRequestData info, MongoId sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var config = LoadPersistedConfig();
            return new ValueTask<string>(SerializeResponse(EconomySettingsSnapshot.From(config, false)));
        }
        catch (Exception exception)
        {
            logger.Error($"[Economy Admiral] settings GET failed: {exception.Message}");
            return new ValueTask<string>(SerializeResponse(new EconomySettingsError(false, exception.Message)));
        }
    }

    public async ValueTask<string> SaveAsync(string url, EconomySettingsUpdateRequest info, MongoId sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var current = LoadPersistedConfig();
            var updated = current with
            {
                Mode = ParseEnum<EconomyMode>(info.Mode, nameof(info.Mode)),
                Preset = ParseEnum<EconomyPreset>(info.Preset, nameof(info.Preset)),
                EnablePlayableEconomyBundle = info.EnablePlayableEconomyBundle,
                EnableQuestEconomyCluster = info.EnableQuestEconomyCluster,
                EnableTraderEconomyCluster = info.EnableTraderEconomyCluster,
                EnableFleaEconomyCluster = info.EnableFleaEconomyCluster,
                EnableLootEconomyCluster = info.EnableLootEconomyCluster,
                EnableItemRewardStackNormalization = info.EnableItemRewardStackNormalization,
                EnableQuestXpPressure = info.EnableQuestXpPressure,
                EnableQuestStandingPressure = info.EnableQuestStandingPressure,
                EnableRestartableQuestPressure = info.EnableRestartableQuestPressure,
                EnableTraderPurchasePressure = info.EnableTraderPurchasePressure,
                EnableTraderSellPressure = info.EnableTraderSellPressure,
                EnableFleaPurchasePressure = info.EnableFleaPurchasePressure,
                EnableFleaListingFeePressure = info.EnableFleaListingFeePressure,
                EnableLootPressure = info.EnableLootPressure,
                EnableLooseLootPressure = info.EnableLooseLootPressure,
                EnableStaticLootPressure = info.EnableStaticLootPressure,
                CustomTraderPurchasePriceMultiplier = info.CustomTraderPurchasePriceMultiplier,
                CustomTraderSellPayoutMultiplier = info.CustomTraderSellPayoutMultiplier,
                CustomFleaBasePriceMultiplier = info.CustomFleaBasePriceMultiplier,
                CustomFleaMaxPriceDifferenceBelowHandbookPercent = info.CustomFleaMaxPriceDifferenceBelowHandbookPercent,
                CustomFleaHandbookPriceMultiplier = info.CustomFleaHandbookPriceMultiplier,
                CustomFleaListingFeeMultiplier = info.CustomFleaListingFeeMultiplier,
                CustomLooseLootScale = info.CustomLooseLootScale,
                CustomStaticLootScale = info.CustomStaticLootScale,
                CustomQuestItemBudgetMultiple = info.CustomQuestItemBudgetMultiple,
                CustomRestartableQuestItemBudgetMultiple = info.CustomRestartableQuestItemBudgetMultiple,
                CustomQuestXpMultiple = info.CustomQuestXpMultiple,
                CustomRestartableQuestXpMultiple = info.CustomRestartableQuestXpMultiple,
                CustomQuestStandingMultiple = info.CustomQuestStandingMultiple,
            };

            EconomyConfigValidator.Validate(updated);
            await PersistValidatedAsync(updated, cancellationToken);
            var persisted = LoadPersistedConfig();
            if (!PersistedConfigEquivalent(updated, persisted))
                throw new InvalidOperationException("Persisted Economy Admiral config did not structurally round-trip to the requested settings.");

            logger.Info("[Economy Admiral] settings saved from client UI; changes apply after next SPT server restart.");
            return SerializeResponse(EconomySettingsSnapshot.From(persisted, true));
        }
        catch (Exception exception)
        {
            logger.Error($"[Economy Admiral] settings SAVE failed: {exception.Message}");
            return SerializeResponse(new EconomySettingsError(false, exception.Message));
        }
    }

    private EconomyConfig LoadPersistedConfig()
    {
        var path = GetConfigPath();
        if (!File.Exists(path)) throw new FileNotFoundException("Economy Admiral config.json is missing.", path);
        var config = JsonSerializer.Deserialize<EconomyConfig>(File.ReadAllText(path), ConfigJsonOptions)
            ?? throw new InvalidOperationException("Economy Admiral config.json deserialized to null.");
        EconomyConfigValidator.Validate(config);
        return config;
    }

    private async Task PersistValidatedAsync(EconomyConfig config, CancellationToken cancellationToken)
    {
        var path = GetConfigPath();
        var directory = Path.GetDirectoryName(path)!;
        var tempPath = path + ".tmp";
        var backupPath = path + ".bak";
        Directory.CreateDirectory(directory);
        var serialized = SerializePersistedConfig(config);
        var roundTrip = JsonSerializer.Deserialize<EconomyConfig>(serialized, ConfigJsonOptions)
            ?? throw new InvalidOperationException("Serialized Economy Admiral settings could not be read back.");
        EconomyConfigValidator.Validate(roundTrip);
        if (!PersistedConfigEquivalent(config, roundTrip))
            throw new InvalidOperationException("Serialized Economy Admiral settings changed configured activation or override state.");

        try
        {
            await File.WriteAllTextAsync(tempPath, serialized + Environment.NewLine, cancellationToken);
            if (File.Exists(path)) File.Copy(path, backupPath, true);
            File.Move(tempPath, path, true);
        }
        catch
        {
            if (File.Exists(backupPath)) File.Copy(backupPath, path, true);
            throw;
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static bool PersistedConfigEquivalent(EconomyConfig left, EconomyConfig right) =>
        JsonNode.DeepEquals(JsonNode.Parse(SerializePersistedConfig(left)), JsonNode.Parse(SerializePersistedConfig(right)));

    private static string SerializePersistedConfig(EconomyConfig config)
    {
        var node = JsonSerializer.SerializeToNode(config, ConfigJsonOptions) as JsonObject
            ?? throw new InvalidOperationException("Economy Admiral config did not serialize to an object.");
        node["EnableItemRewardStackNormalization"] = config.ConfiguredEnableItemRewardStackNormalization;
        node["EnableQuestXpPressure"] = config.ConfiguredEnableQuestXpPressure;
        node["EnableQuestStandingPressure"] = config.ConfiguredEnableQuestStandingPressure;
        node["EnableRestartableQuestPressure"] = config.ConfiguredEnableRestartableQuestPressure;
        node["EnableTraderPurchasePressure"] = config.ConfiguredEnableTraderPurchasePressure;
        node["EnableTraderSellPressure"] = config.ConfiguredEnableTraderSellPressure;
        node["EnableFleaPurchasePressure"] = config.ConfiguredEnableFleaPurchasePressure;
        node["EnableFleaListingFeePressure"] = config.ConfiguredEnableFleaListingFeePressure;
        node["EnableLootPressure"] = config.ConfiguredEnableLootPressure;
        node["EnableLooseLootPressure"] = config.ConfiguredEnableLooseLootPressure;
        node["EnableStaticLootPressure"] = config.ConfiguredEnableStaticLootPressure;
        node["QuestRewardOverrides"] = JsonSerializer.SerializeToNode(config.ConfiguredQuestRewardOverrides, ConfigJsonOptions);
        return node.ToJsonString(ConfigJsonOptions);
    }

    private string GetConfigPath()
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(EconomySettingsRouterCallback).Assembly);
        return Path.Combine(modPath, "config", "config.json");
    }

    private static T ParseEnum<T>(string value, string field) where T : struct, Enum
    {
        if (!Enum.TryParse<T>(value, true, out var parsed) || !Enum.IsDefined(parsed))
            throw new InvalidOperationException($"Unsupported {field} value '{value}'.");
        return parsed;
    }
}

public sealed record EconomySettingsUpdateRequest : IRequestData
{
    public string Mode { get; init; } = "Enforce";
    public string Preset { get; init; } = "Normal";
    public bool EnablePlayableEconomyBundle { get; init; } = true;
    public bool EnableQuestEconomyCluster { get; init; } = true;
    public bool EnableTraderEconomyCluster { get; init; } = true;
    public bool EnableFleaEconomyCluster { get; init; } = true;
    public bool EnableLootEconomyCluster { get; init; } = true;
    public bool EnableItemRewardStackNormalization { get; init; }
    public bool EnableQuestXpPressure { get; init; }
    public bool EnableQuestStandingPressure { get; init; }
    public bool EnableRestartableQuestPressure { get; init; }
    public bool EnableTraderPurchasePressure { get; init; }
    public bool EnableTraderSellPressure { get; init; }
    public bool EnableFleaPurchasePressure { get; init; }
    public bool EnableFleaListingFeePressure { get; init; }
    public bool EnableLootPressure { get; init; }
    public bool EnableLooseLootPressure { get; init; }
    public bool EnableStaticLootPressure { get; init; }
    public double CustomTraderPurchasePriceMultiplier { get; init; } = 1.15;
    public double CustomTraderSellPayoutMultiplier { get; init; } = 0.85;
    public double CustomFleaBasePriceMultiplier { get; init; } = 1.65;
    public double CustomFleaMaxPriceDifferenceBelowHandbookPercent { get; init; } = 45.0;
    public double CustomFleaHandbookPriceMultiplier { get; init; } = 1.10;
    public double CustomFleaListingFeeMultiplier { get; init; } = 1.25;
    public double CustomLooseLootScale { get; init; } = 0.85;
    public double CustomStaticLootScale { get; init; } = 0.85;
    public double CustomQuestItemBudgetMultiple { get; init; } = 1.50;
    public double CustomRestartableQuestItemBudgetMultiple { get; init; } = 1.15;
    public double CustomQuestXpMultiple { get; init; } = 1.50;
    public double CustomRestartableQuestXpMultiple { get; init; } = 1.15;
    public double CustomQuestStandingMultiple { get; init; } = 1.50;
}

public sealed record EconomySettingsSnapshot(
    bool Ok, bool RestartRequired, string Mode, string Preset, bool EnablePlayableEconomyBundle,
    bool EnableQuestEconomyCluster, bool EnableTraderEconomyCluster, bool EnableFleaEconomyCluster, bool EnableLootEconomyCluster,
    bool EnableItemRewardStackNormalization, bool EnableQuestXpPressure, bool EnableQuestStandingPressure, bool EnableRestartableQuestPressure,
    bool EnableTraderPurchasePressure, bool EnableTraderSellPressure, bool EnableFleaPurchasePressure, bool EnableFleaListingFeePressure,
    bool EnableLootPressure, bool EnableLooseLootPressure, bool EnableStaticLootPressure,
    double CustomTraderPurchasePriceMultiplier, double CustomTraderSellPayoutMultiplier,
    double CustomFleaBasePriceMultiplier, double CustomFleaMaxPriceDifferenceBelowHandbookPercent,
    double CustomFleaHandbookPriceMultiplier, double CustomFleaListingFeeMultiplier,
    double CustomLooseLootScale, double CustomStaticLootScale,
    double CustomQuestItemBudgetMultiple, double CustomRestartableQuestItemBudgetMultiple,
    double CustomQuestXpMultiple, double CustomRestartableQuestXpMultiple, double CustomQuestStandingMultiple)
{
    public static EconomySettingsSnapshot From(EconomyConfig config, bool restartRequired) => new(
        true, restartRequired, config.Mode.ToString(), config.Preset.ToString(), config.EnablePlayableEconomyBundle,
        config.EnableQuestEconomyCluster, config.EnableTraderEconomyCluster, config.EnableFleaEconomyCluster, config.EnableLootEconomyCluster,
        config.ConfiguredEnableItemRewardStackNormalization, config.ConfiguredEnableQuestXpPressure,
        config.ConfiguredEnableQuestStandingPressure, config.ConfiguredEnableRestartableQuestPressure,
        config.ConfiguredEnableTraderPurchasePressure, config.ConfiguredEnableTraderSellPressure,
        config.ConfiguredEnableFleaPurchasePressure, config.ConfiguredEnableFleaListingFeePressure,
        config.ConfiguredEnableLootPressure, config.ConfiguredEnableLooseLootPressure, config.ConfiguredEnableStaticLootPressure,
        config.CustomTraderPurchasePriceMultiplier, config.CustomTraderSellPayoutMultiplier,
        config.CustomFleaBasePriceMultiplier, config.CustomFleaMaxPriceDifferenceBelowHandbookPercent,
        config.CustomFleaHandbookPriceMultiplier, config.CustomFleaListingFeeMultiplier,
        config.CustomLooseLootScale, config.CustomStaticLootScale,
        config.CustomQuestItemBudgetMultiple, config.CustomRestartableQuestItemBudgetMultiple,
        config.CustomQuestXpMultiple, config.CustomRestartableQuestXpMultiple, config.CustomQuestStandingMultiple);
}

public sealed record EconomySettingsError(bool Ok, string Error);
