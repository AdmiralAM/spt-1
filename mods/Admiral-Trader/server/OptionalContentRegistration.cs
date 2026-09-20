using System.Reflection;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using IOPath = System.IO.Path;

namespace AdmiralTrader.Server;

// Third-party mods publish their templates after Preload. Keep the persistent
// trader registration early for profile safety, then attach optional content here.
[Injectable(TypePriority = OnLoadOrder.PostLoad), UsedImplicitly]
public sealed class OptionalContentRegistration(
    ModHelper modHelper,
    TradersTable tradersTable,
    TemplateTable templateTable,
    LocaleTable localesTable,
    ISptLogger<OptionalContentRegistration> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        if (!AdmiralTraderRegistration.LoadRuntimeManifest(modPath).RegistrationEnabled)
            return Task.CompletedTask;
        if (!tradersTable.TryGetValue(new MongoId(RuntimeIdentity.TraderId), out Trader? trader))
            throw new InvalidOperationException("Admiral Trader must be registered before optional content is attached");

        int offers = MergeOptionalStorefront(modPath, trader.Assort);
        int optionalRewards = ApplyItemRewardReplacements(modPath, "db/optional/storefront/quest-reward-replacements.json", optional: true);
        int signatureRewards = ApplyItemRewardReplacements(modPath, "db/rewards/natalya-signature-replacements.json", optional: false);
        int earlyWeaponRewards = ApplyCashTrades(modPath, "db/rewards/early-weapon-reward-trades.json", optional: false);
        int fieldSupportRewards = ApplyCashTrades(modPath, "db/rewards/field-support-reward-trades.json", optional: false);
        int tacticalRewards = ApplyCashTrades(modPath, "db/rewards/tactical-reward-trades.json", optional: false);
        int beltRewards = ApplyCashTrades(modPath, "db/rewards/belt-container-reward-trades.json", optional: true);
        int wttPresets = ValidateWttPresetCatalog(modPath);
        int wttRewards = ApplyCashTrades(modPath, "db/optional/wtt-reward-trades.json", optional: true);
        int tgcLocaleFallbacks = RegisterTgcRussianLocaleFallbacks(modPath);
        logger.Success($"Admiral content attached after template publication: {offers} optional offers, {signatureRewards} signature rewards, {earlyWeaponRewards} early weapon rewards, {fieldSupportRewards} field-support rewards, {tacticalRewards} tactical rewards, {wttPresets} WTT complete presets available, {wttRewards} WTT curated rewards, {optionalRewards} optional equipment rewards and {beltRewards} B&A&HB equipment reward trades");
        if (tgcLocaleFallbacks > 0)
            logger.Info($"Admiral supplied {tgcLocaleFallbacks} non-empty Russian-locale fallbacks for available TGC items");
        return Task.CompletedTask;
    }

    private int RegisterTgcRussianLocaleFallbacks(string modPath)
    {
        const string relative = "db/optional/tgc-ru-fallback.json";
        if (!File.Exists(IOPath.Combine(modPath, relative.Replace('/', IOPath.DirectorySeparatorChar)))) return 0;
        Dictionary<MongoId, LocaleDetails> catalogue = modHelper.GetJsonDataFromFile<Dictionary<MongoId, LocaleDetails>>(modPath, relative);
        Dictionary<string, LocaleDetails> available = catalogue
            .Where(row => templateTable.Items.ContainsKey(row.Key))
            .ToDictionary(row => row.Key.ToString(), row => row.Value, StringComparer.Ordinal);
        if (available.Values.Any(row => string.IsNullOrWhiteSpace(row.Name) || string.IsNullOrWhiteSpace(row.ShortName) || string.IsNullOrWhiteSpace(row.Description)))
            throw new InvalidDataException("TGC locale fallback catalogue contains an empty player-facing field");
        if (available.Count == 0) return 0;

        var russian = localesTable.Global.FirstOrDefault(row => row.Key.Equals("ru", StringComparison.OrdinalIgnoreCase)).Value;
        if (russian is null) throw new InvalidDataException("Russian locale table is unavailable");
        russian.AddTransformer(data =>
        {
            if (data is null) return data;
            foreach (var (itemId, locale) in available)
            {
                SetMissing(data, $"{itemId} Name", locale.Name!);
                SetMissing(data, $"{itemId} ShortName", locale.ShortName!);
                SetMissing(data, $"{itemId} Description", locale.Description!);
            }
            return data;
        });
        return available.Count;
    }

    private static void SetMissing(Dictionary<string, string> locale, string key, string fallback)
    {
        if (!locale.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
            locale[key] = fallback;
    }

    private int ValidateWttPresetCatalog(string modPath)
    {
        const string relative = "db/optional/wtt-preset-catalog.json";
        if (!File.Exists(IOPath.Combine(modPath, relative.Replace('/', IOPath.DirectorySeparatorChar)))) return 0;
        List<WttRewardPreset> presets = modHelper.GetJsonDataFromFile<List<WttRewardPreset>>(modPath, relative);
        int admitted = 0;
        foreach (WttRewardPreset preset in presets)
        {
            if (preset.Items.Count == 0 || preset.Items[0].Template.ToString() != preset.RootTemplate || preset.ValueRub <= 0 || string.IsNullOrWhiteSpace(preset.NameRu))
                throw new InvalidDataException($"WTT preset {preset.PresetId} has an invalid root");
            HashSet<string> ids = preset.Items.Select(item => item.Id.ToString()).ToHashSet(StringComparer.Ordinal);
            if (ids.Count != preset.Items.Count || preset.Items.Skip(1).Any(item => item.ParentId is null || !ids.Contains(item.ParentId)))
                throw new InvalidDataException($"WTT preset {preset.PresetId} has a broken item tree");
            if (preset.Items.Any(item => !templateTable.Items.ContainsKey(item.Template))) continue;
            admitted++;
        }
        return admitted;
    }

    private int MergeOptionalStorefront(string modPath, TraderAssort assort)
    {
        int merged = 0;
        foreach (string file in new[] { "wtt-armory-assort.json", "content-backport-assort.json" })
        {
            string relative = $"db/optional/storefront/{file}";
            if (!File.Exists(IOPath.Combine(modPath, relative.Replace('/', IOPath.DirectorySeparatorChar))))
                continue;
            TraderAssort candidate = modHelper.GetJsonDataFromFile<TraderAssort>(modPath, relative);
            Item[] roots = candidate.Items.Where(item => item.ParentId?.ToString() == "hideout").ToArray();
            if (candidate.Items.Select(item => item.Template).Distinct().Any(tpl => !templateTable.Items.ContainsKey(tpl)))
            {
                logger.Info($"Optional Admiral storefront {file} skipped because its source templates are unavailable after mod loading");
                continue;
            }
            HashSet<MongoId> ids = assort.Items.Select(item => item.Id).ToHashSet();
            if (candidate.Items.Any(item => !ids.Add(item.Id)) || candidate.BarterScheme.Keys.Any(assort.BarterScheme.ContainsKey) || candidate.LoyalLevelItems.Keys.Any(assort.LoyalLevelItems.ContainsKey))
                throw new InvalidDataException($"Optional Admiral storefront {file} collides with an existing offer");
            assort.Items.AddRange(candidate.Items);
            foreach (var row in candidate.BarterScheme) assort.BarterScheme.Add(row.Key, row.Value);
            foreach (var row in candidate.LoyalLevelItems) assort.LoyalLevelItems.Add(row.Key, row.Value);
            merged += roots.Length;
        }
        return merged;
    }

    private int ApplyItemRewardReplacements(string modPath, string relative, bool optional)
    {
        if (!File.Exists(IOPath.Combine(modPath, relative.Replace('/', IOPath.DirectorySeparatorChar)))) return 0;
        Dictionary<MongoId, Reward> replacements = modHelper.GetJsonDataFromFile<Dictionary<MongoId, Reward>>(modPath, relative);
        int applied = 0;
        foreach (var (questId, replacement) in replacements)
        {
            if (replacement.Items is null || replacement.Items.Count == 0)
                throw new InvalidDataException($"Reward replacement for {questId} has no items");
            if (replacement.Items.Any(item => !templateTable.Items.ContainsKey(item.Template)))
            {
                if (optional) continue;
                throw new InvalidDataException($"Required signature reward for {questId} references an unknown template");
            }
            if (!templateTable.Quests.TryGetValue(questId, out Quest? quest) || quest.Rewards is null || !quest.Rewards.TryGetValue("Success", out List<Reward>? success))
                throw new InvalidDataException($"Optional reward targets unknown quest {questId}");
            int index = success.FindIndex(reward => reward.Items is { Count: > 0 } && reward.Items[0].Template.ToString() != "5449016a4bdc2d6f028b456f");
            if (index < 0) throw new InvalidDataException($"Optional reward quest {questId} has no replaceable item reward");
            replacement.Index = success[index].Index;
            success[index] = replacement;
            applied++;
        }
        return applied;
    }

    private int ApplyCashTrades(string modPath, string relative, bool optional)
    {
        if (!File.Exists(IOPath.Combine(modPath, relative.Replace('/', IOPath.DirectorySeparatorChar)))) return 0;
        Dictionary<MongoId, OptionalCashTrade> trades = modHelper.GetJsonDataFromFile<Dictionary<MongoId, OptionalCashTrade>>(modPath, relative);
        int applied = 0;
        foreach (var (questId, trade) in trades)
        {
            Reward reward = trade.Reward;
            if (trade.CashReductionRub < 0 || reward.Items is null || reward.Items.Count == 0)
                throw new InvalidDataException($"Optional reward trade for {questId} is malformed");
            if (reward.Items.Any(item => !templateTable.Items.ContainsKey(item.Template)))
            {
                if (optional) continue;
                throw new InvalidDataException($"Required reward trade for {questId} references an unknown template");
            }
            if (!templateTable.Quests.TryGetValue(questId, out Quest? quest) || quest.Rewards is null || !quest.Rewards.TryGetValue("Success", out List<Reward>? success))
                throw new InvalidDataException($"Optional reward trade targets unknown quest {questId}");
            if (trade.CashReductionRub > 0)
            {
                Reward? cash = success.FirstOrDefault(candidate => candidate.Items is { Count: > 0 } && candidate.Items[0].Template.ToString() == "5449016a4bdc2d6f028b456f");
                Item? cashItem = cash?.Items?.FirstOrDefault();
                int minimumCashRub = Math.Max(1, trade.MinimumCashRub);
                if (cash?.Value is null || cash.Value - trade.CashReductionRub < minimumCashRub || cashItem?.Upd is null)
                    throw new InvalidDataException($"Optional reward trade for {questId} cannot preserve its {minimumCashRub:N0} rouble floor");
                double reduced = cash.Value.Value - trade.CashReductionRub;
                cash.Value = reduced;
                cashItem.Upd.StackObjectsCount = reduced;
            }
            reward.Index = success.Max(candidate => candidate.Index ?? 0) + 1;
            success.Add(reward);
            applied++;
        }
        return applied;
    }
}

public sealed record OptionalCashTrade
{
    [JsonPropertyName("cashReductionRub")]
    public int CashReductionRub { get; init; }

    [JsonPropertyName("minimumCashRub")]
    public int MinimumCashRub { get; init; }

    [JsonPropertyName("reward")]
    public required Reward Reward { get; init; }
}

public sealed record WttRewardPreset
{
    [JsonPropertyName("presetId")]
    public required string PresetId { get; init; }
    [JsonPropertyName("source")]
    public required string Source { get; init; }
    [JsonPropertyName("rootTemplate")]
    public required string RootTemplate { get; init; }
    [JsonPropertyName("nameEn")]
    public required string NameEn { get; init; }
    [JsonPropertyName("nameRu")]
    public required string NameRu { get; init; }
    [JsonPropertyName("valueRub")]
    public int ValueRub { get; init; }
    [JsonPropertyName("items")]
    public required List<Item> Items { get; init; }
}
