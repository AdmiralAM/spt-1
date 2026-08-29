using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class FleaListingFeePressurePolicySmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        MustEqual("flea fee Easy multiplier", FleaListingFeePressurePolicy.ResolveTaxMultiplier(new EconomyConfig { Preset = EconomyPreset.Easy }), 1.10);
        MustEqual("flea fee Normal multiplier", FleaListingFeePressurePolicy.ResolveTaxMultiplier(new EconomyConfig { Preset = EconomyPreset.Normal }), 1.25);
        MustEqual("flea fee Hard multiplier", FleaListingFeePressurePolicy.ResolveTaxMultiplier(new EconomyConfig { Preset = EconomyPreset.Hard }), 1.50);
        MustEqual("flea fee Custom multiplier", FleaListingFeePressurePolicy.ResolveTaxMultiplier(new EconomyConfig { Preset = EconomyPreset.Custom, CustomFleaListingFeeMultiplier = 1.40 }), 1.40);

        MustEqual("flea fee Easy 5% component", FleaListingFeePressurePolicy.Apply(5.0, new EconomyConfig { Preset = EconomyPreset.Easy }), 5.5);
        MustEqual("flea fee Normal 5% component", FleaListingFeePressurePolicy.Apply(5.0, new EconomyConfig { Preset = EconomyPreset.Normal }), 6.25);
        MustEqual("flea fee Hard 5% component", FleaListingFeePressurePolicy.Apply(5.0, new EconomyConfig { Preset = EconomyPreset.Hard }), 7.5);

        var easy = FleaListingFeePressurePolicy.Apply(5.0, new EconomyConfig { Preset = EconomyPreset.Easy });
        var normal = FleaListingFeePressurePolicy.Apply(5.0, new EconomyConfig { Preset = EconomyPreset.Normal });
        var hard = FleaListingFeePressurePolicy.Apply(5.0, new EconomyConfig { Preset = EconomyPreset.Hard });
        if (!(easy < normal && normal < hard))
            throw new InvalidOperationException("Flea listing-fee pressure is not strictly ordered Easy < Normal < Hard.");

        EconomyConfigValidator.Validate(new EconomyConfig { EnableFleaListingFeePressure = true });
        EconomyConfigValidator.Validate(new EconomyConfig { Preset = EconomyPreset.Custom, CustomFleaListingFeeMultiplier = 1.00 });
        EconomyConfigValidator.Validate(new EconomyConfig { Preset = EconomyPreset.Custom, CustomFleaListingFeeMultiplier = 2.00 });
        MustReject(new EconomyConfig { CustomFleaListingFeeMultiplier = 0.99 });
        MustReject(new EconomyConfig { CustomFleaListingFeeMultiplier = 2.01 });
        MustReject(new EconomyConfig { CustomFleaListingFeeMultiplier = double.PositiveInfinity });

        Console.WriteLine("PASS flea listing-fee pressure policy");
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

        throw new InvalidOperationException("Expected flea listing-fee config to fail validation.");
    }
}
