using System.Reflection;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
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
        int rewards = ApplyOptionalRewardReplacements(modPath);
        logger.Success($"Admiral optional content attached after template publication: {offers} offers and {rewards} quest reward replacements");
        return Task.CompletedTask;
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

    private int ApplyOptionalRewardReplacements(string modPath)
    {
        const string relative = "db/optional/storefront/quest-reward-replacements.json";
        if (!File.Exists(IOPath.Combine(modPath, relative.Replace('/', IOPath.DirectorySeparatorChar)))) return 0;
        Dictionary<MongoId, Reward> replacements = modHelper.GetJsonDataFromFile<Dictionary<MongoId, Reward>>(modPath, relative);
        int applied = 0;
        foreach (var (questId, replacement) in replacements)
        {
            if (replacement.Items is null || replacement.Items.Count == 0 || replacement.Items.Any(item => !templateTable.Items.ContainsKey(item.Template)))
                continue;
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
}
