using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTBeltArmbandInventory.Server;

[Injectable(TypePriority = OnLoadOrder.Preload + 4)]
public sealed class ArmBandBotLootRegistration(
    BotTable botTable,
    ArmBandFeatureConfig featureConfig,
    ISptLogger<ArmBandBotLootRegistration> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var config = featureConfig.Value.ArmBandRoles;
        if (!config.Enabled)
        {
            logger.Info("B&A&HB randomized ArmBand bot loot is disabled; original pools retained.");
            return Task.CompletedTask;
        }

        IReadOnlyDictionary<ArmBandRole, int> weights = featureConfig.EffectiveWeights();
        int pools = 0;
        int sources = 0;
        foreach (var bot in botTable.Types.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var equipment = bot.Value.BotInventory?.Equipment;
            if (equipment is null || !equipment.TryGetValue(EquipmentSlots.ArmBand, out var original) || original is null)
                continue;

            var replacement = new Dictionary<MongoId, double>();
            int changed = 0;
            foreach (var entry in original)
            {
                ArmBandVariantDescriptor[] variants = ArmBandVariantCatalog.All
                    .Where(item => item.SourceTemplateId == entry.Key.ToString()
                        && ArmBandLootPolicy.Includes(config.RaidVisualPool, item.VisualPool))
                    .ToArray();
                KeyValuePair<string, double>[] split = ArmBandLootPolicy.Split(entry.Value, variants, weights);
                if (split.Length == 0)
                {
                    replacement.Add(entry.Key, entry.Value);
                    continue;
                }
                foreach (var variant in split)
                    replacement.Add(new MongoId(variant.Key), variant.Value);
                changed++;
            }
            if (changed == 0) continue;
            equipment[EquipmentSlots.ArmBand] = replacement;
            pools++;
            sources += changed;
        }

        logger.Success($"B&A&HB bounded ArmBand bot-pool plan committed: pools={pools}, sourceEntries={sources}, visualPool={config.RaidVisualPool}; original weights preserved.");
        return Task.CompletedTask;
    }
}
