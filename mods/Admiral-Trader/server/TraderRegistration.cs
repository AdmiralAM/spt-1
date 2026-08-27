using System.Reflection;
using System.Text.Json;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Utils;
using IOPath = System.IO.Path;

namespace AdmiralTrader.Server;

[Injectable(TypePriority = OnLoadOrder.Preload + 2), UsedImplicitly]
public sealed class AdmiralTraderRegistration(
    ModHelper modHelper,
    ImageRouter imageRouter,
    TraderConfig traderConfig,
    RagfairConfig ragfairConfig,
    TimeUtil timeUtil,
    TradersTable tradersTable,
    LocaleTable localesTable,
    ISptLogger<AdmiralTraderRegistration> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        RuntimeRegistrationManifest runtimeManifest = LoadRuntimeManifest(modPath);
        if (!runtimeManifest.RegistrationEnabled)
        {
            logger.Info("Admiral Trader native registration gate is disabled; data contract validated but trader is not published");
            return Task.CompletedTask;
        }

        RegisterTrader(modPath);
        return Task.CompletedTask;
    }

    private void RegisterTrader(string modPath)
    {
        string avatarPath = IOPath.Combine(modPath, "assets", $"{RuntimeIdentity.TraderId}.jpg");
        if (!File.Exists(avatarPath))
            throw new FileNotFoundException("Admiral Trader registration is enabled but the approved trader portrait is missing", avatarPath);

        TraderBase traderBase = modHelper.GetJsonDataFromFile<TraderBase>(modPath, "db/base.json");
        TraderAssort assort = modHelper.GetJsonDataFromFile<TraderAssort>(modPath, "db/assort.json");
        Dictionary<string, Dictionary<MongoId, MongoId>> questAssort =
            modHelper.GetJsonDataFromFile<Dictionary<string, Dictionary<MongoId, MongoId>>>(modPath, "db/questassort.json");

        ValidateTraderData(traderBase, assort, questAssort);

        imageRouter.AddRoute(traderBase.Avatar!.Replace(".jpg", string.Empty, StringComparison.OrdinalIgnoreCase), avatarPath);
        traderConfig.UpdateTime.Add(new UpdateTime
        {
            TraderId = traderBase.Id,
            Seconds = new MinMax<int>(timeUtil.GetHoursAsSeconds(1), timeUtil.GetHoursAsSeconds(2))
        });
        ragfairConfig.Traders.TryAdd(traderBase.Id, true);

        Trader trader = new()
        {
            Base = traderBase,
            Assort = assort,
            QuestAssort = questAssort,
            Dialogue = []
        };

        if (!tradersTable.TryAdd(traderBase.Id, trader))
            throw new InvalidOperationException($"Cannot register Admiral Trader: trader id {traderBase.Id} already exists");

        AddLocales(traderBase);
        logger.Success($"Admiral Trader registered with id {traderBase.Id} and {assort.Items.Count} assort item records");
    }

    private static void ValidateTraderData(
        TraderBase traderBase,
        TraderAssort assort,
        Dictionary<string, Dictionary<MongoId, MongoId>> questAssort)
    {
        if (traderBase.Id.ToString() != RuntimeIdentity.TraderId)
            throw new InvalidDataException($"base.json trader id mismatch: {traderBase.Id}");
        if (!string.Equals(traderBase.Name, RuntimeIdentity.TraderName, StringComparison.Ordinal))
            throw new InvalidDataException($"base.json trader name mismatch: {traderBase.Name}");
        if (string.IsNullOrWhiteSpace(traderBase.Avatar))
            throw new InvalidDataException("base.json trader avatar route is missing");
        if (assort.Items is null || assort.BarterScheme is null || assort.LoyalLevelItems is null)
            throw new InvalidDataException("assort.json is missing a required native collection");
        foreach (string requiredKey in new[] { "Started", "Success", "Fail" })
            if (!questAssort.ContainsKey(requiredKey))
                throw new InvalidDataException($"questassort.json missing required key: {requiredKey}");
    }

    private void AddLocales(TraderBase traderBase)
    {
        foreach (var (localeCode, localeKvP) in localesTable.Global)
        {
            localeKvP.AddTransformer(lazyLoadedLocaleData =>
            {
                if (lazyLoadedLocaleData is null)
                    return lazyLoadedLocaleData;

                bool isRussian = localeCode.Equals("ru", StringComparison.OrdinalIgnoreCase);
                string localizedName = isRussian ? RuntimeIdentity.TraderNameRu : RuntimeIdentity.TraderName;
                string localizedLocation = isRussian ? "Засекречено" : "Classified";

                lazyLoadedLocaleData[$"{traderBase.Id} FullName"] = localizedName;
                lazyLoadedLocaleData[$"{traderBase.Id} FirstName"] = localizedName;
                lazyLoadedLocaleData[$"{traderBase.Id} Nickname"] = localizedName;
                lazyLoadedLocaleData[$"{traderBase.Id} Location"] = localizedLocation;
                lazyLoadedLocaleData[$"{traderBase.Id} Description"] = localizedName;
                return lazyLoadedLocaleData;
            });
        }
    }

    internal static RuntimeRegistrationManifest LoadRuntimeManifest(string modPath)
    {
        string path = IOPath.Combine(modPath, "manifests", "runtime-manifest.json");
        if (!File.Exists(path))
            throw new FileNotFoundException("Admiral Trader runtime manifest is missing", path);

        RuntimeRegistrationManifest? manifest = JsonSerializer.Deserialize<RuntimeRegistrationManifest>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (manifest is null)
            throw new InvalidDataException("Admiral Trader runtime manifest could not be parsed");
        if (!string.Equals(manifest.TraderId, RuntimeIdentity.TraderId, StringComparison.Ordinal))
            throw new InvalidDataException($"runtime-manifest trader id mismatch: {manifest.TraderId}");
        return manifest;
    }
}

public sealed record RuntimeRegistrationManifest
{
    public int SchemaVersion { get; init; }
    public string? Product { get; init; }
    public string? TraderId { get; init; }
    public bool RegistrationEnabled { get; init; }
}
