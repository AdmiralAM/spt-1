namespace SPTEconomy;

public static class TraderPurchasePressurePolicy
{
    public static double ResolveMultiplier(EconomyConfig config) => config.Preset switch
    {
        EconomyPreset.Easy => 1.05,
        EconomyPreset.Normal => 1.15,
        EconomyPreset.Hard => 1.30,
        EconomyPreset.Custom => config.CustomTraderPurchasePriceMultiplier,
        _ => throw new ArgumentOutOfRangeException(nameof(config.Preset), config.Preset, "Unsupported economy preset."),
    };

    public static double ApplyToCurrencyCost(double before, EconomyConfig config)
    {
        if (!double.IsFinite(before) || before <= 0)
            throw new ArgumentOutOfRangeException(nameof(before), "Trader currency cost must be finite and > 0.");

        var multiplier = ResolveMultiplier(config);
        if (!double.IsFinite(multiplier) || multiplier < 1.0 || multiplier > 2.0)
            throw new InvalidOperationException($"Trader purchase pressure multiplier must be finite and within 1.0..2.0, got {multiplier}.");

        return Math.Ceiling(before * multiplier);
    }
}
