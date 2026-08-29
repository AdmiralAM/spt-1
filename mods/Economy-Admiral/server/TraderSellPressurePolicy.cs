namespace SPTEconomy;

public static class TraderSellPressurePolicy
{
    public static double ResolvePayoutMultiplier(EconomyConfig config) => config.Preset switch
    {
        EconomyPreset.Easy => 0.95,
        EconomyPreset.Normal => 0.85,
        EconomyPreset.Hard => 0.70,
        EconomyPreset.Custom => config.CustomTraderSellPayoutMultiplier,
        _ => throw new ArgumentOutOfRangeException(nameof(config.Preset), config.Preset, "Unsupported economy preset."),
    };

    public static double ApplyToBuyPriceCoefficient(double before, EconomyConfig config)
    {
        if (!double.IsFinite(before) || before < 0 || before > 100)
            throw new ArgumentOutOfRangeException(nameof(before), "Trader buy-price coefficient must be finite and within 0..100.");

        var multiplier = ResolvePayoutMultiplier(config);
        if (!double.IsFinite(multiplier) || multiplier < 0.50 || multiplier > 1.00)
            throw new InvalidOperationException($"Trader sell payout multiplier must be finite and within 0.50..1.00, got {multiplier}.");

        var payoutShare = 1.0 - (before / 100.0);
        var target = 100.0 * (1.0 - (payoutShare * multiplier));
        target = Math.Round(target, 6, MidpointRounding.AwayFromZero);
        return Math.Clamp(target, before, 100.0);
    }
}
