namespace SPTEconomy;

public sealed record FleaPurchasePressureTargets(
    double BasePriceMultiplier,
    double MaxPriceDifferenceBelowHandbookPercent,
    double HandbookPriceMultiplier);

public static class FleaPurchasePressurePolicy
{
    public static FleaPurchasePressureTargets Resolve(EconomyConfig config) => config.Preset switch
    {
        EconomyPreset.Easy => new(1.55, 55, 1.05),
        EconomyPreset.Normal => new(1.65, 45, 1.10),
        EconomyPreset.Hard => new(1.80, 35, 1.15),
        EconomyPreset.Custom => new(
            config.CustomFleaBasePriceMultiplier,
            config.CustomFleaMaxPriceDifferenceBelowHandbookPercent,
            config.CustomFleaHandbookPriceMultiplier),
        _ => throw new ArgumentOutOfRangeException(nameof(config.Preset), config.Preset, "Unsupported economy preset."),
    };

    public static double StrongerBasePriceMultiplier(double current, EconomyConfig config)
    {
        if (!double.IsFinite(current) || current <= 0)
            throw new ArgumentOutOfRangeException(nameof(current), "Current flea base price multiplier must be finite and > 0.");
        var target = Resolve(config).BasePriceMultiplier;
        if (!double.IsFinite(target) || target < 1.0 || target > 2.5)
            throw new InvalidOperationException($"Flea base price multiplier must be finite and within 1.0..2.5, got {target}.");
        return Math.Max(current, target);
    }

    public static double StrongerBelowHandbookDifference(double current, EconomyConfig config)
    {
        if (!double.IsFinite(current) || current < 0 || current > 100)
            throw new ArgumentOutOfRangeException(nameof(current), "Below-handbook difference must be finite and within 0..100 percent.");
        var target = Resolve(config).MaxPriceDifferenceBelowHandbookPercent;
        if (!double.IsFinite(target) || target < 0 || target > 100)
            throw new InvalidOperationException($"Flea below-handbook difference must be finite and within 0..100 percent, got {target}.");
        return Math.Min(current, target);
    }

    public static double StrongerHandbookPriceMultiplier(double current, EconomyConfig config)
    {
        if (!double.IsFinite(current) || current <= 0)
            throw new ArgumentOutOfRangeException(nameof(current), "Current handbook multiplier must be finite and > 0.");
        var target = Resolve(config).HandbookPriceMultiplier;
        if (!double.IsFinite(target) || target < 1.0 || target > 2.0)
            throw new InvalidOperationException($"Flea handbook multiplier must be finite and within 1.0..2.0, got {target}.");
        return Math.Max(current, target);
    }
}
