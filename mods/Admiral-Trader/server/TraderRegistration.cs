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
        TraderAssort natalyaSignatureAssort = modHelper.GetJsonDataFromFile<TraderAssort>(modPath, "db/natalya-signature-assort.json");
        Dictionary<string, Dictionary<MongoId, MongoId>> questAssort =
            modHelper.GetJsonDataFromFile<Dictionary<string, Dictionary<MongoId, MongoId>>>(modPath, "db/questassort.json");

        MergeNatalyaSignatureStock(assort, natalyaSignatureAssort);
        ValidateTraderData(traderBase, assort, questAssort);
        ValidateRelationshipStock(modPath, assort, questAssort);

        if (tradersTable.ContainsKey(traderBase.Id))
            throw new InvalidOperationException($"Cannot register Admiral Trader: trader id {traderBase.Id} already exists");
        if (traderConfig.UpdateTime.Any(entry => entry.TraderId == traderBase.Id))
            throw new InvalidOperationException($"Cannot register Admiral Trader: update-time entry {traderBase.Id} already exists");
        if (ragfairConfig.Traders.ContainsKey(traderBase.Id))
            throw new InvalidOperationException($"Cannot register Admiral Trader: ragfair entry {traderBase.Id} already exists");

        UpdateTime updateTime = new()
        {
            TraderId = traderBase.Id,
            Seconds = new MinMax<int>(timeUtil.GetHoursAsSeconds(1), timeUtil.GetHoursAsSeconds(2))
        };

        Trader trader = new()
        {
            Base = traderBase,
            Assort = assort,
            QuestAssort = questAssort,
            Dialogue = []
        };

        bool traderAdded = false;
        bool updateTimeAdded = false;
        bool ragfairAdded = false;
        try
        {
            traderAdded = tradersTable.TryAdd(traderBase.Id, trader);
            if (!traderAdded)
                throw new InvalidOperationException($"Cannot register Admiral Trader: trader id {traderBase.Id} already exists");
            traderConfig.UpdateTime.Add(updateTime);
            updateTimeAdded = true;
            ragfairAdded = ragfairConfig.Traders.TryAdd(traderBase.Id, true);
            if (!ragfairAdded)
                throw new InvalidOperationException($"Cannot register Admiral Trader: ragfair entry {traderBase.Id} already exists");
            imageRouter.AddRoute(traderBase.Avatar!.Replace(".jpg", string.Empty, StringComparison.OrdinalIgnoreCase), avatarPath);
            AddLocales(traderBase);
        }
        catch
        {
            if (ragfairAdded)
                ragfairConfig.Traders.Remove(traderBase.Id);
            if (updateTimeAdded)
                traderConfig.UpdateTime.Remove(updateTime);
            if (traderAdded)
                tradersTable.Remove(traderBase.Id);
            throw;
        }
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

        string[] exactNativeKeys = ["started", "success", "fail"];
        if (questAssort.Count != exactNativeKeys.Length || exactNativeKeys.Any(key => !questAssort.ContainsKey(key)))
            throw new InvalidDataException("questassort.json must contain exactly the native lower-case keys: started, success, fail");
        if (questAssort.Keys.Any(key => key is "Started" or "Success" or "Fail"))
            throw new InvalidDataException("questassort.json contains legacy capitalized state keys that are invalid for exact SPT 4.1.5 runtime validation");
    }

    private static void MergeNatalyaSignatureStock(TraderAssort assort, TraderAssort signatureAssort)
    {
        if (signatureAssort.Items is null || signatureAssort.BarterScheme is null || signatureAssort.LoyalLevelItems is null)
            throw new InvalidDataException("Natalya signature stock is missing a required native collection");

        var signatureItems = signatureAssort.Items!;
        var signatureBarters = signatureAssort.BarterScheme!;
        var signatureLoyalty = signatureAssort.LoyalLevelItems!;
        Item[] roots = signatureItems.Where(item => item.ParentId?.ToString() == "hideout").ToArray();
        if (roots.Length != 4)
            throw new InvalidDataException($"Expected four Natalya signature offers, got {roots.Length}");
        if (roots.Any(root => root.Upd is null || root.Upd.UnlimitedCount is not false || root.Upd.StackObjectsCount is null or <= 0 || root.Upd.BuyRestrictionMax is not 1))
            throw new InvalidDataException("Natalya signature offers must remain finite one-per-reset presets");

        HashSet<MongoId> existingItemIds = assort.Items.Select(item => item.Id).ToHashSet();
        if (signatureItems.Any(item => !existingItemIds.Add(item.Id)))
            throw new InvalidDataException("Natalya signature stock contains an item id already owned by Admiral");
        if (signatureBarters.Keys.Any(assort.BarterScheme.ContainsKey)
            || signatureLoyalty.Keys.Any(assort.LoyalLevelItems.ContainsKey))
            throw new InvalidDataException("Natalya signature stock contains an offer id already owned by Admiral");

        assort.Items.AddRange(signatureItems);
        foreach (var (offerId, scheme) in signatureBarters)
            assort.BarterScheme.Add(offerId, scheme);
        foreach (var (offerId, loyalty) in signatureLoyalty)
            assort.LoyalLevelItems.Add(offerId, loyalty);
    }

    private static void ValidateRelationshipStock(
        string modPath,
        TraderAssort assort,
        Dictionary<string, Dictionary<MongoId, MongoId>> questAssort)
    {
        string path = IOPath.Combine(modPath, "manifests", "relationship-stock.json");
        if (!File.Exists(path))
            throw new FileNotFoundException("M5 Relationship stock manifest is missing", path);

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1
            || !string.Equals(root.GetProperty("stockClass").GetString(), "Relationship", StringComparison.Ordinal)
            || !root.GetProperty("materialization").GetProperty("enabled").GetBoolean())
            throw new InvalidDataException("M5 Relationship stock authority is not materialized");

        JsonElement[] offers = root.GetProperty("offers").EnumerateArray().ToArray();
        if (offers.Length != 3)
            throw new InvalidDataException($"Expected three M5 Relationship offers, got {offers.Length}");

        HashSet<string> milestoneIds = questAssort["success"].Keys.Select(id => id.ToString()).ToHashSet(StringComparer.Ordinal);
        foreach (JsonElement policy in offers)
        {
            string offerId = policy.GetProperty("offerId").GetString()
                ?? throw new InvalidDataException("Relationship offer id is missing");
            string tpl = policy.GetProperty("tpl").GetString()
                ?? throw new InvalidDataException($"Relationship offer {offerId} template is missing");
            int loyaltyLevel = policy.GetProperty("loyaltyLevel").GetInt32();
            int stock = policy.GetProperty("stockPerReset").GetInt32();
            int buyRestriction = policy.GetProperty("buyRestriction").GetInt32();

            if (milestoneIds.Contains(offerId))
                throw new InvalidDataException($"Relationship offer {offerId} cannot be quest gated");

            Item? item = assort.Items.SingleOrDefault(candidate => candidate.Id.ToString() == offerId);
            if (item is null || item.Template.ToString() != tpl || item.Upd is null)
                throw new InvalidDataException($"Relationship offer {offerId} runtime identity drift");
            if (item.Upd.UnlimitedCount is not false || item.Upd.StackObjectsCount != stock || item.Upd.BuyRestrictionMax != buyRestriction)
                throw new InvalidDataException($"Relationship offer {offerId} finite capacity drift");
            if (!assort.LoyalLevelItems.TryGetValue(new MongoId(offerId), out int runtimeLoyalty) || runtimeLoyalty != loyaltyLevel)
                throw new InvalidDataException($"Relationship offer {offerId} loyalty mapping drift");
        }
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
        if (manifest.SchemaVersion != 2)
            throw new InvalidDataException($"Unsupported Admiral Trader runtime manifest schema: {manifest.SchemaVersion}");
        if (!string.Equals(manifest.Product, "Admiral Trader", StringComparison.Ordinal))
            throw new InvalidDataException($"runtime-manifest product mismatch: {manifest.Product}");
        if (!string.Equals(manifest.Version, "0.3.0", StringComparison.Ordinal))
            throw new InvalidDataException($"runtime-manifest version mismatch: {manifest.Version}");
        if (!string.Equals(manifest.SptCompatibility, "~4.1.0", StringComparison.Ordinal))
            throw new InvalidDataException($"runtime-manifest SPT compatibility mismatch: {manifest.SptCompatibility}");
        if (!string.Equals(manifest.TraderId, RuntimeIdentity.TraderId, StringComparison.Ordinal))
            throw new InvalidDataException($"runtime-manifest trader id mismatch: {manifest.TraderId}");
        return manifest;
    }
}

public sealed record RuntimeRegistrationManifest
{
    public int SchemaVersion { get; init; }
    public string? Product { get; init; }
    public string? Version { get; init; }
    public string? TraderId { get; init; }
    public string? SptCompatibility { get; init; }
    public bool RegistrationEnabled { get; init; }
}
