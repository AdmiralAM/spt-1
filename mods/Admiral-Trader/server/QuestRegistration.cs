using System.Reflection;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using IOPath = System.IO.Path;

namespace AdmiralTrader.Server;

[Injectable(TypePriority = OnLoadOrder.Preload + 3), UsedImplicitly]
public sealed class AdmiralQuestRegistration(
    ModHelper modHelper,
    TemplateTable templateTable,
    LocaleTable localesTable,
    ISptLogger<AdmiralQuestRegistration> logger) : IOnLoad
{
    private const int ExpectedAccessQuestCount = 10;
    private const int ExpectedArsenalQuestCount = 40;
    private const int ExpectedOperationQuestCount = 22;
    private const int ExpectedStoryQuestCount = 100;
    private const int ExpectedQuestCount = ExpectedAccessQuestCount + ExpectedArsenalQuestCount + ExpectedOperationQuestCount + ExpectedStoryQuestCount;
    private static readonly HashSet<string> OperationQuestIds =
    [
        "8dad0d354ac000b7bbf05b9a", "56813681ae0690016376f163", "208db81b5ce195bf0c176852",
        "6574a072f763d0b09a553401", "8b6f2b25ab2e91e0540761e3", "41a41cb262ea084c1e110513",
        "133aa723b4695a3d93de92f1", "db220288bc8d5559a45feeb1", "4c2cc3f85d60170907642d9e",
        "b1b3d9e3a930a3eae47b2353", "f62d8e1285027e336767513c", "4072a5e458946a243b886ad8",
        "02c07ee31821696597ceabef", "3c6e085fc02f0597efdb5d5a", "31ab6a69a8436df6b3834b0a",
        "e520cec55b83621928e9e4ec", "4a8f533e1ed458e83b41c01f", "4ab0b49478adb233ae900b33",
        "ca33fab8b9cc5f5f5ad322c0", "9c35b3ac22ede1a5a79118bc", "ee813142de655daf2dedfebc",
        "47480d824cea0b80917cafa5"
    ];

    private static readonly string[] RequiredLocaleFields =
    [
        "name",
        "description",
        "note",
        "startedMessageText",
        "successMessageText",
        "failMessageText",
        "acceptPlayerMessage",
        "declinePlayerMessage",
        "completePlayerMessage",
        "changeQuestMessageText"
    ];

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        RuntimeRegistrationManifest runtimeManifest = AdmiralTraderRegistration.LoadRuntimeManifest(modPath);
        if (!runtimeManifest.RegistrationEnabled)
        {
            logger.Info("Admiral Trader quest publication gate is disabled; authored quest data is not injected");
            return Task.CompletedTask;
        }

        Dictionary<MongoId, Quest> quests = LoadQuests(modPath);
        HashSet<string> storyQuestIds = LoadStoryQuestIds(modPath);
        ValidateQuests(quests, storyQuestIds);
        PreflightQuestIds(quests);

        List<MongoId> addedQuestIds = [];
        try
        {
            foreach (var (questId, quest) in quests)
            {
                templateTable.Quests.Add(questId, quest);
                addedQuestIds.Add(questId);
            }

            RegisterQuestLocales(modPath, quests);
        }
        catch
        {
            foreach (MongoId questId in addedQuestIds)
                templateTable.Quests.Remove(questId);
            throw;
        }
        logger.Success($"Registered {quests.Count} authored Admiral quests");
        return Task.CompletedTask;
    }

    private Dictionary<MongoId, Quest> LoadQuests(string modPath)
    {
        string questDirectory = IOPath.Combine(modPath, "db", "quests");
        if (!Directory.Exists(questDirectory))
            throw new DirectoryNotFoundException($"Admiral quest directory is missing: {questDirectory}");

        string[] files = Directory.GetFiles(questDirectory, "*.json", SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.Ordinal);
        Dictionary<MongoId, Quest> quests = new();

        foreach (string file in files)
        {
            string relativePath = IOPath.GetRelativePath(modPath, file).Replace('\\', '/');
            Quest quest = modHelper.GetJsonDataFromFile<Quest>(modPath, relativePath);
            if (!quests.TryAdd(quest.Id, quest))
                throw new InvalidDataException($"Duplicate Admiral quest id {quest.Id} in {relativePath}");
        }

        return quests;
    }

    private HashSet<string> LoadStoryQuestIds(string modPath)
    {
        StoryCampaignRuntimeManifest manifest = modHelper.GetJsonDataFromFile<StoryCampaignRuntimeManifest>(
            modPath,
            "manifests/story-campaign-runtime.json");
        if (manifest.Status != "runtime-materialized" || manifest.StoryQuestCount != ExpectedStoryQuestCount)
            throw new InvalidDataException("Story campaign runtime manifest is not materialized at the expected 100-quest scope");
        HashSet<string> ids = manifest.Quests.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
        if (ids.Count != ExpectedStoryQuestCount)
            throw new InvalidDataException($"Story campaign manifest has {ids.Count} unique quest IDs, expected {ExpectedStoryQuestCount}");
        return ids;
    }

    private static void ValidateQuests(Dictionary<MongoId, Quest> quests, HashSet<string> storyQuestIds)
    {
        if (quests.Count != ExpectedQuestCount)
            throw new InvalidDataException($"Expected {ExpectedQuestCount} authored Admiral quests, got {quests.Count}");

        int accessCount = 0;
        int arsenalCount = 0;
        int operationCount = 0;
        int storyCount = 0;

        foreach (var (questId, quest) in quests)
        {
            if (quest.Id != questId)
                throw new InvalidDataException($"Quest dictionary key/id mismatch: {questId} != {quest.Id}");
            if (quest.TraderId.ToString() != RuntimeIdentity.TraderId)
                throw new InvalidDataException($"Quest {questId} has unexpected trader id {quest.TraderId}");
            if (string.IsNullOrWhiteSpace(quest.QuestName))
                throw new InvalidDataException($"Quest {questId} has no authored QuestName fallback");

            ValidateNativeLifecycleBoundary(questId, quest);

            if (quest.Conditions.AvailableForFinish is not { Count: > 0 } finishConditions)
                throw new InvalidDataException($"Quest {questId} must have at least one finish condition");

            if (storyQuestIds.Contains(questId.ToString()))
            {
                if (finishConditions.Any(finish => finish.ConditionType is not ("CounterCreator" or "FindItem" or "HandoverItem" or "PlaceBeacon")))
                    throw new InvalidDataException($"Story quest {questId} has an unsupported finish condition");
                storyCount++;
                continue;
            }

            if (OperationQuestIds.Contains(questId.ToString()))
            {
                if (finishConditions.Any(finish => finish.ConditionType is not ("CounterCreator" or "HandoverItem")))
                    throw new InvalidDataException($"M3 operation {questId} has an unsupported finish condition");
                operationCount++;
                continue;
            }

            if (finishConditions.Count != 1)
                throw new InvalidDataException($"Frozen baseline quest {questId} must keep exactly one finish condition");

            QuestCondition finish = finishConditions[0];
            if (string.Equals(finish.ConditionType, "FindItem", StringComparison.Ordinal))
            {
                ValidateAccessQuest(questId, finish);
                accessCount++;
                continue;
            }

            if (string.Equals(finish.ConditionType, "CounterCreator", StringComparison.Ordinal))
            {
                if (quest.Type != QuestTypeEnum.Elimination)
                    throw new InvalidDataException($"Arsenal quest {questId} must be Elimination, got {quest.Type}");
                arsenalCount++;
                continue;
            }

            throw new InvalidDataException(
                $"Quest {questId} has unsupported finish condition {finish.ConditionType}; expected FindItem or CounterCreator");
        }

        if (accessCount != ExpectedAccessQuestCount || arsenalCount != ExpectedArsenalQuestCount || operationCount != ExpectedOperationQuestCount || storyCount != ExpectedStoryQuestCount)
            throw new InvalidDataException(
                $"Admiral quest mix drifted: Access={accessCount}/{ExpectedAccessQuestCount}, Arsenal={arsenalCount}/{ExpectedArsenalQuestCount}, Operations={operationCount}/{ExpectedOperationQuestCount}, Story={storyCount}/{ExpectedStoryQuestCount}");
    }

    private static void ValidateNativeLifecycleBoundary(MongoId questId, Quest quest)
    {
        if (quest.InstantComplete is not false)
            throw new InvalidDataException($"Quest {questId} must keep instantComplete=false for explicit native Complete flow");
        if (!string.Equals(quest.AcceptanceAndFinishingSource, "eft", StringComparison.Ordinal))
            throw new InvalidDataException($"Quest {questId} must keep acceptanceAndFinishingSource=eft");
        if (quest.SptStatus is not null)
            throw new InvalidDataException($"Quest {questId} must not pre-seed a per-profile SPT quest status");
        if (quest.Restartable)
            throw new InvalidDataException($"Quest {questId} must remain non-restartable in the current M1 lifecycle contract");

        if (quest.Status != 0)
            throw new InvalidDataException($"Quest {questId} must publish native EFT appear status 0");
        if (!string.Equals(quest.ProgressSource, "eft", StringComparison.Ordinal))
            throw new InvalidDataException($"Quest {questId} must publish native EFT progressSource");
        if (quest.GameModes is null || quest.RankingModes is null || quest.ArenaLocations is null)
            throw new InvalidDataException($"Quest {questId} must publish native EFT mode metadata");

        if (quest.Conditions.Started is not null || quest.Conditions.Success is not null)
            throw new InvalidDataException($"Quest {questId} must omit non-native Started/Success condition collections");
        if (quest.Conditions.Fail is { Count: > 0 })
            throw new InvalidDataException($"Quest {questId} must not attach authored automatic Fail conditions during M1");

        if (quest.Rewards is not null && quest.Rewards.TryGetValue("Started", out List<Reward>? startedRewards) && startedRewards.Count != 0)
            throw new InvalidDataException($"Quest {questId} must not issue rewards on Started during M1");
    }

    private static void ValidateAccessQuest(MongoId questId, QuestCondition finish)
    {
        if (finish.OnlyFoundInRaid is not false)
            throw new InvalidDataException($"Access quest {questId} must not require found-in-raid keys");
        if (finish.Target is null || finish.Value is null || finish.Value <= 0)
            throw new InvalidDataException($"Access quest {questId} has an invalid key objective");
    }

    private void PreflightQuestIds(Dictionary<MongoId, Quest> quests)
    {
        List<MongoId> collisions = quests.Keys
            .Where(templateTable.Quests.ContainsKey)
            .OrderBy(id => id.ToString(), StringComparer.Ordinal)
            .ToList();

        if (collisions.Count != 0)
            throw new InvalidOperationException(
                $"Cannot register Admiral quests: {collisions.Count} quest id collision(s): {string.Join(", ", collisions)}");
    }

    private void RegisterQuestLocales(string modPath, Dictionary<MongoId, Quest> quests)
    {
        Dictionary<string, string> english = LoadLocaleSet(modPath, "en.json", "arsenal-en.json", "m3-en.json", "m8-en.json", "story-en.json");
        Dictionary<string, string> russian = LoadLocaleSet(modPath, "ru.json", "arsenal-ru.json", "m3-ru.json", "m8-ru.json", "story-ru.json");

        EnsureLocaleCoverage("en", english, quests);
        EnsureLocaleCoverage("ru", russian, quests);

        foreach (var (localeCode, localeKvP) in localesTable.Global)
        {
            localeKvP.AddTransformer(lazyLoadedLocaleData =>
            {
                if (lazyLoadedLocaleData is null)
                    return lazyLoadedLocaleData;

                Dictionary<string, string> source = localeCode.Equals("ru", StringComparison.OrdinalIgnoreCase)
                    ? russian
                    : english;
                foreach (var (key, value) in source)
                    lazyLoadedLocaleData[key] = value;

                return lazyLoadedLocaleData;
            });
        }
    }

    private Dictionary<string, string> LoadLocaleSet(string modPath, params string[] localeFiles)
    {
        Dictionary<string, string> merged = new(StringComparer.Ordinal);
        foreach (string localeFile in localeFiles)
        {
            Dictionary<string, string> source =
                modHelper.GetJsonDataFromFile<Dictionary<string, string>>(modPath, $"db/locales/{localeFile}");
            foreach (var (key, value) in source)
            {
                if (!merged.TryAdd(key, value))
                    throw new InvalidDataException($"Duplicate Admiral locale key {key} while loading {localeFile}");
            }
        }

        return merged;
    }

    private static void EnsureLocaleCoverage(
        string localeCode,
        Dictionary<string, string> locale,
        Dictionary<MongoId, Quest> quests)
    {
        foreach (MongoId questId in quests.Keys)
        {
            string id = questId.ToString();
            foreach (string field in RequiredLocaleFields)
            {
                string key = $"{id} {field}";
                if (!locale.ContainsKey(key))
                    throw new InvalidDataException($"Admiral locale {localeCode} is missing required key {key}");
            }
        }
    }
}

public sealed record StoryCampaignRuntimeManifest(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("storyQuestCount")] int StoryQuestCount,
    [property: JsonPropertyName("quests")] List<StoryCampaignQuestRecord> Quests);

public sealed record StoryCampaignQuestRecord([property: JsonPropertyName("id")] string Id);
