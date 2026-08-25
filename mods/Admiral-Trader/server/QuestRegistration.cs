using System.Reflection;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace AdmiralTrader.Server;

[Injectable(TypePriority = OnLoadOrder.Preload + 3), UsedImplicitly]
public sealed class AdmiralQuestRegistration(
    ModHelper modHelper,
    TemplateTable templateTable,
    LocaleTable localesTable,
    ISptLogger<AdmiralQuestRegistration> logger) : IOnLoad
{
    private const int ExpectedQuestCount = 10;

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

        RegisterQuestLocales(modPath);
        logger.Success($"Registered {quests.Count} authored Admiral quests");
        return Task.CompletedTask;
    }

    private Dictionary<MongoId, Quest> LoadQuests(string modPath)
    {
        string questDirectory = Path.Combine(modPath, "db", "quests");
        if (!Directory.Exists(questDirectory))
            throw new DirectoryNotFoundException($"Admiral quest directory is missing: {questDirectory}");

        string[] files = Directory.GetFiles(questDirectory, "*.json", SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.Ordinal);
        Dictionary<MongoId, Quest> quests = new();

        foreach (string file in files)
        {
            string relativePath = Path.GetRelativePath(modPath, file).Replace('\\', '/');
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

        foreach (var (questId, quest) in quests)
        {
            if (quest.Id != questId)
                throw new InvalidDataException($"Quest dictionary key/id mismatch: {questId} != {quest.Id}");
            if (quest.TraderId.ToString() != RuntimeIdentity.TraderId)
                throw new InvalidDataException($"Quest {questId} has unexpected trader id {quest.TraderId}");
            if (quest.Conditions.AvailableForFinish is not { Count: 1 } finishConditions)
                throw new InvalidDataException($"Quest {questId} must have exactly one finish condition");

            QuestCondition finish = finishConditions[0];
            if (!string.Equals(finish.ConditionType, "FindItem", StringComparison.Ordinal))
                throw new InvalidDataException($"Quest {questId} finish condition must be FindItem, got {finish.ConditionType}");
            if (finish.OnlyFoundInRaid is not false)
                throw new InvalidDataException($"Quest {questId} must not require found-in-raid keys");
            if (finish.Target is null || finish.Value is null || finish.Value <= 0)
                throw new InvalidDataException($"Quest {questId} has an invalid key objective");
        }
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

    private void RegisterQuestLocales(string modPath)
    {
        Dictionary<string, string> english =
            modHelper.GetJsonDataFromFile<Dictionary<string, string>>(modPath, "db/locales/en.json");
        Dictionary<string, string> russian =
            modHelper.GetJsonDataFromFile<Dictionary<string, string>>(modPath, "db/locales/ru.json");

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
}
