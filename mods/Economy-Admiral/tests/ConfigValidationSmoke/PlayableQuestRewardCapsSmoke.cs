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
        Require(normal.ItemBudgetMultiple == 1.50 && normal.RestartableItemBudgetMultiple == 1.15,
            "Normal item caps are the Playable Economy v1 contract");
        Require(normal.XpMultiple == 1.50 && normal.RestartableXpMultiple == 1.15,
            "Normal XP caps are the Playable Economy v1 contract");
        Require(normal.StandingMultiple == 1.50,
            "Normal standing cap is the Playable Economy v1 contract");

        var customConfig = new EconomyConfig
        {
            Preset = EconomyPreset.Custom,
            CustomQuestItemBudgetMultiple = 1.7,
            CustomRestartableQuestItemBudgetMultiple = 1.2,
            CustomQuestXpMultiple = 1.6,
            CustomRestartableQuestXpMultiple = 1.1,
            CustomQuestStandingMultiple = 1.4,
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
        Require(resolvedCustom.XpMultiple == 1.6 && resolvedCustom.RestartableXpMultiple == 1.1 && resolvedCustom.StandingMultiple == 1.4,
            "Custom XP/standing enforcement caps must use dedicated user targets");
        Require(customConfig.CustomAuditPolicy.HighItemValueLowStructureWarnMultiple == 9.0,
            "Custom enforcement targets must not overwrite audit detection policy");

        Console.WriteLine("Economy Admiral Playable Economy v1 reward cap smoke PASS");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
