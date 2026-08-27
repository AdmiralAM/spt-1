using System.Reflection;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace AdmiralTrader.Server;

/// <summary>
/// Applies the Gameplay Alpha player-facing corrections after the base authored
/// locale set: qualification prose describes possession/readiness rather than
/// combat, and milestone completion text states the concrete payoff.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Preload + 5), UsedImplicitly]
public sealed class QuestGameplayAlphaOverrides(
    ModHelper modHelper,
    LocaleTable localesTable,
    ISptLogger<QuestGameplayAlphaOverrides> logger) : IOnLoad
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

        Dictionary<string, string> english =
            modHelper.GetJsonDataFromFile<Dictionary<string, string>>(modPath, "db/locales/gameplay-alpha-en.json");
        Dictionary<string, string> russian =
            modHelper.GetJsonDataFromFile<Dictionary<string, string>>(modPath, "db/locales/gameplay-alpha-ru.json");

        ValidateOverrides(english, russian);
        ApplyStarterClarityOverrides(english, russian);

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

        logger.Success($"Applied {english.Count} Gameplay Alpha quest text overrides");
        return Task.CompletedTask;
    }

    private static void ApplyStarterClarityOverrides(
        Dictionary<string, string> english,
        Dictionary<string, string> russian)
    {
        const string fundamentals = "5d404ebd654de4efecef71d2";
        const string factory = "1b92e4cf212d895be4f70b2c";

        english[$"{fundamentals} description"] =
            "Have any 2 approved access keys in your inventory at the same time. The keys do not need Found in Raid status, are not handed over, and no raid is required.";
        english[$"{fundamentals} startedMessageText"] =
            "First check: keep any two approved access keys in your inventory. Nothing is consumed and you do not need to enter a raid.";
        english[$"{fundamentals} acceptPlayerMessage"] = english[$"{fundamentals} startedMessageText"];

        russian[$"{fundamentals} description"] =
            "Имей одновременно любые 2 подходящих ключа доступа в инвентаре. Статус «Найдено в рейде» не нужен, ключи не сдаются и идти в рейд не требуется.";
        russian[$"{fundamentals} startedMessageText"] =
            "Первая проверка: держи в инвентаре любые два подходящих ключа доступа. Ничего сдавать не нужно и в рейд идти не требуется.";
        russian[$"{fundamentals} acceptPlayerMessage"] = russian[$"{fundamentals} startedMessageText"];

        english[$"{factory} description"] =
            "Have any 1 approved Factory access key in your inventory. It does not need Found in Raid status, is not handed over, and no Factory raid is required.";
        english[$"{factory} startedMessageText"] =
            "Factory check: keep one approved Factory key in your inventory. Nothing is consumed and you do not need to enter Factory.";
        english[$"{factory} acceptPlayerMessage"] = english[$"{factory} startedMessageText"];

        russian[$"{factory} description"] =
            "Имей в инвентаре 1 подходящий ключ доступа для Завода. Статус «Найдено в рейде» не нужен, ключ не сдаётся и заходить на Завод не требуется.";
        russian[$"{factory} startedMessageText"] =
            "Проверка Завода: держи в инвентаре один подходящий ключ Завода. Ничего сдавать не нужно и заходить на карту не требуется.";
        russian[$"{factory} acceptPlayerMessage"] = russian[$"{factory} startedMessageText"];
    }

    private static void ValidateOverrides(
        Dictionary<string, string> english,
        Dictionary<string, string> russian)
    {
        if (english.Count != ExpectedOverrideCount || russian.Count != ExpectedOverrideCount)
            throw new InvalidDataException(
                $"Admiral Gameplay Alpha locale override count drift: EN={english.Count}, RU={russian.Count}, expected={ExpectedOverrideCount}");

        if (!english.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(russian.Keys))
            throw new InvalidDataException("Admiral Gameplay Alpha EN/RU override key sets differ");

        string[] allowedSuffixes =
        [
            " description",
            " startedMessageText",
            " acceptPlayerMessage",
            " successMessageText",
            " completePlayerMessage"
        ];

        foreach (string key in english.Keys)
        {
            if (!allowedSuffixes.Any(suffix => key.EndsWith(suffix, StringComparison.Ordinal)))
                throw new InvalidDataException($"Unsupported Admiral Gameplay Alpha locale override key: {key}");
            if (string.IsNullOrWhiteSpace(english[key]) || string.IsNullOrWhiteSpace(russian[key]))
                throw new InvalidDataException($"Empty Admiral Gameplay Alpha locale override: {key}");
        }
    }
}
