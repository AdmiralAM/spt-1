using System.Reflection;
using System.Text.Json;
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
        Dictionary<string, AccessObjective> accessObjectives = LoadAccessObjectives(modPath);
        ValidateObjectiveLocales(finishConditionIds, english, russian);

        foreach (var (localeCode, localeKvP) in localesTable.Global)
        {
            localeKvP.AddTransformer(lazyLoadedLocaleData =>
            {
                if (lazyLoadedLocaleData is null)
                    return lazyLoadedLocaleData;

                bool isRussian = localeCode.Equals("ru", StringComparison.OrdinalIgnoreCase);
                Dictionary<string, string> source = isRussian ? russian : english;
                foreach (var (key, value) in source)
                    lazyLoadedLocaleData[key] = value;

                foreach (AccessObjective access in accessObjectives.Values)
                {
                    List<string> names = [];
                    bool completeNames = true;
                    foreach (string tpl in access.Targets)
                    {
                        if (!lazyLoadedLocaleData.TryGetValue($"{tpl} Name", out string? name) || string.IsNullOrWhiteSpace(name))
                        {
                            completeNames = false;
                            break;
                        }
                        names.Add(name.Trim());
                    }

                    // Never expose raw template ids to the player. If an installed locale lacks
                    // an item name, retain the validated generic fallback from objectives-*.json.
                    if (!completeNames || names.Count == 0)
                        continue;

                    string list = string.Join(", ", names);
                    if (isRussian)
                    {
                        lazyLoadedLocaleData[access.ConditionId] = access.ConditionType == "HandoverItem"
                            ? $"Передайте {access.Value} предмет(а) из списка: {list}. FIR не требуется."
                            : $"После принятия задания получите {access.Value} предмет(а) из списка: {list}. Уже лежащие в схроне экземпляры не засчитываются; FIR не требуется, предметы не изымаются.";
                    }
                    else
                    {
                        lazyLoadedLocaleData[access.ConditionId] = access.ConditionType == "HandoverItem"
                            ? $"Hand over {access.Value} item(s) from this list: {list}. Found in Raid is not required."
                            : $"After accepting the quest, acquire {access.Value} item(s) from this list: {list}. Existing stash copies do not count; Found in Raid is not required and the items are not consumed.";
                    }
                }

                return lazyLoadedLocaleData;
            });
        }

        logger.Success($"Registered {finishConditionIds.Count} player-facing Admiral objective locales; {accessObjectives.Count} access objectives render concrete installed-locale item names");
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

    private static Dictionary<string, AccessObjective> LoadAccessObjectives(string modPath)
    {
        string questDirectory = IOPath.Combine(modPath, "db", "quests");
        Dictionary<string, AccessObjective> result = new(StringComparer.Ordinal);

        foreach (string file in Directory.GetFiles(questDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
            JsonElement root = document.RootElement;
            string questName = root.GetProperty("QuestName").GetString() ?? string.Empty;
            if (questName.StartsWith("Arsenal Protocol:", StringComparison.Ordinal))
                continue;

            JsonElement finish = root.GetProperty("conditions").GetProperty("AvailableForFinish")[0];
            string conditionType = finish.GetProperty("conditionType").GetString() ?? string.Empty;
            if (conditionType is not ("FindItem" or "HandoverItem"))
                continue;

            string conditionId = finish.GetProperty("id").GetString() ?? throw new InvalidDataException($"Access objective in {IOPath.GetFileName(file)} has no id");
            int value = finish.GetProperty("value").GetInt32();
            List<string> targets = finish.GetProperty("target").EnumerateArray()
                .Select(element => element.GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (targets.Count == 0)
                throw new InvalidDataException($"Access objective {conditionId} has no concrete authored target list");

            result.Add(conditionId, new AccessObjective(conditionId, conditionType, value, targets));
        }

        if (result.Count != 10)
            throw new InvalidDataException($"Expected 10 Admiral access objectives, got {result.Count}");
        return result;
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

    private sealed record AccessObjective(string ConditionId, string ConditionType, int Value, List<string> Targets);
}
