using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class LootPressurePolicySmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var easy = LootPressurePolicy.Resolve(new EconomyConfig { Preset = EconomyPreset.Easy });
        var normal = LootPressurePolicy.Resolve(new EconomyConfig { Preset = EconomyPreset.Normal });
        var hard = LootPressurePolicy.Resolve(new EconomyConfig { Preset = EconomyPreset.Hard });
        var custom = LootPressurePolicy.Resolve(new EconomyConfig
        {
            Preset = EconomyPreset.Custom,
            CustomLooseLootScale = 0.83,
            CustomStaticLootScale = 0.92,
        });

        Require(Approximately(easy.LooseLootScale, 0.95) && Approximately(easy.StaticLootScale, 0.95),
            "Easy loot pressure must remain 0.95/0.95");
        Require(Approximately(normal.LooseLootScale, 0.85) && Approximately(normal.StaticLootScale, 0.85),
            "Normal loot pressure must remain 0.85/0.85");
        Require(Approximately(hard.LooseLootScale, 0.70) && Approximately(hard.StaticLootScale, 0.70),
            "Hard loot pressure must remain 0.70/0.70");
        Require(easy.LooseLootScale > normal.LooseLootScale && normal.LooseLootScale > hard.LooseLootScale,
            "loose loot pressure must be strictly stronger Easy < Normal < Hard");
        Require(easy.StaticLootScale > normal.StaticLootScale && normal.StaticLootScale > hard.StaticLootScale,
            "static loot pressure must be strictly stronger Easy < Normal < Hard");
        Require(Approximately(custom.LooseLootScale, 0.83) && Approximately(custom.StaticLootScale, 0.92),
            "custom loot pressure targets must pass through exactly");
        Require(Approximately(LootPressurePolicy.ApplyScale(2.5, normal.LooseLootScale), 2.125),
            "Normal loose loot scale must turn stock bigmap 2.5 into 2.125");
        Require(Approximately(LootPressurePolicy.ApplyScale(1.0, normal.StaticLootScale), 0.85),
            "Normal static loot scale must turn stock 1.0 into 0.85");
        Require(Approximately(LootPressurePolicy.ApplyScale(0, hard.LooseLootScale), 0),
            "zero/non-spawning location multipliers must remain zero");

        Console.WriteLine("PASS loot pressure exact factors, issue targets, ordering, custom pass-through, and zero preservation");
    }

    private static bool Approximately(double left, double right) => Math.Abs(left - right) <= 0.000001;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
