using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Config;

namespace SPTEconomy;

[Injectable]
public sealed class LootPressureService(
    LocationConfig locationConfig,
    ISptLogger<LootPressureService> logger)
{
    private static readonly HashSet<string> NonPlayableKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "develop", "hideout", "privatearea", "suburbs", "terminal", "town",
    };

    private int _applied;

    public LootPressureResult Apply(EconomyConfig config)
    {
        if (!config.EnableLootPressure || config.Mode != EconomyMode.Enforce)
            return new(false, 0, 0);
        if (Interlocked.CompareExchange(ref _applied, 1, 0) != 0)
            return new(false, 0, 0);

        var targets = LootPressurePolicy.Resolve(config);
        var looseBefore = locationConfig.LooseLootMultiplier.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var staticBefore = locationConfig.StaticLootMultiplier.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var looseChanged = 0;
        var staticChanged = 0;

        try
        {
            if (config.EnableLooseLootPressure)
            {
                foreach (var key in locationConfig.LooseLootMultiplier.Keys.ToArray())
                {
                    if (NonPlayableKeys.Contains(key))
                        continue;
                    var before = locationConfig.LooseLootMultiplier[key];
                    var after = LootPressurePolicy.ApplyScale(before, targets.LooseLootScale);
                    if (after < before)
                    {
                        locationConfig.LooseLootMultiplier[key] = after;
                        looseChanged++;
                    }
                }
            }

            if (config.EnableStaticLootPressure)
            {
                foreach (var key in locationConfig.StaticLootMultiplier.Keys.ToArray())
                {
                    if (NonPlayableKeys.Contains(key))
                        continue;
                    var before = locationConfig.StaticLootMultiplier[key];
                    var after = LootPressurePolicy.ApplyScale(before, targets.StaticLootScale);
                    if (after < before)
                    {
                        locationConfig.StaticLootMultiplier[key] = after;
                        staticChanged++;
                    }
                }
            }

            logger.Info(
                $"[Economy Admiral] loot pressure applied: preset={config.Preset}, " +
                $"looseEnabled={config.EnableLooseLootPressure}, staticEnabled={config.EnableStaticLootPressure}, " +
                $"looseScale={targets.LooseLootScale:0.###}, staticScale={targets.StaticLootScale:0.###}, " +
                $"looseMaps={looseChanged}, staticMaps={staticChanged}");
            return new(true, looseChanged, staticChanged);
        }
        catch
        {
            locationConfig.LooseLootMultiplier.Clear();
            foreach (var pair in looseBefore)
                locationConfig.LooseLootMultiplier[pair.Key] = pair.Value;
            locationConfig.StaticLootMultiplier.Clear();
            foreach (var pair in staticBefore)
                locationConfig.StaticLootMultiplier[pair.Key] = pair.Value;
            Interlocked.Exchange(ref _applied, 0);
            throw;
        }
    }
}

public sealed record LootPressureResult(bool Applied, int LooseMapsChanged, int StaticMapsChanged);
