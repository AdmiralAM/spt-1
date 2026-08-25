using System.Reflection;
using System.Text.Json;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;
using IOPath = System.IO.Path;

namespace AdmiralTrader.Server;

/// <summary>
/// Exact-runtime publication guard. The source toolchain resolves candidate TPLs from pinned mirrors,
/// but this guard verifies every gameplay-facing Admiral item/weapon TPL against the database of the
/// SPT runtime that is actually starting before trader/quest registration is allowed to proceed.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Preload + 1), UsedImplicitly]
public sealed class AdmiralRuntimeDataValidation(
    ModHelper modHelper,
    TemplateTable templateTable,
    ISptLogger<AdmiralRuntimeDataValidation> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        RuntimeRegistrationManifest manifest = AdmiralTraderRegistration.LoadRuntimeManifest(modPath);
        if (!manifest.RegistrationEnabled)
        {
            logger.Info("Admiral Trader exact-runtime item validation deferred because publication gate is disabled");
            return Task.CompletedTask;
        }

        HashSet<string> referencedTpls = CollectReferencedTpls(modPath);
        List<string> missing = referencedTpls
            .Where(tpl => !templateTable.Items.ContainsKey(new MongoId(tpl)))
            .OrderBy(tpl => tpl, StringComparer.Ordinal)
            .ToList();

        if (missing.Count != 0)
            throw new InvalidDataException(
                $"Admiral Trader cannot publish on this SPT runtime: {missing.Count} referenced item/weapon TPL(s) are missing: {string.Join(", ", missing)}");

        logger.Success($"Admiral Trader exact-runtime item gate verified {referencedTpls.Count} TPL references against the active SPT database");
        return Task.CompletedTask;
    }

    private static HashSet<string> CollectReferencedTpls(string modPath)
    {
        HashSet<string> result = new(StringComparer.Ordinal);
        CollectAssortTpls(IOPath.Combine(modPath, "db", "assort.json"), result);

        string questDirectory = IOPath.Combine(modPath, "db", "quests");
        if (!Directory.Exists(questDirectory))
            throw new DirectoryNotFoundException($"Admiral quest directory is missing: {questDirectory}");

        foreach (string questPath in Directory.GetFiles(questDirectory, "*.json", SearchOption.TopDirectoryOnly))
            CollectQuestTpls(questPath, result);

        if (result.Count == 0)
            throw new InvalidDataException("Admiral exact-runtime item gate found no TPL references to validate");

        foreach (string tpl in result)
            if (tpl.Length != 24 || tpl.Any(ch => !Uri.IsHexDigit(ch)))
                throw new InvalidDataException($"Admiral data contains malformed item TPL: {tpl}");

        return result;
    }

    private static void CollectAssortTpls(string path, HashSet<string> result)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;

        foreach (JsonElement item in root.GetProperty("items").EnumerateArray())
            AddTpl(item, "_tpl", result);

        foreach (JsonProperty offer in root.GetProperty("barter_scheme").EnumerateObject())
            foreach (JsonElement scheme in offer.Value.EnumerateArray())
                foreach (JsonElement requirement in scheme.EnumerateArray())
                    AddTpl(requirement, "_tpl", result);
    }

    private static void CollectQuestTpls(string path, HashSet<string> result)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;

        if (root.TryGetProperty("conditions", out JsonElement conditions)
            && conditions.TryGetProperty("AvailableForFinish", out JsonElement finish))
        {
            foreach (JsonElement condition in finish.EnumerateArray())
            {
                string? type = condition.TryGetProperty("conditionType", out JsonElement typeElement)
                    ? typeElement.GetString()
                    : null;

                if (string.Equals(type, "FindItem", StringComparison.Ordinal)
                    && condition.TryGetProperty("target", out JsonElement targets))
                {
                    foreach (JsonElement target in targets.EnumerateArray())
                        AddString(target, result);
                }

                if (string.Equals(type, "CounterCreator", StringComparison.Ordinal)
                    && condition.TryGetProperty("counter", out JsonElement counter)
                    && counter.TryGetProperty("conditions", out JsonElement counterConditions))
                {
                    foreach (JsonElement counterCondition in counterConditions.EnumerateArray())
                        if (counterCondition.TryGetProperty("weapon", out JsonElement weapons))
                            foreach (JsonElement weapon in weapons.EnumerateArray())
                                AddString(weapon, result);
                }
            }
        }

        if (root.TryGetProperty("rewards", out JsonElement rewards)
            && rewards.TryGetProperty("Success", out JsonElement successRewards))
        {
            foreach (JsonElement reward in successRewards.EnumerateArray())
                if (reward.TryGetProperty("items", out JsonElement items))
                    foreach (JsonElement item in items.EnumerateArray())
                        AddTpl(item, "_tpl", result);
        }
    }

    private static void AddTpl(JsonElement element, string property, HashSet<string> result)
    {
        if (element.TryGetProperty(property, out JsonElement value))
            AddString(value, result);
    }

    private static void AddString(JsonElement value, HashSet<string> result)
    {
        string? text = value.GetString();
        if (!string.IsNullOrWhiteSpace(text))
            result.Add(text);
    }
}
