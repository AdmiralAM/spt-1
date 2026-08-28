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
        ApplyQualificationCombatOverrides(english, russian);

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

    private static void ApplyQualificationCombatOverrides(Dictionary<string, string> english, Dictionary<string, string> russian)
    {
        (string Id, string EnFamily, string RuFamily)[] qualifications =
        [
            ("59ca4829e098dfafa03888d2", "an approved sidearm", "одобренного пистолета"),
            ("ad9233f54a7132d905d6f29d", "an approved SMG or PDW", "одобренного ПП или PDW"),
            ("5f62a924076e4b7c2320f2e8", "an approved shotgun", "одобренного дробовика"),
            ("4ada822d634041a721b346d5", "an approved assault rifle", "одобренной штурмовой винтовки"),
            ("2568ee0bfe2ee12f24d78f45", "an approved marksman or battle rifle", "одобренной марксманской или боевой винтовки"),
            ("a0d05e28971f1ba57639b97d", "an approved precision rifle", "одобренной снайперской винтовки"),
            ("cb8a202d7107f39d860ccb38", "an approved special weapon", "одобренного специального оружия")
        ];

        foreach (var qualification in qualifications)
        {
            english[$"{qualification.Id} description"] = $"Qualification: eliminate 1 enemy using {qualification.EnFamily}. No FIR or item handover is involved.";
            english[$"{qualification.Id} startedMessageText"] = $"Prove basic readiness in the field: eliminate one enemy using {qualification.EnFamily}.";
            english[$"{qualification.Id} acceptPlayerMessage"] = english[$"{qualification.Id} startedMessageText"];

            russian[$"{qualification.Id} description"] = $"Квалификация: устраните 1 противника с помощью {qualification.RuFamily}. FIR и сдача оружия не требуются.";
            russian[$"{qualification.Id} startedMessageText"] = $"Подтверди базовую готовность в бою: устрани одного противника с помощью {qualification.RuFamily}.";
            russian[$"{qualification.Id} acceptPlayerMessage"] = russian[$"{qualification.Id} startedMessageText"];
        }
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
