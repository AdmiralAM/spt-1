using System.Text.Json;
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
        new RouteAction<EmptyRequestData>(
            "/economy-admiral/settings/get",
            async (url, info, sessionId, output, cancellationToken) =>
                await callback.GetAsync(url, info, sessionId, cancellationToken)),
        new RouteAction<EconomySettingsUpdateRequest>(
            "/economy-admiral/settings/save",
            async (url, info, sessionId, output, cancellationToken) =>
                await callback.SaveAsync(url, info, sessionId, cancellationToken)),
    ])
{
}

[Injectable]
public sealed class EconomySettingsRouterCallback(
    ModHelper modHelper,
    JsonUtil jsonUtil,
    ISptLogger<EconomySettingsRouterCallback> logger)
{
    private static readonly JsonSerializerOptions ConfigJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public ValueTask<string> GetAsync(
        string url,
        EmptyRequestData info,
        MongoId sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var config = LoadPersistedConfig();
            return new ValueTask<string>(jsonUtil.Serialize(EconomySettingsSnapshot.From(config, restartRequired: false)));
        }
        catch (Exception exception)
        {
            logger.Error($"[Economy Admiral] settings GET failed: {exception.Message}");
            return new ValueTask<string>(jsonUtil.Serialize(new EconomySettingsError(false, exception.Message)));
        }
    }

    public async ValueTask<string> SaveAsync(
        string url,
        EconomySettingsUpdateRequest info,
        MongoId sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var current = LoadPersistedConfig();
            var mode = ParseEnum<EconomyMode>(info.Mode, nameof(info.Mode));
            var preset = ParseEnum<EconomyPreset>(info.Preset, nameof(info.Preset));

            var customPolicy = current.CustomAuditPolicy with
            {
                HighItemValueLowStructureWarnMultiple = info.CustomQuestItemBudgetMultiple,
                RestartableHighItemValueWarnMultiple = info.CustomRestartableQuestItemBudgetMultiple,
                HighXpLowDepthWarnMultiple = info.CustomQuestXpMultiple,
                RestartableHighXpWarnMultiple = info.CustomRestartableQuestXpMultiple,
                HighStandingLowDepthWarnMultiple = info.CustomQuestStandingMultiple,
            };

            var updated = current with
            {
                Mode = mode,
                Preset = preset,
                EnablePlayableEconomyBundle = info.EnablePlayableEconomyBundle,
                EnableQuestEconomyCluster = info.EnableQuestEconomyCluster,
                EnableTraderEconomyCluster = info.EnableTraderEconomyCluster,
                EnableFleaEconomyCluster = info.EnableFleaEconomyCluster,
                EnableLootEconomyCluster = info.EnableLootEconomyCluster,
                CustomTraderPurchasePriceMultiplier = info.CustomTraderPurchasePriceMultiplier,
                CustomTraderSellPayoutMultiplier = info.CustomTraderSellPayoutMultiplier,
                CustomFleaBasePriceMultiplier = info.CustomFleaBasePriceMultiplier,
                CustomFleaListingFeeMultiplier = info.CustomFleaListingFeeMultiplier,
                CustomLooseLootScale = info.CustomLooseLootScale,
                CustomStaticLootScale = info.CustomStaticLootScale,
                CustomAuditPolicy = customPolicy,
            };

            EconomyConfigValidator.Validate(updated);
            await PersistValidatedAsync(updated, cancellationToken);
            logger.Info("[Economy Admiral] settings saved from client UI; changes apply after next SPT server restart.");
            return jsonUtil.Serialize(EconomySettingsSnapshot.From(updated, restartRequired: true));
        }
        catch (Exception exception)
        {
            logger.Error($"[Economy Admiral] settings SAVE failed: {exception.Message}");
            return jsonUtil.Serialize(new EconomySettingsError(false, exception.Message));
        }
    }

    private EconomyConfig LoadPersistedConfig()
    {
        var path = GetConfigPath();
        if (!File.Exists(path))
            throw new FileNotFoundException("Economy Admiral config.json is missing.", path);

        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<EconomyConfig>(json, ConfigJsonOptions)
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

        var serialized = JsonSerializer.Serialize(config, ConfigJsonOptions);
        var roundTrip = JsonSerializer.Deserialize<EconomyConfig>(serialized, ConfigJsonOptions)
            ?? throw new InvalidOperationException("Serialized Economy Admiral settings could not be read back.");
        EconomyConfigValidator.Validate(roundTrip);

        try
        {
            await File.WriteAllTextAsync(tempPath, serialized + Environment.NewLine, cancellationToken);
            if (File.Exists(path))
                File.Copy(path, backupPath, overwrite: true);
            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(backupPath))
                File.Copy(backupPath, path, overwrite: true);
            throw;
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private string GetConfigPath()
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(EconomySettingsRouterCallback).Assembly);
        return Path.Combine(modPath, "config", "config.json");
    }

    private static T ParseEnum<T>(string value, string field) where T : struct, Enum
    {
        if (!Enum.TryParse<T>(value, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            throw new InvalidOperationException($"Unsupported {field} value '{value}'.");
        return parsed;
    }
}

public sealed record EconomySettingsUpdateRequest : IRequestData
{
    public string Mode { get; init; } = "Audit";
    public string Preset { get; init; } = "Normal";
    public bool EnablePlayableEconomyBundle { get; init; } = true;
    public bool EnableQuestEconomyCluster { get; init; } = true;
    public bool EnableTraderEconomyCluster { get; init; } = true;
    public bool EnableFleaEconomyCluster { get; init; } = true;
    public bool EnableLootEconomyCluster { get; init; } = true;
    public double CustomTraderPurchasePriceMultiplier { get; init; } = 1.15;
    public double CustomTraderSellPayoutMultiplier { get; init; } = 0.85;
    public double CustomFleaBasePriceMultiplier { get; init; } = 1.65;
    public double CustomFleaListingFeeMultiplier { get; init; } = 1.25;
    public double CustomLooseLootScale { get; init; } = 0.85;
    public double CustomStaticLootScale { get; init; } = 0.85;
    public double CustomQuestItemBudgetMultiple { get; init; } = 3.0;
    public double CustomRestartableQuestItemBudgetMultiple { get; init; } = 2.0;
    public double CustomQuestXpMultiple { get; init; } = 3.0;
    public double CustomRestartableQuestXpMultiple { get; init; } = 2.0;
    public double CustomQuestStandingMultiple { get; init; } = 3.0;
}

public sealed record EconomySettingsSnapshot(
    bool Ok,
    bool RestartRequired,
    string Mode,
    string Preset,
    bool EnablePlayableEconomyBundle,
    bool EnableQuestEconomyCluster,
    bool EnableTraderEconomyCluster,
    bool EnableFleaEconomyCluster,
    bool EnableLootEconomyCluster,
    double CustomTraderPurchasePriceMultiplier,
    double CustomTraderSellPayoutMultiplier,
    double CustomFleaBasePriceMultiplier,
    double CustomFleaListingFeeMultiplier,
    double CustomLooseLootScale,
    double CustomStaticLootScale,
    double CustomQuestItemBudgetMultiple,
    double CustomRestartableQuestItemBudgetMultiple,
    double CustomQuestXpMultiple,
    double CustomRestartableQuestXpMultiple,
    double CustomQuestStandingMultiple)
{
    public static EconomySettingsSnapshot From(EconomyConfig config, bool restartRequired) => new(
        true,
        restartRequired,
        config.Mode.ToString(),
        config.Preset.ToString(),
        config.EnablePlayableEconomyBundle,
        config.EnableQuestEconomyCluster,
        config.EnableTraderEconomyCluster,
        config.EnableFleaEconomyCluster,
        config.EnableLootEconomyCluster,
        config.CustomTraderPurchasePriceMultiplier,
        config.CustomTraderSellPayoutMultiplier,
        config.CustomFleaBasePriceMultiplier,
        config.CustomFleaListingFeeMultiplier,
        config.CustomLooseLootScale,
        config.CustomStaticLootScale,
        config.CustomAuditPolicy.HighItemValueLowStructureWarnMultiple,
        config.CustomAuditPolicy.RestartableHighItemValueWarnMultiple,
        config.CustomAuditPolicy.HighXpLowDepthWarnMultiple,
        config.CustomAuditPolicy.RestartableHighXpWarnMultiple,
        config.CustomAuditPolicy.HighStandingLowDepthWarnMultiple);
}

public sealed record EconomySettingsError(bool Ok, string Error);
