using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils.Json;

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
    ISptLogger<LegacyTraderConsolidation> logger) : IOnLoad
{
    public const string PainterTraderId = "668aaff35fd574b6dcc4a686";
    public const string ArtemTraderId = "66bf757f27d0b097db0acea5";
    public static readonly string[] LegacyTraderIds = [PainterTraderId, ArtemTraderId];
    private static readonly MongoId AdmiralId = new(RuntimeIdentity.TraderId);
    private const string MigrationKey = "admiral-trader-legacy-consolidation-v1";

    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!tradersTable.TryGetValue(AdmiralId, out Trader? admiral))
            throw new InvalidOperationException("Admiral must exist before legacy trader consolidation");

        int offerRoots = 0;
        int itemRows = 0;
        int questUnlocks = 0;
        int suits = 0;
        int quests = 0;
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
            offerRoots += legacy.Assort!.Items.Count(item => item.ParentId?.ToString() == "hideout");
            itemRows += MergeAssort(admiral.Assort, legacy.Assort, legacyIdText);
            questUnlocks += MergeQuestAssort(admiral.QuestAssort, legacy.QuestAssort, legacyIdText);
            suits += MergeSuits(admiral, legacy);
            quests += RemapQuests(legacyId);
        }

        foreach (var (profileId, profile) in saveServer.GetProfiles())
        {
            if (!MigrateProfile(profile, LegacyTraderIds.Select(id => new MongoId(id)).ToArray()))
                continue;
            await saveServer.SaveProfileAsync(profileId, cancellationToken);
        }

        if (presentLegacyIds.Count == 0)
        {
            RemoveEmptyCompatibilityShells();
            return;
        }

        foreach (MongoId legacyId in presentLegacyIds)
        {
            tradersTable.Remove(legacyId);
            traderConfig.UpdateTime.RemoveAll(update => update.TraderId == legacyId);
            ragfairConfig.Traders.Remove(legacyId);
        }
        RemoveEmptyCompatibilityShells();

        logger.Success($"Admiral consolidated {presentLegacyIds.Count} legacy content providers: {offerRoots} offers/{itemRows} item rows, {quests} quests, {questUnlocks} quest unlocks and {suits} suits; legacy trader tabs removed");
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
        if (legacy.Suits is null || legacy.Suits.Count == 0) return 0;
        admiral.Suits ??= [];
        HashSet<MongoId> existing = admiral.Suits.Select(suit => suit.SuiteId).ToHashSet();
        int added = 0;
        foreach (Suit suit in legacy.Suits)
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
                if (reward.TraderId?.String == legacyId.ToString())
                    reward.TraderId = new StringOrInt(RuntimeIdentity.TraderId, null);
            count++;
        }
        return count;
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
