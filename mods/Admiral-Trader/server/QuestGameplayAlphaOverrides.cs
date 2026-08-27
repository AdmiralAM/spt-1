using System.Reflection;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace AdmiralTrader.Server;

[Injectable(TypePriority = OnLoadOrder.Preload + 5), UsedImplicitly]
public sealed class QuestGameplayAlphaOverrides(ModHelper modHelper, LocaleTable localesTable, ISptLogger<QuestGameplayAlphaOverrides> logger) : IOnLoad
{
    private const int ExpectedOverrideCount = 59;

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        RuntimeRegistrationManifest runtimeManifest = AdmiralTraderRegistration.LoadRuntimeManifest(modPath);
        if (!runtimeManifest.RegistrationEnabled)
        {
            logger.Info("Admiral Gameplay Alpha locale override gate is disabled with runtime registration");
            return Task.CompletedTask;
        }

        Dictionary<string, string> english = modHelper.GetJsonDataFromFile<Dictionary<string, string>>(modPath, "db/locales/gameplay-alpha-en.json");
        Dictionary<string, string> russian = modHelper.GetJsonDataFromFile<Dictionary<string, string>>(modPath, "db/locales/gameplay-alpha-ru.json");
        ValidateOverrides(english, russian);
        ApplyStarterClarityOverrides(english, russian);

        foreach (var (localeCode, localeKvP) in localesTable.Global)
        {
            localeKvP.AddTransformer(lazyLoadedLocaleData =>
            {
                if (lazyLoadedLocaleData is null) return lazyLoadedLocaleData;
                Dictionary<string, string> source = localeCode.Equals("ru", StringComparison.OrdinalIgnoreCase) ? russian : english;
                foreach (var (key, value) in source) lazyLoadedLocaleData[key] = value;
                return lazyLoadedLocaleData;
            });
        }

        logger.Success($"Applied {english.Count} Gameplay Alpha quest text overrides");
        return Task.CompletedTask;
    }

    private static void ApplyStarterClarityOverrides(Dictionary<string, string> english, Dictionary<string, string> russian)
    {
        const string fundamentals = "5d404ebd654de4efecef71d2";
        const string factory = "1b92e4cf212d895be4f70b2c";

        english[$"{fundamentals} description"] = "Hand over 2 keys in total: Dorm room 214 key and/or Dorm room 204 key. Found in Raid is not required; use the keys already in your stash.";
        english[$"{fundamentals} startedMessageText"] = "First check: hand over two Dorms access keys — Dorm room 214 and/or Dorm room 204. Existing stash copies are valid; no raid is required.";
        english[$"{fundamentals} acceptPlayerMessage"] = english[$"{fundamentals} startedMessageText"];

        russian[$"{fundamentals} description"] = "Передайте Адмиралу 2 ключа в сумме: «Общ 214» и/или «Общ 204». Статус «Найдено в рейде» не нужен; подходят ключи, которые уже лежат в схроне.";
        russian[$"{fundamentals} startedMessageText"] = "Первая проверка: передай два ключа общежития — «Общ 214» и/или «Общ 204». Подойдут уже имеющиеся в схроне; в рейд идти не требуется.";
        russian[$"{fundamentals} acceptPlayerMessage"] = russian[$"{fundamentals} startedMessageText"];

        english[$"{factory} description"] = "Have any 1 approved Factory access key in your inventory. It does not need Found in Raid status, is not handed over, and no Factory raid is required.";
        english[$"{factory} startedMessageText"] = "Factory check: keep one approved Factory key in your inventory. Nothing is consumed and you do not need to enter Factory.";
        english[$"{factory} acceptPlayerMessage"] = english[$"{factory} startedMessageText"];

        russian[$"{factory} description"] = "Имей в инвентаре 1 подходящий ключ доступа для Завода. Статус «Найдено в рейде» не нужен, ключ не сдаётся и заходить на Завод не требуется.";
        russian[$"{factory} startedMessageText"] = "Проверка Завода: держи в инвентаре один подходящий ключ Завода. Ничего сдавать не нужно и заходить на карту не требуется.";
        russian[$"{factory} acceptPlayerMessage"] = russian[$"{factory} startedMessageText"];
    }

    private static void ValidateOverrides(Dictionary<string, string> english, Dictionary<string, string> russian)
    {
        if (english.Count != ExpectedOverrideCount || russian.Count != ExpectedOverrideCount)
            throw new InvalidDataException($"Admiral Gameplay Alpha locale override count drift: EN={english.Count}, RU={russian.Count}, expected={ExpectedOverrideCount}");
        if (!english.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(russian.Keys))
            throw new InvalidDataException("Admiral Gameplay Alpha EN/RU override key sets differ");
        string[] allowedSuffixes = [" description", " startedMessageText", " acceptPlayerMessage", " successMessageText", " completePlayerMessage"];
        foreach (string key in english.Keys)
        {
            if (!allowedSuffixes.Any(suffix => key.EndsWith(suffix, StringComparison.Ordinal)))
                throw new InvalidDataException($"Unsupported Admiral Gameplay Alpha locale override key: {key}");
            if (string.IsNullOrWhiteSpace(english[key]) || string.IsNullOrWhiteSpace(russian[key]))
                throw new InvalidDataException($"Empty Admiral Gameplay Alpha locale override: {key}");
        }
    }
}
