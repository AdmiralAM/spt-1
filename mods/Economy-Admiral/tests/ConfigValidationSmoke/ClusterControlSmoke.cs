using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class ClusterControlSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var full = new EconomyConfig
        {
            Mode = EconomyMode.Enforce,
            Preset = EconomyPreset.Normal,
            EnablePlayableEconomyBundle = true,
        };

        Require(full.EnableItemRewardStackNormalization, "bundle should enable quest item-stack pressure");
        Require(full.EnableTraderPurchasePressure && full.EnableTraderSellPressure, "bundle should enable trader cluster surfaces");
        Require(full.EnableFleaPurchasePressure && full.EnableFleaListingFeePressure, "bundle should enable flea cluster surfaces");
        Require(full.EnableLootPressure, "bundle should enable loot cluster surface");

        var questsOff = full with
        {
            EnableQuestEconomyCluster = false,
            QuestRewardOverrides = new Dictionary<string, ManualQuestRewardOverride>(StringComparer.Ordinal)
            {
                ["fixture"] = new() { ExperienceTarget = 1 },
            },
        };
        Require(!questsOff.EnableItemRewardStackNormalization, "quest cluster OFF must hard-disable item-stack pressure");
        Require(questsOff.QuestRewardOverrides.Count == 0, "quest cluster OFF must hide manual quest mutations");

        var tradersOff = full with { EnableTraderEconomyCluster = false };
        Require(!tradersOff.EnableTraderPurchasePressure && !tradersOff.EnableTraderSellPressure,
            "trader cluster OFF must hard-disable buy/sell pressure");

        var fleaOff = full with { EnableFleaEconomyCluster = false };
        Require(!fleaOff.EnableFleaPurchasePressure && !fleaOff.EnableFleaListingFeePressure,
            "flea cluster OFF must hard-disable purchase/listing pressure");

        var lootOff = full with { EnableLootEconomyCluster = false };
        Require(!lootOff.EnableLootPressure, "loot cluster OFF must hard-disable loot pressure");

        var granular = new EconomyConfig
        {
            Mode = EconomyMode.Enforce,
            EnablePlayableEconomyBundle = false,
            EnableTraderEconomyCluster = true,
            EnableTraderPurchasePressure = true,
            EnableTraderSellPressure = false,
        };
        Require(granular.EnableTraderPurchasePressure && !granular.EnableTraderSellPressure,
            "bundle OFF must preserve granular feature control inside an enabled cluster");

        var granularBlocked = granular with { EnableTraderEconomyCluster = false };
        Require(!granularBlocked.EnableTraderPurchasePressure,
            "cluster OFF must override a granular true flag");

        Console.WriteLine("PASS advanced economy cluster controls");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
