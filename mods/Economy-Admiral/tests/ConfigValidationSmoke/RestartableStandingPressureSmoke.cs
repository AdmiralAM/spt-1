using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class RestartableStandingPressureSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var normal = PlayableQuestRewardCaps.Resolve(new EconomyConfig { Preset = EconomyPreset.Normal });
        var threshold = RestartableStandingPressureCore.ResolveThreshold(normal);
        if (Math.Abs(threshold - 1.15) > 0.000001)
            throw new InvalidOperationException($"Restartable standing Normal threshold must reuse maintained restartable tightness 1.15, got {threshold}.");

        if (!RestartableStandingPressureCore.ShouldFlag(true, 1.20, threshold))
            throw new InvalidOperationException("Restartable standing above the Normal restartable threshold must be classified for pressure.");
        if (RestartableStandingPressureCore.ShouldFlag(true, 1.10, threshold))
            throw new InvalidOperationException("Restartable standing below the Normal restartable threshold must remain untouched.");
        if (RestartableStandingPressureCore.ShouldFlag(false, 5.0, threshold))
            throw new InvalidOperationException("Normal non-restartable quests must never receive the restartable standing flag.");
        if (RestartableStandingPressureCore.ShouldFlag(true, null, threshold))
            throw new InvalidOperationException("Missing standing evidence must fail closed.");

        var enforcementFlags = RestartableStandingPressureCore.EnforcementFlags(true, 1.20, threshold);
        if (!enforcementFlags.Contains(RestartableStandingPressureCore.Flag, StringComparer.Ordinal)
            || !enforcementFlags.Contains(RestartableStandingPressureCore.StandingBudgetFlag, StringComparer.Ordinal))
            throw new InvalidOperationException("Restartable standing classification must emit both its explicit reason and the standing-budget flag consumed by the mutation planner.");
        if (RestartableStandingPressureCore.EnforcementFlags(true, 1.10, threshold).Count != 0)
            throw new InvalidOperationException("Standing below the restartable threshold must emit no enforcement flags.");

        var classifierPolicy = new AuditPolicy
        {
            HighStandingLowDepthWarnMultiple = normal.StandingMultiple,
            RestartableHighStandingWarnMultiple = threshold,
        };
        var classified = QuestRewardPressureClassifier.Reclassify(
            new QuestRewardPressureSignals
            {
                Restartable = true,
                StandingVsVanillaMedian = 1.20,
                ExistingFlags = [],
            },
            classifierPolicy);
        if (!classified.Contains(RestartableStandingPressureCore.Flag, StringComparer.Ordinal)
            || !classified.Contains(RestartableStandingPressureCore.StandingBudgetFlag, StringComparer.Ordinal))
            throw new InvalidOperationException("Final reward-pressure reclassification must surface authored restartable standing into the standing mutation path.");

        var enforcementPolicy = classifierPolicy;
        var restartableTargetMultiple = RestartableStandingPressureCore.ResolveTargetMultiple(true, enforcementFlags, enforcementPolicy);
        if (Math.Abs(restartableTargetMultiple - 1.15) > 0.000001)
            throw new InvalidOperationException("A restartable standing outlier must target the restartable 1.15 cap rather than the ordinary 1.50 standing cap.");
        var ordinaryTargetMultiple = RestartableStandingPressureCore.ResolveTargetMultiple(false, [RestartableStandingPressureCore.StandingBudgetFlag], enforcementPolicy);
        if (Math.Abs(ordinaryTargetMultiple - 1.50) > 0.000001)
            throw new InvalidOperationException("A non-restartable standing outlier must keep the ordinary 1.50 standing cap.");

        var selectiveOff = new EconomyConfig
        {
            Mode = EconomyMode.Enforce,
            EnablePlayableEconomyBundle = false,
            EnableQuestEconomyCluster = true,
            EnableQuestStandingPressure = true,
            EnableRestartableQuestPressure = false,
        };
        if (QuestMechanismGate.AutomaticFlagEnabled(selectiveOff, true, RestartableStandingPressureCore.Flag)
            || QuestMechanismGate.AutomaticFlagEnabled(selectiveOff, true, RestartableStandingPressureCore.StandingBudgetFlag))
            throw new InvalidOperationException("Repeatable / Restartable Pressure OFF must block all automatic restartable standing enforcement flags.");

        var selectiveOn = selectiveOff with { EnableRestartableQuestPressure = true };
        if (!QuestMechanismGate.AutomaticFlagEnabled(selectiveOn, true, RestartableStandingPressureCore.Flag)
            || !QuestMechanismGate.AutomaticFlagEnabled(selectiveOn, true, RestartableStandingPressureCore.StandingBudgetFlag))
            throw new InvalidOperationException("Standing Pressure ON plus Repeatable / Restartable Pressure ON must allow both restartable standing enforcement flags.");

        var standingOff = selectiveOn with { EnableQuestStandingPressure = false };
        if (QuestMechanismGate.AutomaticFlagEnabled(standingOff, true, RestartableStandingPressureCore.Flag)
            || QuestMechanismGate.AutomaticFlagEnabled(standingOff, true, RestartableStandingPressureCore.StandingBudgetFlag))
            throw new InvalidOperationException("Trader Standing Reward Pressure OFF must block restartable standing pressure.");

        Console.WriteLine("PASS restartable standing classification reaches final classifier + stricter standing target + mutation gates");
    }
}
