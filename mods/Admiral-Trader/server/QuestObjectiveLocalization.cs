using System.Reflection;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using IOPath = System.IO.Path;

namespace AdmiralTrader.Server;

/// <summary>
/// Publishes explicit player-facing text for every Admiral finish condition.
/// Native EFT quest UI resolves objective text by condition id; quest-level
/// descriptions alone are not sufficient and otherwise expose opaque ids.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Preload + 4), UsedImplicitly]
public sealed class QuestObjectiveLocalization(
    ModHelper modHelper,
    LocaleTable localesTable,
    ISptLogger<QuestObjectiveLocalization> logger) : IOnLoad
{
    private const int ExpectedObjectiveCount = 31;

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        RuntimeRegistrationManifest runtimeManifest = AdmiralTraderRegistration.LoadRuntimeManifest(modPath);
        if (!runtimeManifest.RegistrationEnabled)
        {
            logger.Info("Admiral objective localization gate is disabled with runtime registration");
            return Task.CompletedTask;
        }

        Dictionary<string, string> english =
            modHelper.GetJsonDataFromFile<Dictionary<string, string>>(modPath, "db/locales/objectives-en.json");
        Dictionary<string, string> russian =
            modHelper.GetJsonDataFromFile<Dictionary<string, string>>(modPath, "db/locales/objectives-ru.json");

        HashSet<string> finishConditionIds = LoadFinishConditionIds(modPath);
        ValidateObjectiveLocales(finishConditionIds, english, russian);

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

        logger.Success($"Registered {finishConditionIds.Count} player-facing Admiral objective locales");
        return Task.CompletedTask;
    }

    private HashSet<string> LoadFinishConditionIds(string modPath)
    {
        string questDirectory = IOPath.Combine(modPath, "db", "quests");
        if (!Directory.Exists(questDirectory))
            throw new DirectoryNotFoundException($"Admiral quest directory is missing: {questDirectory}");

        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (string file in Directory.GetFiles(questDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            string relativePath = IOPath.GetRelativePath(modPath, file).Replace('\\', '/');
            Quest quest = modHelper.GetJsonDataFromFile<Quest>(modPath, relativePath);
            if (quest.Conditions.AvailableForFinish is not { Count: 1 } finishConditions)
                throw new InvalidDataException($"Quest {quest.Id} must expose exactly one finish condition for objective localization");

            string conditionId = finishConditions[0].Id.ToString();
            if (string.IsNullOrWhiteSpace(conditionId) || !ids.Add(conditionId))
                throw new InvalidDataException($"Quest {quest.Id} has a missing or duplicate finish condition id: {conditionId}");
        }

        if (ids.Count != ExpectedObjectiveCount)
            throw new InvalidDataException($"Expected {ExpectedObjectiveCount} Admiral finish objectives, got {ids.Count}");
        return ids;
    }

    private static void ValidateObjectiveLocales(
        HashSet<string> finishConditionIds,
        Dictionary<string, string> english,
        Dictionary<string, string> russian)
    {
        ValidateOneLocale("en", finishConditionIds, english);
        ValidateOneLocale("ru", finishConditionIds, russian);

        HashSet<string> extraEnglish = english.Keys.Where(key => !finishConditionIds.Contains(key)).ToHashSet(StringComparer.Ordinal);
        HashSet<string> extraRussian = russian.Keys.Where(key => !finishConditionIds.Contains(key)).ToHashSet(StringComparer.Ordinal);
        if (extraEnglish.Count != 0 || extraRussian.Count != 0)
            throw new InvalidDataException(
                $"Admiral objective locale drift: extra EN=[{string.Join(", ", extraEnglish.Order())}], " +
                $"extra RU=[{string.Join(", ", extraRussian.Order())}]");
    }

    private static void ValidateOneLocale(
        string localeCode,
        HashSet<string> finishConditionIds,
        Dictionary<string, string> locale)
    {
        List<string> missing = finishConditionIds.Where(id => !locale.ContainsKey(id)).Order().ToList();
        if (missing.Count != 0)
            throw new InvalidDataException($"Admiral objective locale {localeCode} is missing condition ids: {string.Join(", ", missing)}");

        foreach (string conditionId in finishConditionIds)
        {
            string text = locale[conditionId];
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidDataException($"Admiral objective locale {localeCode} has empty text for {conditionId}");
            if (string.Equals(text.Trim(), conditionId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Admiral objective locale {localeCode} exposes raw condition id {conditionId}");
        }
    }
}
