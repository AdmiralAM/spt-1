using System.Globalization;
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

[Injectable(TypePriority = OnLoadOrder.Preload + 4), UsedImplicitly]
public sealed class AdmiralQuestUxRegistration(
    ModHelper modHelper,
    LocaleTable localesTable,
    ISptLogger<AdmiralQuestUxRegistration> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        RuntimeRegistrationManifest runtimeManifest = AdmiralTraderRegistration.LoadRuntimeManifest(modPath);
        if (!runtimeManifest.RegistrationEnabled)
        {
            logger.Info("Admiral Trader quest UX publication gate is disabled");
            return Task.CompletedTask;
        }

        Dictionary<string, string> english = LoadLayeredLocale(
            modPath,
            ["en.json", "arsenal-en.json"],
            ["gameplay-alpha-en.json"],
            "objectives-en.json");
        Dictionary<string, string> russian = LoadLayeredLocale(
            modPath,
            ["ru.json", "arsenal-ru.json"],
            ["gameplay-alpha-ru.json"],
            "objectives-ru.json");

        Dictionary<string, decimal> standingByQuest = LoadStandingRewards(modPath);
        AppendStandingContext(english, standingByQuest, isRussian: false);
        AppendStandingContext(russian, standingByQuest, isRussian: true);

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

        logger.Success($"Registered Admiral quest UX locales with {standingByQuest.Count} explicit standing reward contexts");
        return Task.CompletedTask;
    }

    private Dictionary<string, string> LoadLayeredLocale(
        string modPath,
        IReadOnlyList<string> baseFiles,
        IReadOnlyList<string> overrideFiles,
        string objectiveFile)
    {
        Dictionary<string, string> merged = new(StringComparer.Ordinal);

        foreach (string file in baseFiles)
            AddUnique(merged, LoadLocaleFile(modPath, file), file);

        foreach (string file in overrideFiles)
        {
            Dictionary<string, string> overrides = LoadLocaleFile(modPath, file);
            foreach (var (key, value) in overrides)
            {
                if (!merged.ContainsKey(key))
                    throw new InvalidDataException($"Admiral UX override {file} references unknown locale key {key}");
                merged[key] = value;
            }
        }

        AddUnique(merged, LoadLocaleFile(modPath, objectiveFile), objectiveFile);
        return merged;
    }

    private Dictionary<string, string> LoadLocaleFile(string modPath, string file)
        => modHelper.GetJsonDataFromFile<Dictionary<string, string>>(modPath, $"db/locales/{file}");

    private static void AddUnique(
        Dictionary<string, string> target,
        Dictionary<string, string> source,
        string file)
    {
        foreach (var (key, value) in source)
        {
            if (!target.TryAdd(key, value))
                throw new InvalidDataException($"Duplicate Admiral locale key {key} while loading {file}");
        }
    }

    private static Dictionary<string, decimal> LoadStandingRewards(string modPath)
    {
        string questDirectory = IOPath.Combine(modPath, "db", "quests");
        Dictionary<string, decimal> result = new(StringComparer.Ordinal);

        foreach (string file in Directory.GetFiles(questDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
            JsonElement root = document.RootElement;
            string questId = root.GetProperty("_id").GetString()
                ?? throw new InvalidDataException($"Quest file {file} has no _id");

            JsonElement success = root.GetProperty("rewards").GetProperty("Success");
            decimal standing = 0m;
            foreach (JsonElement reward in success.EnumerateArray())
            {
                if (!string.Equals(reward.GetProperty("type").GetString(), "TraderStanding", StringComparison.Ordinal))
                    continue;
                standing += reward.GetProperty("value").GetDecimal();
            }

            if (standing <= 0m)
                continue;
            if (standing > 0.05m)
                throw new InvalidDataException($"Quest {questId} standing reward {standing} exceeds Gameplay Alpha safety ceiling 0.05");
            if (!result.TryAdd(questId, standing))
                throw new InvalidDataException($"Duplicate standing context for Admiral quest {questId}");
        }

        return result;
    }

    private static void AppendStandingContext(
        Dictionary<string, string> locale,
        Dictionary<string, decimal> standingByQuest,
        bool isRussian)
    {
        foreach (var (questId, standing) in standingByQuest)
        {
            string amount = standing.ToString("0.00", CultureInfo.InvariantCulture);
            string context = isRussian
                ? $" Репутация у Адмирала: +{amount}."
                : $" Admiral reputation: +{amount}.";

            foreach (string field in new[] { "successMessageText", "completePlayerMessage" })
            {
                string key = $"{questId} {field}";
                if (!locale.TryGetValue(key, out string? existing))
                    throw new InvalidDataException($"Missing Admiral locale key required for standing context: {key}");
                if (!existing.Contains(context, StringComparison.Ordinal))
                    locale[key] = existing.TrimEnd() + context;
            }
        }
    }
}
