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

        var selectiveOff = new EconomyConfig
        {
            Mode = EconomyMode.Enforce,
            EnablePlayableEconomyBundle = false,
            EnableQuestEconomyCluster = true,
            EnableQuestStandingPressure = true,
            EnableRestartableQuestPressure = false,
        };
        if (QuestMechanismGate.AutomaticFlagEnabled(selectiveOff, true, RestartableStandingPressureCore.Flag))
            throw new InvalidOperationException("Repeatable / Restartable Pressure OFF must block automatic restartable standing pressure.");

        var selectiveOn = selectiveOff with { EnableRestartableQuestPressure = true };
        if (!QuestMechanismGate.AutomaticFlagEnabled(selectiveOn, true, RestartableStandingPressureCore.Flag))
            throw new InvalidOperationException("Standing Pressure ON plus Repeatable / Restartable Pressure ON must allow restartable standing pressure.");

        var standingOff = selectiveOn with { EnableQuestStandingPressure = false };
        if (QuestMechanismGate.AutomaticFlagEnabled(standingOff, true, RestartableStandingPressureCore.Flag))
            throw new InvalidOperationException("Trader Standing Reward Pressure OFF must block restartable standing pressure.");

        Console.WriteLine("PASS restartable standing pressure classification + mechanism gates");
    }
}
