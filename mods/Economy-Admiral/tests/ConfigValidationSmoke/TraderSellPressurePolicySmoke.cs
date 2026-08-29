using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class TraderSellPressurePolicySmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        MustEqual("trader sell Easy payout multiplier", TraderSellPressurePolicy.ResolvePayoutMultiplier(new EconomyConfig { Preset = EconomyPreset.Easy }), 0.95);
        MustEqual("trader sell Normal payout multiplier", TraderSellPressurePolicy.ResolvePayoutMultiplier(new EconomyConfig { Preset = EconomyPreset.Normal }), 0.85);
        MustEqual("trader sell Hard payout multiplier", TraderSellPressurePolicy.ResolvePayoutMultiplier(new EconomyConfig { Preset = EconomyPreset.Hard }), 0.70);
        MustEqual("trader sell Custom payout multiplier", TraderSellPressurePolicy.ResolvePayoutMultiplier(new EconomyConfig { Preset = EconomyPreset.Custom, CustomTraderSellPayoutMultiplier = 0.77 }), 0.77);

        MustEqual("trader sell Easy coefficient 44", TraderSellPressurePolicy.ApplyToBuyPriceCoefficient(44, new EconomyConfig { Preset = EconomyPreset.Easy }), 46.8);
        MustEqual("trader sell Normal coefficient 44", TraderSellPressurePolicy.ApplyToBuyPriceCoefficient(44, new EconomyConfig { Preset = EconomyPreset.Normal }), 52.4);
        MustEqual("trader sell Hard coefficient 44", TraderSellPressurePolicy.ApplyToBuyPriceCoefficient(44, new EconomyConfig { Preset = EconomyPreset.Hard }), 60.8);
        MustEqual("trader sell preserves zero-payout coefficient", TraderSellPressurePolicy.ApplyToBuyPriceCoefficient(100, new EconomyConfig { Preset = EconomyPreset.Hard }), 100);

        var easy = TraderSellPressurePolicy.ApplyToBuyPriceCoefficient(44, new EconomyConfig { Preset = EconomyPreset.Easy });
        var normal = TraderSellPressurePolicy.ApplyToBuyPriceCoefficient(44, new EconomyConfig { Preset = EconomyPreset.Normal });
        var hard = TraderSellPressurePolicy.ApplyToBuyPriceCoefficient(44, new EconomyConfig { Preset = EconomyPreset.Hard });
        if (!(easy < normal && normal < hard))
            throw new InvalidOperationException("Trader sell pressure coefficients are not strictly ordered Easy < Normal < Hard.");

        EconomyConfigValidator.Validate(new EconomyConfig { EnableTraderSellPressure = true });
        EconomyConfigValidator.Validate(new EconomyConfig { Preset = EconomyPreset.Custom, CustomTraderSellPayoutMultiplier = 0.50 });
        EconomyConfigValidator.Validate(new EconomyConfig { Preset = EconomyPreset.Custom, CustomTraderSellPayoutMultiplier = 1.00 });
        MustReject(new EconomyConfig { CustomTraderSellPayoutMultiplier = 0.49 });
        MustReject(new EconomyConfig { CustomTraderSellPayoutMultiplier = 1.01 });
        MustReject(new EconomyConfig { CustomTraderSellPayoutMultiplier = double.NaN });

        Console.WriteLine("PASS trader sell-side pressure policy");
    }

    private static void MustEqual(string name, double actual, double expected)
    {
        if (Math.Abs(actual - expected) > 0.000001)
            throw new InvalidOperationException($"{name}: expected {expected}, got {actual}.");
    }

    private static void MustReject(EconomyConfig config)
    {
        try
        {
            EconomyConfigValidator.Validate(config);
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException("Expected trader sell pressure config to fail validation.");
    }
}
