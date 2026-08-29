using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class NativeRepeatableQuestPressureSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var normal = PlayableQuestRewardCaps.Resolve(new EconomyConfig { Preset = EconomyPreset.Normal });
        if (Math.Abs(NativeRepeatableQuestPressureCore.Cap(1149, 1000, normal.RestartableXpMultiple) - 1149) > 0.000001)
            throw new InvalidOperationException("Normal native repeatable XP at <=115% pristine must not be increased or reduced.");
        if (Math.Abs(NativeRepeatableQuestPressureCore.Cap(1500, 1000, normal.RestartableXpMultiple) - 1150) > 0.000001)
            throw new InvalidOperationException("Normal native repeatable XP above 115% pristine must cap at 115%.");
        if (Math.Abs(NativeRepeatableQuestPressureCore.Cap(4.0, 2.0, normal.RestartableItemBudgetMultiple) - 2.3) > 0.000001)
            throw new InvalidOperationException("Normal native repeatable item-count potential above 115% pristine must use the restartable item allowance.");

        var standingMultiple = NativeRepeatableQuestPressureCore.ResolveStandingMultiple(normal);
        if (Math.Abs(standingMultiple - 1.15) > 0.000001)
            throw new InvalidOperationException("Normal native repeatable standing must use the maintained stricter restartable 1.15 multiple.");
        if (Math.Abs(NativeRepeatableQuestPressureCore.Cap(0.03, 0.02, standingMultiple) - 0.023) > 0.000001)
            throw new InvalidOperationException("Native repeatable standing inflation must cap against pristine reward scaling.");

        var hard = PlayableQuestRewardCaps.Resolve(new EconomyConfig { Preset = EconomyPreset.Hard });
        if (Math.Abs(NativeRepeatableQuestPressureCore.Cap(2000, 1000, hard.RestartableXpMultiple) - 1000) > 0.000001)
            throw new InvalidOperationException("Hard native repeatable XP must cap inflation back to pristine.");
        if (Math.Abs(NativeRepeatableQuestPressureCore.Cap(4.0, 2.0, hard.RestartableItemBudgetMultiple) - 2.0) > 0.000001)
            throw new InvalidOperationException("Hard native repeatable item-count inflation must cap back to pristine potential.");

        if (!NativeRepeatableQuestPressureCore.Compatible([1d, 2d], [3d, 4d]))
            throw new InvalidOperationException("Equal non-empty native repeatable tier vectors must be compatible.");
        if (NativeRepeatableQuestPressureCore.Compatible([1d], [2d, 3d]))
            throw new InvalidOperationException("Mismatched native repeatable tier vectors must fail closed.");
        if (NativeRepeatableQuestPressureCore.NeedsMutation(100, 100) || NativeRepeatableQuestPressureCore.NeedsMutation(100, 110))
            throw new InvalidOperationException("Native repeatable policy must never produce an increasing/no-op mutation.");
        if (!NativeRepeatableQuestPressureCore.NeedsMutation(100, 90))
            throw new InvalidOperationException("Native repeatable policy must recognize a real downward cap.");

        Console.WriteLine("PASS native daily/weekly/scav repeatable item/XP/standing cap math is bounded and fail-closed");
    }
}
