namespace SPTEconomy;

public sealed record LootPressureTargets(double LooseLootScale, double StaticLootScale);

public static class LootPressurePolicy
{
    public static LootPressureTargets Resolve(EconomyConfig config) => config.Preset switch
    {
        EconomyPreset.Easy => new(0.95, 0.95),
        EconomyPreset.Normal => new(0.85, 0.85),
        EconomyPreset.Hard => new(0.70, 0.70),
        EconomyPreset.Custom => new(config.CustomLooseLootScale, config.CustomStaticLootScale),
        _ => throw new ArgumentOutOfRangeException(nameof(config.Preset), config.Preset, "Unsupported economy preset."),
    };

    public static double ApplyScale(double current, double scale)
    {
        if (!double.IsFinite(current) || current < 0)
            throw new ArgumentOutOfRangeException(nameof(current), "Current loot multiplier must be finite and >= 0.");
        if (!double.IsFinite(scale) || scale < 0.50 || scale > 1.00)
            throw new ArgumentOutOfRangeException(nameof(scale), "Loot pressure scale must be finite and within 0.50..1.00.");
        if (current == 0)
            return 0;
        return current * scale;
    }
}
