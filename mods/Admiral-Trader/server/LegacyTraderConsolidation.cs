using System.Reflection;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Utils.Json;
using IOPath = System.IO.Path;

namespace AdmiralTrader.Server;

/// <summary>
/// Keeps the distributed Painter and Artem identities valid during profile load,
/// then folds their content and profile state into Admiral before the client can
/// request trader settings. The legacy records are removed only after every
/// loaded profile has been migrated and saved successfully.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.PostLoad - 1), UsedImplicitly]
public sealed class LegacyTraderConsolidation(
    TradersTable tradersTable,
    TemplateTable templateTable,
    TraderConfig traderConfig,
    RagfairConfig ragfairConfig,
    SaveServer saveServer,
    ModHelper modHelper,
    ImageRouter imageRouter,
    LocaleTable localesTable,
    MailSendService mailSendService,
    ISptLogger<LegacyTraderConsolidation> logger) : IOnLoad
{
    public const string PainterTraderId = "668aaff35fd574b6dcc4a686";
    public const string ArtemTraderId = "66bf757f27d0b097db0acea5";
    public static readonly string[] LegacyTraderIds = [PainterTraderId, ArtemTraderId];
    private static readonly MongoId AdmiralId = new(RuntimeIdentity.TraderId);
    private const string MigrationKey = "admiral-trader-legacy-consolidation-v1";
    private const string PainterTapedUpQuestId = "668aacd1dee3de3ce276fdef";
    private const string PainterTapedUpRepairKey = "admiral-trader-painter-taped-up-reward-repair-v1";
    private const int PainterTapedUpRoubles = 21000;
    private const double PainterTapedUpStanding = 0.02;

    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!tradersTable.TryGetValue(AdmiralId, out Trader? admiral))
            throw new InvalidOperationException("Admiral must exist before legacy trader consolidation");

        string modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        ExternalContentCounts direct = AttachContentOnlyProviders(modPath, admiral);

        int offerRoots = direct.OfferRoots;
        int itemRows = direct.ItemRows;
        int questUnlocks = direct.QuestUnlocks;
        int suits = direct.Suits;
        int quests = direct.Quests;
        List<MongoId> presentLegacyIds = [];

        foreach (string legacyIdText in LegacyTraderIds)
        {
            MongoId legacyId = new(legacyIdText);
            if (!tradersTable.TryGetValue(legacyId, out Trader? legacy))
                continue;

            int legacyRows = legacy.Assort?.Items?.Count ?? 0;
            int legacyQuests = templateTable.Quests.Values.Count(quest => quest.TraderId == legacyId);
            bool hasProviderContent = legacyRows > 0 || legacyQuests > 0 || (legacy.Suits?.Count ?? 0) > 0;
            if (!hasProviderContent)
                continue;

            presentLegacyIds.Add(legacyId);
            int skippedOffers = PruneUnavailableOfferTrees(legacy.Assort);
            if (LegacyQuestsReferenceMissingTemplates(legacyId))
            {
                RemoveLegacyQuests(legacyId);
                legacyQuests = 0;
                logger.Warning($"Admiral skipped the quest graph for legacy provider {legacyIdText}: a required item template is unavailable");
            }
            if (skippedOffers > 0)
                logger.Warning($"Admiral omitted {skippedOffers} invalid offers from legacy provider {legacyIdText}; all valid content remains available");
            ValidateExternalAssort(legacy.Assort!, legacyIdText);
            ValidateExternalQuestGraph(legacyId);
            offerRoots += legacy.Assort!.Items.Count(item => item.ParentId?.ToString() == "hideout");
            itemRows += MergeAssort(admiral.Assort, legacy.Assort, legacyIdText);
            questUnlocks += MergeQuestAssort(admiral.QuestAssort, legacy.QuestAssort, legacyIdText);
            suits += MergeSuits(admiral, legacy);
            quests += RemapQuests(legacyId);
        }

        foreach (var (profileId, profile) in saveServer.GetProfiles())
        {
            bool changed = MigrateProfile(profile, LegacyTraderIds.Select(id => new MongoId(id)).ToArray());
            changed |= RepairFailedPainterTapedUpReward(profileId, profile);
            if (!changed)
                continue;
            await saveServer.SaveProfileAsync(profileId, cancellationToken);
        }

        if (presentLegacyIds.Count == 0)
        {
            RemoveEmptyCompatibilityShells();
            return;
        }

        foreach (MongoId legacyId in LegacyTraderIds.Select(id => new MongoId(id)))
        {
            tradersTable.Remove(legacyId);
            traderConfig.UpdateTime.RemoveAll(update => update.TraderId == legacyId);
            ragfairConfig.Traders.Remove(legacyId);
        }
        RemoveEmptyCompatibilityShells();

        logger.Success($"Admiral consolidated {presentLegacyIds.Count} legacy content providers: {offerRoots} offers/{itemRows} item rows, {quests} quests, {questUnlocks} quest unlocks and {suits} suits; legacy trader tabs removed");
    }

    private ExternalContentCounts AttachContentOnlyProviders(string modPath, Trader admiral)
    {
        ExternalContentCounts result = new();
        DirectoryInfo? modsDirectory = Directory.GetParent(modPath.TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar));
        if (modsDirectory is null || !modsDirectory.Exists)
            return result;

        bool painterProviderLoaded = templateTable.Quests.Values.Any(quest => quest.TraderId.ToString() == PainterTraderId);
        tradersTable.TryGetValue(new MongoId(PainterTraderId), out Trader? painterTrader);
        bool painterTraderHasStock = (painterTrader?.Assort?.Items?.Count ?? 0) > 0;

        DirectoryInfo? tgc = modsDirectory.EnumerateDirectories()
            .FirstOrDefault(candidate => File.Exists(IOPath.Combine(candidate.FullName, "db", "CustomItems", "modTGC_items.json"))
                && File.Exists(IOPath.Combine(candidate.FullName, "db", "traders", PainterTraderId, "assort.json")));
        if (tgc is not null)
        {
            TraderAssort assort = LoadExternal<TraderAssort>(modPath, IOPath.Combine(tgc.FullName, "db", "traders", PainterTraderId, "assort.json"));
            List<Suit> externalSuits = LoadExternal<List<Suit>>(modPath, IOPath.Combine(tgc.FullName, "db", "traders", PainterTraderId, "suits.json"));
            HashSet<MongoId> legacyPainterRoots = painterTrader?.Assort?.Items
                .Where(item => item.ParentId?.ToString() == "hideout").Select(item => item.Id).ToHashSet() ?? [];
            bool tgcAlreadyOwnedByLegacyPainter = assort.Items
                .Where(item => item.ParentId?.ToString() == "hideout").Any(item => legacyPainterRoots.Contains(item.Id));
            MongoId[] missing = assort.Items.Select(item => item.Template).Where(template => !templateTable.Items.ContainsKey(template)).Distinct().ToArray();
            if (missing.Length == 0 && !tgcAlreadyOwnedByLegacyPainter)
            {
                int roots = assort.Items.Count(item => item.ParentId?.ToString() == "hideout");
                if (roots != 114 || assort.Items.Count != 236 || externalSuits.Count != 4)
                    throw new InvalidDataException($"TGC 3.0.0 content shape drift: roots={roots}, rows={assort.Items.Count}, suits={externalSuits.Count}");
                ApplyPurchaseLimit(assort, "672e2e75a8f42643cd43c4b8", 1, "TGC M4A1 preset");
                ValidateExternalAssort(assort, "TGC content-only provider");
                result = result with
                {
                    OfferRoots = roots,
                    ItemRows = MergeAssort(admiral.Assort, assort, "TGC content-only provider"),
                    Suits = MergeSuits(admiral, externalSuits)
                };
            }
            else if (missing.Length > 0)
                logger.Warning($"Admiral skipped TGC storefront: {missing.Length} TGC templates are unavailable; core Admiral remains active");
        }

        string packagedPainter = IOPath.Combine(modPath, "external", "painter");
        DirectoryInfo? painter = Directory.Exists(packagedPainter) && FindPainterQuestFile(packagedPainter) is not null
            ? new DirectoryInfo(packagedPainter)
            : modsDirectory.EnumerateDirectories()
            .FirstOrDefault(candidate => FindPainterQuestFile(candidate.FullName) is not null);
        if (painter is not null)
        {
            if (!painterProviderLoaded && !painterTraderHasStock)
                result += AttachPainterContent(modPath, painter.FullName, admiral);
            else
                result = result with
                {
                    QuestUnlocks = MergeQuestAssort(admiral.QuestAssort, PainterQuestUnlocks(), "packaged Painter unlocks")
                };
        }
        return result;
    }

    private ExternalContentCounts AttachPainterContent(string modPath, string painterPath, Trader admiral)
    {
        string questFile = FindPainterQuestFile(painterPath)!;
        string assortFile = IOPath.Combine(painterPath, "db", "assort.json");
        if (!File.Exists(assortFile))
            return new();
        TraderAssort assort = LoadExternal<TraderAssort>(modPath, assortFile);
        Dictionary<MongoId, Quest> quests = LoadExternal<Dictionary<MongoId, Quest>>(modPath, questFile);
        MongoId[] missing = assort.Items.Select(item => item.Template).Where(template => !templateTable.Items.ContainsKey(template)).Distinct().ToArray();
        if (missing.Length > 0)
        {
            logger.Warning($"Admiral skipped Painter content: {missing.Length} Painter templates are unavailable; core Admiral remains active");
            return new();
        }
        if (quests.Count != 12 || assort.Items.Count(item => item.ParentId?.ToString() == "hideout") != 7)
            throw new InvalidDataException("Painter 3.0.0 content shape drift");
        foreach (var (questId, quest) in quests)
            if (!templateTable.Quests.TryAdd(questId, quest))
                throw new InvalidDataException($"Painter quest id collides with an existing quest: {questId}");
        ValidateExternalAssort(assort, "Painter content-only provider");
        ValidateExternalQuestGraph(new MongoId(PainterTraderId));
        int remapped = RemapQuests(new MongoId(PainterTraderId));
        RegisterPainterLocales(modPath, painterPath);
        RegisterPainterImages(painterPath);
        int unlocks = MergeQuestAssort(admiral.QuestAssort, PainterQuestUnlocks(), "Painter content-only provider");
        return new(7, MergeAssort(admiral.Assort, assort, "Painter content-only provider"), unlocks, 0, remapped);
    }

    private static Dictionary<string, Dictionary<MongoId, MongoId>> PainterQuestUnlocks() => new()
    {
        ["started"] = [],
        ["success"] = new()
        {
            [new MongoId("672e2804a0529208b4e10e18")] = new MongoId("668aad3c3ff8f5b258e3a65b"),
            [new MongoId("672e284a363b798192b802af")] = new MongoId("668c18eb12542b3c3ff6e20f"),
            [new MongoId("672e289bb4096716fcb918a7")] = new MongoId("668c18eb12542b3c3ff6e20f")
        },
        ["fail"] = []
    };

    private T LoadExternal<T>(string modPath, string absolutePath) =>
        modHelper.GetJsonDataFromFile<T>(modPath, IOPath.GetRelativePath(modPath, absolutePath).Replace('\\', '/'));

    private static string? FindPainterQuestFile(string root)
    {
        string current = IOPath.Combine(root, "db", "CustomQuests", PainterTraderId, "Quests", "painter.json");
        if (File.Exists(current)) return current;
        string legacy = IOPath.Combine(root, "db", "quests", "painter.json");
        return File.Exists(legacy) ? legacy : null;
    }

    private void RegisterPainterLocales(string modPath, string painterPath)
    {
        string localeRoot = IOPath.Combine(painterPath, "db", "CustomQuests", PainterTraderId, "Locales");
        if (!Directory.Exists(localeRoot)) localeRoot = IOPath.Combine(painterPath, "db", "locales");
        string? englishPath = FindLocaleFile(localeRoot, "en");
        if (englishPath is null) throw new InvalidDataException("Painter English quest locale is missing");
        Dictionary<string, string> english = LoadExternal<Dictionary<string, string>>(modPath, englishPath);
        foreach (var (localeCode, locale) in localesTable.Global)
        {
            string? localizedPath = FindLocaleFile(localeRoot, localeCode);
            Dictionary<string, string> source = localizedPath is null
                ? english
                : LoadExternal<Dictionary<string, string>>(modPath, localizedPath);
            locale.AddTransformer(data =>
            {
                if (data is null) return data;
                foreach (var (key, value) in english) data[key] = value;
                foreach (var (key, value) in source) data[key] = value;
                return data;
            });
        }
    }

    private static string? FindLocaleFile(string root, string locale)
    {
        string direct = IOPath.Combine(root, $"{locale}.json");
        if (File.Exists(direct)) return direct;
        string directory = IOPath.Combine(root, locale);
        return Directory.Exists(directory) ? Directory.GetFiles(directory, "*.json").OrderBy(path => path, StringComparer.Ordinal).FirstOrDefault() : null;
    }

    private void RegisterPainterImages(string painterPath)
    {
        string imageRoot = IOPath.Combine(painterPath, "db", "CustomQuests", PainterTraderId, "Images");
        if (!Directory.Exists(imageRoot)) imageRoot = IOPath.Combine(painterPath, "res", "quests");
        if (!Directory.Exists(imageRoot)) throw new InvalidDataException("Painter quest images are missing");
        foreach (string image in Directory.GetFiles(imageRoot).Where(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)))
            imageRouter.AddRoute($"/files/quest/icon/{IOPath.GetFileNameWithoutExtension(image)}", image);
    }

    private void RemoveLegacyQuests(MongoId legacyId)
    {
        foreach (MongoId questId in templateTable.Quests.Where(row => row.Value.TraderId == legacyId).Select(row => row.Key).ToArray())
            templateTable.Quests.Remove(questId);
    }

    private int PruneUnavailableOfferTrees(TraderAssort? assort)
    {
        if (assort?.Items is null || assort.Items.Count == 0) return 0;
        Dictionary<MongoId, Item> byId = assort.Items.ToDictionary(item => item.Id);
        HashSet<MongoId> invalidRoots = [];
        foreach (Item item in assort.Items.Where(item => !templateTable.Items.ContainsKey(item.Template)))
        {
            Item cursor = item;
            HashSet<MongoId> visited = [];
            for (int depth = 0; depth < assort.Items.Count; depth++)
            {
                if (cursor.ParentId is null || cursor.ParentId.ToString() == "hideout" || !visited.Add(cursor.Id)
                    || !byId.TryGetValue(new MongoId(cursor.ParentId), out Item? parent))
                    break;
                cursor = parent;
            }
            invalidRoots.Add(cursor.Id);
        }
        if (invalidRoots.Count == 0) return 0;
        HashSet<MongoId> removed = invalidRoots.ToHashSet();
        List<MongoId> queue = invalidRoots.ToList();
        for (int index = 0; index < queue.Count; index++)
        {
            MongoId parentId = queue[index];
            foreach (Item item in assort.Items)
                if (item.ParentId is not null && item.ParentId.ToString() != "hideout"
                    && new MongoId(item.ParentId) == parentId && removed.Add(item.Id))
                    queue.Add(item.Id);
        }
        assort.Items.RemoveAll(item => removed.Contains(item.Id));
        foreach (MongoId root in invalidRoots)
        {
            assort.BarterScheme.Remove(root);
            assort.LoyalLevelItems.Remove(root);
        }
        return invalidRoots.Count;
    }

    private bool LegacyQuestsReferenceMissingTemplates(MongoId legacyId)
    {
        foreach (Quest quest in templateTable.Quests.Values.Where(candidate => candidate.TraderId == legacyId))
        {
            foreach (QuestCondition condition in EnumerateConditions(quest))
                if (condition.ConditionType is "FindItem" or "HandoverItem"
                    && EnumerateTargets(condition).Any(target => target.Length == 24 && !templateTable.Items.ContainsKey(new MongoId(target))))
                    return true;
            foreach (Reward reward in quest.Rewards?.Values.SelectMany(rows => rows) ?? [])
                if ((reward.Items ?? []).Any(item => !templateTable.Items.ContainsKey(item.Template)))
                    return true;
        }
        return false;
    }

    private static IEnumerable<string> EnumerateTargets(QuestCondition condition)
    {
        if (condition.Target is null) yield break;
        if (condition.Target.IsList)
            foreach (string target in condition.Target.List ?? []) yield return target;
        else if (condition.Target.IsItem && condition.Target.Item is not null)
            yield return condition.Target.Item;
    }

    private void RemoveEmptyCompatibilityShells()
    {
        foreach (string legacyIdText in LegacyTraderIds)
        {
            MongoId legacyId = new(legacyIdText);
            if (tradersTable.TryGetValue(legacyId, out Trader? trader)
                && (trader.Assort?.Items?.Count ?? 0) == 0
                && templateTable.Quests.Values.All(quest => quest.TraderId != legacyId))
                tradersTable.Remove(legacyId);
        }
    }

    private static int MergeAssort(TraderAssort target, TraderAssort source, string sourceName)
    {
        HashSet<MongoId> existingItems = target.Items.Select(item => item.Id).ToHashSet();
        int added = 0;
        foreach (Item item in source.Items)
        {
            if (!existingItems.Add(item.Id))
                throw new InvalidDataException($"Legacy trader {sourceName} item id collides with Admiral: {item.Id}");
            target.Items.Add(item);
            added++;
        }
        foreach (var (offerId, scheme) in source.BarterScheme)
        {
            if (!target.BarterScheme.TryAdd(offerId, scheme))
                throw new InvalidDataException($"Legacy trader {sourceName} barter id collides with Admiral: {offerId}");
        }
        foreach (var (offerId, loyalty) in source.LoyalLevelItems)
        {
            if (!target.LoyalLevelItems.TryAdd(offerId, Math.Clamp(loyalty, 1, 4)))
                throw new InvalidDataException($"Legacy trader {sourceName} loyalty id collides with Admiral: {offerId}");
        }
        return added;
    }

    private static void ValidateExternalAssort(TraderAssort assort, string sourceName)
    {
        HashSet<MongoId> ids = assort.Items.Select(item => item.Id).ToHashSet();
        MongoId[] roots = assort.Items.Where(item => item.ParentId?.ToString() == "hideout").Select(item => item.Id).ToArray();
        foreach (Item item in assort.Items.Where(item => item.ParentId is not null && item.ParentId.ToString() != "hideout"))
            if (!ids.Contains(new MongoId(item.ParentId)))
                throw new InvalidDataException($"{sourceName} assort item {item.Id} has missing parent {item.ParentId}");
        foreach (MongoId root in roots)
            if (!assort.BarterScheme.ContainsKey(root) || !assort.LoyalLevelItems.ContainsKey(root))
                throw new InvalidDataException($"{sourceName} offer {root} is missing barter or loyalty data");
        if (assort.BarterScheme.Keys.Any(key => !roots.Contains(key)) || assort.LoyalLevelItems.Keys.Any(key => !roots.Contains(key)))
            throw new InvalidDataException($"{sourceName} has orphaned barter or loyalty keys");
    }

    private static void ApplyPurchaseLimit(TraderAssort assort, string offerIdText, int limit, string label)
    {
        MongoId offerId = new(offerIdText);
        Item? root = assort.Items.SingleOrDefault(item => item.Id == offerId && item.ParentId?.ToString() == "hideout");
        if (root?.Upd is null)
            throw new InvalidDataException($"Cannot apply Admiral purchase limit to missing {label} offer {offerIdText}");
        root.Upd.BuyRestrictionMax = limit;
        root.Upd.BuyRestrictionCurrent = 0;
    }

    private void ValidateExternalQuestGraph(MongoId legacyId)
    {
        foreach (Quest quest in templateTable.Quests.Values.Where(candidate => candidate.TraderId == legacyId))
            foreach (QuestCondition condition in quest.Conditions.AvailableForStart ?? [])
                if (condition.ConditionType == "Quest")
                    foreach (string target in EnumerateTargets(condition).Where(target => target.Length == 24))
                        if (!templateTable.Quests.ContainsKey(new MongoId(target)))
                            throw new InvalidDataException($"External quest {quest.Id} references missing prerequisite {target}");
    }

    private static int MergeQuestAssort(
        Dictionary<string, Dictionary<MongoId, MongoId>> target,
        Dictionary<string, Dictionary<MongoId, MongoId>>? source,
        string sourceName)
    {
        if (source is null) return 0;
        int added = 0;
        foreach (var (rawState, mappings) in source)
        {
            string state = rawState.ToLowerInvariant();
            if (!target.TryGetValue(state, out Dictionary<MongoId, MongoId>? destination))
                throw new InvalidDataException($"Legacy trader {sourceName} has unsupported quest-assort state {rawState}");
            foreach (var (offerId, questId) in mappings)
            {
                if (!destination.TryAdd(offerId, questId))
                    throw new InvalidDataException($"Legacy trader {sourceName} quest unlock collides with Admiral: {offerId}");
                added++;
            }
        }
        return added;
    }

    private static int MergeSuits(Trader admiral, Trader legacy)
    {
        return MergeSuits(admiral, legacy.Suits);
    }

    private static int MergeSuits(Trader admiral, List<Suit>? source)
    {
        if (source is null || source.Count == 0) return 0;
        admiral.Suits ??= [];
        HashSet<MongoId> existing = admiral.Suits.Select(suit => suit.SuiteId).ToHashSet();
        int added = 0;
        foreach (Suit suit in source)
            if (existing.Add(suit.SuiteId))
            {
                admiral.Suits.Add(suit);
                added++;
            }
        return added;
    }

    private int RemapQuests(MongoId legacyId)
    {
        int count = 0;
        foreach (Quest quest in templateTable.Quests.Values.Where(candidate => candidate.TraderId == legacyId))
        {
            quest.TraderId = AdmiralId;
            foreach (QuestCondition condition in EnumerateConditions(quest))
                if (condition.TraderId == legacyId.ToString())
                    condition.TraderId = RuntimeIdentity.TraderId;
            foreach (Reward reward in quest.Rewards?.Values.SelectMany(rows => rows) ?? [])
            {
                if (reward.TraderId?.String == legacyId.ToString())
                    reward.TraderId = new StringOrInt(RuntimeIdentity.TraderId, null);
                if (reward.Target == legacyId.ToString())
                    reward.Target = RuntimeIdentity.TraderId;
            }
            count++;
        }
        return count;
    }

    private bool RepairFailedPainterTapedUpReward(MongoId profileId, SptProfile profile)
    {
        bool repaired = ApplyPainterTapedUpRewardRepair(
            profile,
            items => mailSendService.SendDirectNpcMessageToPlayer(
                profileId,
                RuntimeIdentity.TraderId,
                MessageType.QuestSuccess,
                "Компенсация за задание «Связано скотчем»: денежная награда не была выдана из-за ошибки переноса Painter.",
                items),
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        if (repaired)
            logger.Success($"Restored missing Painter quest reward for profile {profileId}: {PainterTapedUpRoubles} RUB by Admiral mail and +{PainterTapedUpStanding:0.00} standing; XP was not repeated");
        return repaired;
    }

    public static bool ApplyPainterTapedUpRewardRepair(SptProfile profile, Action<List<Item>> deliver, long repairTimestamp)
    {
        profile.SptData ??= new Spt();
        profile.SptData.Migrations ??= [];
        if (profile.SptData.Migrations.ContainsKey(PainterTapedUpRepairKey)
            || !profile.SptData.Migrations.TryGetValue(MigrationKey, out long consolidationTime))
            return false;

        QuestStatus? quest = profile.CharacterData?.PmcData?.Quests?
            .FirstOrDefault(row => row.QId.ToString() == PainterTapedUpQuestId && row.Status == QuestStatusEnum.Success);
        if (quest is null
            || !quest.StatusTimers.TryGetValue(QuestStatusEnum.Success, out double completionTime)
            || completionTime < consolidationTime)
            return false;

        Dictionary<MongoId, TraderInfo>? traders = profile.CharacterData?.PmcData?.TradersInfo;
        if (traders is null || !traders.TryGetValue(AdmiralId, out TraderInfo? admiral))
            throw new InvalidDataException($"Cannot repair Painter quest {PainterTapedUpQuestId}: Admiral trader state is missing");

        double previousStanding = admiral.Standing ?? 0;
        try
        {
            deliver([new Item
            {
                Id = new MongoId(),
                Template = new MongoId("5449016a4bdc2d6f028b456f"),
                Upd = new Upd { StackObjectsCount = PainterTapedUpRoubles }
            }]);
            admiral.Standing = previousStanding + PainterTapedUpStanding;
            profile.SptData.Migrations[PainterTapedUpRepairKey] = repairTimestamp;
            return true;
        }
        catch (Exception exception)
        {
            admiral.Standing = previousStanding;
            profile.SptData.Migrations.Remove(PainterTapedUpRepairKey);
            throw new InvalidOperationException($"Failed to repair Painter quest {PainterTapedUpQuestId}; no repair marker was saved", exception);
        }
    }

    private static IEnumerable<QuestCondition> EnumerateConditions(Quest quest)
    {
        QuestConditionTypes groups = quest.Conditions;
        foreach (List<QuestCondition>? rows in new[] { groups.Started, groups.AvailableForFinish, groups.AvailableForStart, groups.Success, groups.Fail })
            if (rows is not null)
                foreach (QuestCondition row in rows)
                    yield return row;
    }

    public static bool MigrateProfile(SptProfile profile, IReadOnlyCollection<MongoId> legacyIds)
    {
        profile.SptData ??= new Spt();
        profile.SptData.Migrations ??= [];
        if (profile.SptData.Migrations.ContainsKey(MigrationKey)) return false;
        if (!HasLegacyProfileState(profile, legacyIds)) return false;

        foreach (MongoId legacyId in legacyIds)
        {
            MergeTraderInfo(profile.CharacterData?.PmcData?.TradersInfo, legacyId);
            MergeTraderInfo(profile.CharacterData?.ScavData?.TradersInfo, legacyId);
            MergePurchases(profile, legacyId);
            MergeDialogue(profile, legacyId);
            foreach (SPTarkov.Server.Core.Models.Eft.Profile.Insurance insurance in profile.InsuranceList ?? [])
                if (insurance.TraderId == legacyId)
                    insurance.TraderId = AdmiralId;
            foreach (PmcDataRepeatableQuest repeatable in profile.CharacterData?.PmcData?.RepeatableQuests ?? [])
                foreach (RepeatableQuest quest in repeatable.ActiveQuests ?? [])
                    if (quest.TraderId == legacyId)
                        quest.TraderId = AdmiralId;
        }
        profile.SptData.Migrations[MigrationKey] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return true;
    }

    private static bool HasLegacyProfileState(SptProfile profile, IReadOnlyCollection<MongoId> legacyIds)
    {
        HashSet<MongoId> ids = legacyIds.ToHashSet();
        if (profile.CharacterData?.PmcData?.TradersInfo?.Keys.Any(ids.Contains) is true
            || profile.CharacterData?.ScavData?.TradersInfo?.Keys.Any(ids.Contains) is true
            || profile.TraderPurchases?.Keys.Any(ids.Contains) is true
            || profile.DialogueRecords?.Keys.Any(ids.Contains) is true
            || (profile.InsuranceList ?? []).Any(row => ids.Contains(row.TraderId)))
            return true;
        return (profile.CharacterData?.PmcData?.RepeatableQuests ?? [])
            .SelectMany(group => group.ActiveQuests ?? [])
            .Any(quest => ids.Contains(quest.TraderId));
    }

    private static void MergeTraderInfo(Dictionary<MongoId, TraderInfo>? traders, MongoId legacyId)
    {
        if (traders is null || !traders.Remove(legacyId, out TraderInfo? legacy)) return;
        if (!traders.TryGetValue(AdmiralId, out TraderInfo? admiral))
        {
            legacy.Disabled = false;
            traders[AdmiralId] = legacy;
            return;
        }
        admiral.Standing = Math.Max(admiral.Standing ?? 0, legacy.Standing ?? 0);
        admiral.SalesSum = Math.Max(admiral.SalesSum ?? 0, legacy.SalesSum ?? 0);
        admiral.LoyaltyLevel = Math.Max(admiral.LoyaltyLevel ?? 1, legacy.LoyaltyLevel ?? 1);
        admiral.Unlocked = (admiral.Unlocked ?? false) || (legacy.Unlocked ?? false);
        admiral.Disabled = false;
    }

    private static void MergePurchases(SptProfile profile, MongoId legacyId)
    {
        if (profile.TraderPurchases is null || !profile.TraderPurchases.Remove(legacyId, out Dictionary<MongoId, TraderPurchaseData>? legacy) || legacy is null)
            return;
        profile.TraderPurchases.TryAdd(AdmiralId, []);
        Dictionary<MongoId, TraderPurchaseData> target = profile.TraderPurchases[AdmiralId]!;
        foreach (var (offerId, purchase) in legacy)
            if (!target.TryAdd(offerId, purchase))
            {
                TraderPurchaseData current = target[offerId];
                current.PurchaseCount = Math.Max(current.PurchaseCount ?? 0, purchase.PurchaseCount ?? 0);
                current.PurchaseTimestamp = Math.Max(current.PurchaseTimestamp ?? 0, purchase.PurchaseTimestamp ?? 0);
            }
    }

    private static void MergeDialogue(SptProfile profile, MongoId legacyId)
    {
        if (profile.DialogueRecords is null || !profile.DialogueRecords.Remove(legacyId, out Dialogue? legacy)) return;
        legacy.Id = AdmiralId;
        foreach (UserDialogInfo user in legacy.Users ?? [])
            if (user.Id == legacyId) user.Id = AdmiralId;
        foreach (Message message in legacy.Messages ?? [])
            if (message.UserId == legacyId) message.UserId = AdmiralId;

        if (!profile.DialogueRecords.TryGetValue(AdmiralId, out Dialogue? admiral))
        {
            profile.DialogueRecords[AdmiralId] = legacy;
            return;
        }
        admiral.Messages ??= [];
        HashSet<MongoId> messageIds = admiral.Messages.Select(message => message.Id).ToHashSet();
        admiral.Messages.AddRange((legacy.Messages ?? []).Where(message => messageIds.Add(message.Id)));
        admiral.AttachmentsNew = (admiral.AttachmentsNew ?? 0) + (legacy.AttachmentsNew ?? 0);
        admiral.New = (admiral.New ?? 0) + (legacy.New ?? 0);
    }
}

public readonly record struct ExternalContentCounts(int OfferRoots = 0, int ItemRows = 0, int QuestUnlocks = 0, int Suits = 0, int Quests = 0)
{
    public static ExternalContentCounts operator +(ExternalContentCounts left, ExternalContentCounts right) =>
        new(left.OfferRoots + right.OfferRoots, left.ItemRows + right.ItemRows, left.QuestUnlocks + right.QuestUnlocks, left.Suits + right.Suits, left.Quests + right.Quests);
}
