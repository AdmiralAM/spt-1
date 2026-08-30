using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class PlayableQuestRewardCapsSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var easy = PlayableQuestRewardCaps.Resolve(new EconomyConfig { Preset = EconomyPreset.Easy });
        var normal = PlayableQuestRewardCaps.Resolve(new EconomyConfig { Preset = EconomyPreset.Normal });
        var hard = PlayableQuestRewardCaps.Resolve(new EconomyConfig { Preset = EconomyPreset.Hard });

        Require(easy.ItemBudgetMultiple > normal.ItemBudgetMultiple && normal.ItemBudgetMultiple > hard.ItemBudgetMultiple,
            "item budget strength must be Easy > Normal > Hard");
        Require(easy.XpMultiple > normal.XpMultiple && normal.XpMultiple > hard.XpMultiple,
            "XP strength must be Easy > Normal > Hard");
        Require(easy.StandingMultiple > normal.StandingMultiple && normal.StandingMultiple > hard.StandingMultiple,
            "standing strength must be Easy > Normal > Hard");
        Require(easy.RestartableStandingMultiple > normal.RestartableStandingMultiple && normal.RestartableStandingMultiple > hard.RestartableStandingMultiple,
            "restartable standing strength must be Easy > Normal > Hard");
        Require(normal.ItemBudgetMultiple == 1.50 && normal.RestartableItemBudgetMultiple == 1.15,
            "Normal item caps are the Playable Economy v1 contract");
        Require(normal.XpMultiple == 1.50 && normal.RestartableXpMultiple == 1.15,
            "Normal XP caps are the Playable Economy v1 contract");
        Require(normal.StandingMultiple == 1.50 && normal.RestartableStandingMultiple == 1.15,
            "Normal standing caps are the Playable Economy v1 contract");

        var customConfig = new EconomyConfig
        {
            Preset = EconomyPreset.Custom,
            CustomQuestItemBudgetMultiple = 1.7,
            CustomRestartableQuestItemBudgetMultiple = 1.2,
            CustomQuestXpMultiple = 1.6,
            CustomRestartableQuestXpMultiple = 1.1,
            CustomQuestStandingMultiple = 1.4,
            CustomRestartableQuestStandingMultiple = 1.05,
            CustomAuditPolicy = new AuditPolicy
            {
                HighItemValueLowStructureWarnMultiple = 9.0,
                RestartableHighItemValueWarnMultiple = 8.0,
                HighXpLowDepthWarnMultiple = 7.0,
                RestartableHighXpWarnMultiple = 6.0,
                HighStandingLowDepthWarnMultiple = 5.0,
            },
        };
        var resolvedCustom = PlayableQuestRewardCaps.Resolve(customConfig);
        Require(resolvedCustom.ItemBudgetMultiple == 1.7 && resolvedCustom.RestartableItemBudgetMultiple == 1.2,
            "Custom item enforcement caps must use dedicated user targets");
        Require(resolvedCustom.XpMultiple == 1.6 && resolvedCustom.RestartableXpMultiple == 1.1,
            "Custom XP enforcement caps must use dedicated user targets");
        Require(resolvedCustom.StandingMultiple == 1.4 && resolvedCustom.RestartableStandingMultiple == 1.05,
            "Custom ordinary/restartable standing caps must be independently configurable");
        Require(customConfig.CustomAuditPolicy.HighItemValueLowStructureWarnMultiple == 9.0,
            "Custom enforcement targets must not overwrite audit detection policy");

        var enforcementPolicy = new AuditPolicy
        {
            HighItemValueLowStructureWarnMultiple = normal.ItemBudgetMultiple,
            HighXpLowDepthWarnMultiple = normal.XpMultiple,
            HighStandingLowDepthWarnMultiple = normal.StandingMultiple,
            RestartableHighItemValueWarnMultiple = normal.RestartableItemBudgetMultiple,
            RestartableHighXpWarnMultiple = normal.RestartableXpMultiple,
            RestartableHighStandingWarnMultiple = RestartableStandingPressureCore.ResolveThreshold(normal),
            LowDepthMaxRelativeMultiple = 1.0,
            LowStructureMaxRelativeMultiple = 1.0,
        };
        var regularFlags = QuestRewardPressureClassifier.Reclassify(
            CreateSignals(false, 1.60, 1.60, 1.60, ["PREREQUISITE_CYCLE"]), enforcementPolicy);
        Require(regularFlags.Contains("HIGH_ITEM_VALUE_LOW_STRUCTURE", StringComparer.Ordinal)
                && regularFlags.Contains("HIGH_XP_LOW_DEPTH", StringComparer.Ordinal)
                && regularFlags.Contains("HIGH_STANDING_LOW_DEPTH", StringComparer.Ordinal),
            "playable preset caps must classify automatic item/XP/standing pressure even when upstream audit thresholds did not");
        Require(regularFlags.Contains("PREREQUISITE_CYCLE", StringComparer.Ordinal),
            "enforcement reclassification must preserve non-reward analysis flags");

        var restartableFlags = QuestRewardPressureClassifier.Reclassify(
            CreateSignals(true, 1.20, 1.20, 1.20, []), enforcementPolicy);
        Require(restartableFlags.Contains("RESTARTABLE_HIGH_ITEM_VALUE", StringComparer.Ordinal)
                && restartableFlags.Contains("RESTARTABLE_HIGH_XP", StringComparer.Ordinal)
                && restartableFlags.Contains(RestartableStandingPressureCore.Flag, StringComparer.Ordinal)
                && restartableFlags.Contains(RestartableStandingPressureCore.StandingBudgetFlag, StringComparer.Ordinal),
            "restartable reward pressure must classify from the stricter playable caps and retain the standing mutation-planner route");

        Console.WriteLine("Economy Admiral Playable Economy v1 reward cap smoke PASS");
    }

    private static QuestRewardPressureSignals CreateSignals(bool restartable, double itemRatio, double xpRatio, double standingRatio, IReadOnlyList<string> flags)
    {
        return new QuestRewardPressureSignals
        {
            Restartable = restartable,
            HandbookValueVsVanillaMedian = itemRatio,
            XpVsVanillaMedian = xpRatio,
            StandingVsVanillaMedian = standingRatio,
            PrerequisiteDepthVsVanillaMedian = 0,
            StructuredConstraintsVsVanillaMedian = 0,
            ExistingFlags = flags,
        };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
