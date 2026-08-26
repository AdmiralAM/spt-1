using System.Reflection;
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
    private const int ExpectedArsenalQuestCount = 21;
    private const int ExpectedArsenalReadinessQuestCount = 7;
    private const int ExpectedArsenalCombatQuestCount = 14;
    private const int ExpectedQuestCount = ExpectedAccessQuestCount + ExpectedArsenalQuestCount;

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
        ValidateQuests(quests);
        PreflightQuestIds(quests);

        foreach (var (questId, quest) in quests)
            templateTable.Quests.Add(questId, quest);

        RegisterQuestLocales(modPath, quests);
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

    private static void ValidateQuests(Dictionary<MongoId, Quest> quests)
    {
        if (quests.Count != ExpectedQuestCount)
            throw new InvalidDataException($"Expected {ExpectedQuestCount} authored Admiral quests, got {quests.Count}");

        int accessCount = 0;
        int arsenalReadinessCount = 0;
        int arsenalCombatCount = 0;

        foreach (var (questId, quest) in quests)
        {
            if (quest.Id != questId)
                throw new InvalidDataException($"Quest dictionary key/id mismatch: {questId} != {quest.Id}");
            if (quest.TraderId.ToString() != RuntimeIdentity.TraderId)
                throw new InvalidDataException($"Quest {questId} has unexpected trader id {quest.TraderId}");
            if (string.IsNullOrWhiteSpace(quest.QuestName))
                throw new InvalidDataException($"Quest {questId} has no authored QuestName fallback");
            if (quest.Conditions.AvailableForFinish is not { Count: 1 } finishConditions)
                throw new InvalidDataException($"Quest {questId} must have exactly one finish condition");

            QuestCondition finish = finishConditions[0];
            bool isArsenal = quest.QuestName.StartsWith("Arsenal Protocol:", StringComparison.Ordinal);

            if (string.Equals(finish.ConditionType, "FindItem", StringComparison.Ordinal))
            {
                ValidateNonFirItemProof(questId, finish);
                if (isArsenal)
                {
                    if (quest.Type != QuestTypeEnum.PickUp)
                        throw new InvalidDataException($"Arsenal Qualification {questId} must be PickUp, got {quest.Type}");
                    arsenalReadinessCount++;
                }
                else
                {
                    accessCount++;
                }
                continue;
            }

            if (string.Equals(finish.ConditionType, "CounterCreator", StringComparison.Ordinal))
            {
                if (!isArsenal)
                    throw new InvalidDataException($"Non-Arsenal quest {questId} unexpectedly uses CounterCreator");
                if (quest.Type != QuestTypeEnum.Elimination)
                    throw new InvalidDataException($"Arsenal combat quest {questId} must be Elimination, got {quest.Type}");
                arsenalCombatCount++;
                continue;
            }

            throw new InvalidDataException(
                $"Quest {questId} has unsupported finish condition {finish.ConditionType}; expected FindItem or CounterCreator");
        }

        if (accessCount != ExpectedAccessQuestCount
            || arsenalReadinessCount != ExpectedArsenalReadinessQuestCount
            || arsenalCombatCount != ExpectedArsenalCombatQuestCount)
            throw new InvalidDataException(
                $"Admiral quest mix drifted: Access={accessCount}/{ExpectedAccessQuestCount}, " +
                $"ArsenalReadiness={arsenalReadinessCount}/{ExpectedArsenalReadinessQuestCount}, " +
                $"ArsenalCombat={arsenalCombatCount}/{ExpectedArsenalCombatQuestCount}");
    }

    private static void ValidateNonFirItemProof(MongoId questId, QuestCondition finish)
    {
        if (finish.OnlyFoundInRaid is not false)
            throw new InvalidDataException($"Item-proof quest {questId} must not require found-in-raid items");
        if (finish.Target is null || finish.Value is null || finish.Value <= 0)
            throw new InvalidDataException($"Item-proof quest {questId} has an invalid objective");
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
        Dictionary<string, string> english = LoadLocaleSet(modPath, "en.json", "arsenal-en.json");
        Dictionary<string, string> russian = LoadLocaleSet(modPath, "ru.json", "arsenal-ru.json");

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
