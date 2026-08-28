using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class PlayableQuestRewardCapsSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var custom = new AuditPolicy();
        var easy = PlayableQuestRewardCaps.Resolve(EconomyPreset.Easy, custom);
        var normal = PlayableQuestRewardCaps.Resolve(EconomyPreset.Normal, custom);
        var hard = PlayableQuestRewardCaps.Resolve(EconomyPreset.Hard, custom);

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

        var customPolicy = new AuditPolicy
        {
            HighItemValueLowStructureWarnMultiple = 1.7,
            RestartableHighItemValueWarnMultiple = 1.2,
            HighXpLowDepthWarnMultiple = 1.6,
            RestartableHighXpWarnMultiple = 1.1,
            HighStandingLowDepthWarnMultiple = 1.4,
        };
        var resolvedCustom = PlayableQuestRewardCaps.Resolve(EconomyPreset.Custom, customPolicy);
        Require(resolvedCustom.ItemBudgetMultiple == 1.7 && resolvedCustom.RestartableItemBudgetMultiple == 1.2,
            "Custom item caps must remain user-configurable");
        Require(resolvedCustom.XpMultiple == 1.6 && resolvedCustom.RestartableXpMultiple == 1.1 && resolvedCustom.StandingMultiple == 1.4,
            "Custom numeric caps must remain user-configurable");

        Console.WriteLine("Economy Admiral Playable Economy v1 reward cap smoke PASS");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
