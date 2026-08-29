using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class PlayableEconomyBundleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var audit = new EconomyConfig
        {
            Mode = EconomyMode.Audit,
            EnablePlayableEconomyBundle = true,
        };
        MustAllFalse("Audit bundle safety", audit);

        var enforce = new EconomyConfig
        {
            Mode = EconomyMode.Enforce,
            EnablePlayableEconomyBundle = true,
        };
        MustAllTrue("Enforce playable bundle", enforce);

        var selective = new EconomyConfig
        {
            Mode = EconomyMode.Enforce,
            EnablePlayableEconomyBundle = false,
            EnableItemRewardStackNormalization = true,
            EnableTraderPurchasePressure = false,
            EnableTraderSellPressure = true,
            EnableFleaPurchasePressure = false,
            EnableFleaListingFeePressure = true,
            EnableLootPressure = false,
        };

        if (!selective.EnableItemRewardStackNormalization
            || selective.EnableTraderPurchasePressure
            || !selective.EnableTraderSellPressure
            || selective.EnableFleaPurchasePressure
            || !selective.EnableFleaListingFeePressure
            || selective.EnableLootPressure)
            throw new InvalidOperationException("Granular feature switches are not preserved when playable bundle is disabled.");

        EconomyConfigValidator.Validate(audit);
        EconomyConfigValidator.Validate(enforce);
        EconomyConfigValidator.Validate(selective);
        Console.WriteLine("PASS playable economy one-switch activation");
    }

    private static void MustAllFalse(string name, EconomyConfig config)
    {
        if (config.EnableItemRewardStackNormalization
            || config.EnableTraderPurchasePressure
            || config.EnableTraderSellPressure
            || config.EnableFleaPurchasePressure
            || config.EnableFleaListingFeePressure
            || config.EnableLootPressure)
            throw new InvalidOperationException($"{name}: at least one effective playable feature unexpectedly enabled.");
    }

    private static void MustAllTrue(string name, EconomyConfig config)
    {
        if (!config.EnableItemRewardStackNormalization
            || !config.EnableTraderPurchasePressure
            || !config.EnableTraderSellPressure
            || !config.EnableFleaPurchasePressure
            || !config.EnableFleaListingFeePressure
            || !config.EnableLootPressure)
            throw new InvalidOperationException($"{name}: at least one effective playable feature failed to activate.");
    }
}
