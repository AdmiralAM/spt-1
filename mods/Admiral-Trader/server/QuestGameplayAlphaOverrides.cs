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
