namespace SPTEconomy;

public static class FleaListingFeePressurePolicy
{
    public static double ResolveTaxMultiplier(EconomyConfig config) => config.Preset switch
    {
        EconomyPreset.Easy => 1.10,
        EconomyPreset.Normal => 1.25,
        EconomyPreset.Hard => 1.50,
        EconomyPreset.Custom => config.CustomFleaListingFeeMultiplier,
        _ => throw new ArgumentOutOfRangeException(nameof(config.Preset), config.Preset, "Unsupported economy preset."),
    };

    public static double Apply(double before, EconomyConfig config)
    {
        if (!double.IsFinite(before) || before <= 0)
            throw new ArgumentOutOfRangeException(nameof(before), "Flea tax component must be finite and > 0.");

        var multiplier = ResolveTaxMultiplier(config);
        if (!double.IsFinite(multiplier) || multiplier < 1.0 || multiplier > 2.0)
            throw new InvalidOperationException($"Flea listing-fee multiplier must be finite and within 1.0..2.0, got {multiplier}.");

        return Math.Round(before * multiplier, 6, MidpointRounding.AwayFromZero);
    }
}
