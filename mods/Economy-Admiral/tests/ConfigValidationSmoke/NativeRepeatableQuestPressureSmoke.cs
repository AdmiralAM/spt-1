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
        if (Math.Abs(NativeRepeatableQuestPressureCore.Cap(20, 15, normal.RestartableXpMultiple) - 17.25) > 0.000001)
            throw new InvalidOperationException("Normal native repeatable skill reward chance inflation must share maintained restartable progression pressure.");
        if (Math.Abs(NativeRepeatableQuestPressureCore.Cap(30, 20, normal.RestartableXpMultiple) - 23) > 0.000001)
            throw new InvalidOperationException("Normal native repeatable skill-point reward inflation must share maintained restartable progression pressure.");
        if (Math.Abs(NativeRepeatableQuestPressureCore.Cap(4.0, 2.0, normal.RestartableItemCountMultiple) - 2.3) > 0.000001)
            throw new InvalidOperationException("Normal native repeatable item-count potential above 115% pristine must use the maintained repeatable item-count allowance.");
        if (Math.Abs(normal.RestartableItemCountMultiple - normal.RestartableItemBudgetMultiple) > 0.000001)
            throw new InvalidOperationException("Normal preset balance must remain unchanged when item-count and reward-value controls are separated.");

        var normalSpreadMultiple = NativeRepeatableQuestPressureCore.ResolveRewardSpreadMultiple(normal, true, true, true);
        if (normalSpreadMultiple is null || Math.Abs(normalSpreadMultiple.Value - 1.15) > 0.000001)
            throw new InvalidOperationException("Normal repeatable reward spread must reuse the maintained strictest active 1.15 allowance.");
        if (Math.Abs(NativeRepeatableQuestPressureCore.Cap(0.50, 0.25, normalSpreadMultiple.Value) - 0.2875) > 0.000001)
            throw new InvalidOperationException("Inflated native repeatable reward spread must be bounded relative to pristine.");
        if (NativeRepeatableQuestPressureCore.ResolveRewardSpreadMultiple(normal, false, false, false) is not null)
            throw new InvalidOperationException("Reward spread must remain untouched when no affected repeatable reward mechanism is active.");

        var custom = PlayableQuestRewardCaps.Resolve(new EconomyConfig
        {
            Preset = EconomyPreset.Custom,
            CustomRestartableQuestItemBudgetMultiple = 1.40,
            CustomRestartableQuestItemCountMultiple = 0.80,
            CustomRestartableQuestXpMultiple = 1.20,
            CustomRestartableQuestStandingMultiple = 1.10,
        });
        if (Math.Abs(custom.RestartableItemBudgetMultiple - 1.40) > 0.000001
            || Math.Abs(custom.RestartableItemCountMultiple - 0.80) > 0.000001)
            throw new InvalidOperationException("Custom repeatable reward-value and item-count caps must resolve independently.");
        if (Math.Abs(NativeRepeatableQuestPressureCore.Cap(2000, 1000, custom.RestartableItemBudgetMultiple) - 1400) > 0.000001)
            throw new InvalidOperationException("Custom repeatable reward-value pressure must use its own cap.");
        if (Math.Abs(NativeRepeatableQuestPressureCore.Cap(4.0, 2.0, custom.RestartableItemCountMultiple) - 1.6) > 0.000001)
            throw new InvalidOperationException("Custom repeatable item-count pressure must use its own cap.");
        var customSpreadMultiple = NativeRepeatableQuestPressureCore.ResolveRewardSpreadMultiple(custom, true, true, true);
        if (customSpreadMultiple is null || Math.Abs(customSpreadMultiple.Value - 1.10) > 0.000001)
            throw new InvalidOperationException("Custom reward spread must obey the strictest active affected dimension without a new balance coefficient.");
        var customItemOnlySpread = NativeRepeatableQuestPressureCore.ResolveRewardSpreadMultiple(custom, true, false, false);
        if (customItemOnlySpread is null || Math.Abs(customItemOnlySpread.Value - 1.40) > 0.000001)
            throw new InvalidOperationException("Custom item-only repeatable spread pressure must reuse the item-value allowance.");

        var standingMultiple = NativeRepeatableQuestPressureCore.ResolveStandingMultiple(normal);
        if (Math.Abs(standingMultiple - 1.15) > 0.000001)
            throw new InvalidOperationException("Normal native repeatable standing must use the maintained stricter restartable 1.15 multiple.");
        if (Math.Abs(NativeRepeatableQuestPressureCore.Cap(0.03, 0.02, standingMultiple) - 0.023) > 0.000001)
            throw new InvalidOperationException("Native repeatable standing inflation must cap against pristine reward scaling.");

        var hard = PlayableQuestRewardCaps.Resolve(new EconomyConfig { Preset = EconomyPreset.Hard });
        if (Math.Abs(NativeRepeatableQuestPressureCore.Cap(2000, 1000, hard.RestartableXpMultiple) - 1000) > 0.000001)
            throw new InvalidOperationException("Hard native repeatable XP must cap inflation back to pristine.");
        if (Math.Abs(NativeRepeatableQuestPressureCore.Cap(4.0, 2.0, hard.RestartableItemCountMultiple) - 2.0) > 0.000001)
            throw new InvalidOperationException("Hard native repeatable item-count inflation must cap back to pristine potential.");
        var hardSpreadMultiple = NativeRepeatableQuestPressureCore.ResolveRewardSpreadMultiple(hard, true, true, true);
        if (hardSpreadMultiple is null || Math.Abs(NativeRepeatableQuestPressureCore.Cap(0.5, 0.25, hardSpreadMultiple.Value) - 0.25) > 0.000001)
            throw new InvalidOperationException("Hard native repeatable reward spread inflation must cap back to pristine.");

        if (!NativeRepeatableQuestPressureCore.Compatible([1d, 2d], [3d, 4d]))
            throw new InvalidOperationException("Equal non-empty native repeatable tier vectors must be compatible.");
        if (NativeRepeatableQuestPressureCore.Compatible([1d], [2d, 3d]))
            throw new InvalidOperationException("Mismatched native repeatable tier vectors must fail closed.");
        if (NativeRepeatableQuestPressureCore.NeedsMutation(100, 100) || NativeRepeatableQuestPressureCore.NeedsMutation(100, 110))
            throw new InvalidOperationException("Native repeatable policy must never produce an increasing/no-op mutation.");
        if (!NativeRepeatableQuestPressureCore.NeedsMutation(100, 90))
            throw new InvalidOperationException("Native repeatable policy must recognize a real downward cap.");

        Console.WriteLine("PASS native repeatable value/count/XP/skill/standing/spread cap math is independent, bounded and fail-closed");
    }
}
