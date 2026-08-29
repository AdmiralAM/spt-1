using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class ClusterControlSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var full = new EconomyConfig
        {
            Mode = EconomyMode.Enforce,
            Preset = EconomyPreset.Normal,
            EnablePlayableEconomyBundle = true,
        };

        Require(full.EnableItemRewardStackNormalization, "bundle should enable quest item-stack pressure");
        Require(full.EnableQuestXpPressure, "bundle should enable quest XP pressure");
        Require(full.EnableQuestStandingPressure, "bundle should enable quest standing pressure");
        Require(full.EnableRestartableQuestPressure, "bundle should enable repeatable quest pressure");
        Require(full.EnableTraderPurchasePressure && full.EnableTraderSellPressure, "bundle should enable trader cluster surfaces");
        Require(full.EnableFleaPurchasePressure && full.EnableFleaListingFeePressure, "bundle should enable flea cluster surfaces");
        Require(full.EnableLooseLootPressure && full.EnableStaticLootPressure && full.EnableLootPressure,
            "bundle should enable both loot mechanisms and the effective legacy master");

        var questsOff = full with
        {
            EnableQuestEconomyCluster = false,
            QuestRewardOverrides = new Dictionary<string, ManualQuestRewardOverride>(StringComparer.Ordinal)
            {
                ["fixture"] = new() { ExperienceTarget = 1 },
            },
        };
        Require(!questsOff.EnableItemRewardStackNormalization
                && !questsOff.EnableQuestXpPressure
                && !questsOff.EnableQuestStandingPressure
                && !questsOff.EnableRestartableQuestPressure,
            "quest cluster OFF must hard-disable every quest pressure mechanism");
        Require(questsOff.QuestRewardOverrides.Count == 0, "quest cluster OFF must hide manual quest mutations");

        var tradersOff = full with { EnableTraderEconomyCluster = false };
        Require(!tradersOff.EnableTraderPurchasePressure && !tradersOff.EnableTraderSellPressure,
            "trader cluster OFF must hard-disable buy/sell pressure");

        var fleaOff = full with { EnableFleaEconomyCluster = false };
        Require(!fleaOff.EnableFleaPurchasePressure && !fleaOff.EnableFleaListingFeePressure,
            "flea cluster OFF must hard-disable purchase/listing pressure");

        var lootOff = full with { EnableLootEconomyCluster = false };
        Require(!lootOff.EnableLootPressure && !lootOff.EnableLooseLootPressure && !lootOff.EnableStaticLootPressure,
            "loot cluster OFF must hard-disable loose/static pressure");

        var granular = new EconomyConfig
        {
            Mode = EconomyMode.Enforce,
            EnablePlayableEconomyBundle = false,
            EnableQuestEconomyCluster = true,
            EnableItemRewardStackNormalization = false,
            EnableQuestXpPressure = true,
            EnableQuestStandingPressure = false,
            EnableRestartableQuestPressure = false,
            EnableTraderEconomyCluster = true,
            EnableTraderPurchasePressure = true,
            EnableTraderSellPressure = false,
            EnableLootEconomyCluster = true,
            EnableLooseLootPressure = true,
            EnableStaticLootPressure = false,
        };
        Require(!granular.EnableItemRewardStackNormalization
                && granular.EnableQuestXpPressure
                && !granular.EnableQuestStandingPressure
                && !granular.EnableRestartableQuestPressure,
            "bundle OFF must preserve independent quest mechanism control");
        Require(granular.EnableTraderPurchasePressure && !granular.EnableTraderSellPressure,
            "bundle OFF must preserve granular trader feature control");
        Require(granular.EnableLootPressure && granular.EnableLooseLootPressure && !granular.EnableStaticLootPressure,
            "bundle OFF must preserve independent loose/static loot control");

        var legacyLootMaster = new EconomyConfig
        {
            Mode = EconomyMode.Enforce,
            EnablePlayableEconomyBundle = false,
            EnableLootEconomyCluster = true,
            EnableLootPressure = true,
        };
        Require(legacyLootMaster.EnableLooseLootPressure && legacyLootMaster.EnableStaticLootPressure,
            "legacy loot master must continue enabling both loot mechanisms");

        var granularBlocked = granular with
        {
            EnableQuestEconomyCluster = false,
            EnableTraderEconomyCluster = false,
            EnableLootEconomyCluster = false,
        };
        Require(!granularBlocked.EnableQuestXpPressure
                && !granularBlocked.EnableTraderPurchasePressure
                && !granularBlocked.EnableLooseLootPressure,
            "cluster OFF must override configured granular true flags");

        var questRow = FixtureQuest(restartable: false);
        Require(PlayableQuestRewardPolicy.AutomaticFlagEnabled(granular, questRow, "HIGH_XP_LOW_DEPTH"),
            "XP pressure ON must preserve automatic XP enforcement flags");
        Require(!PlayableQuestRewardPolicy.AutomaticFlagEnabled(granular, questRow, "HIGH_STANDING_LOW_DEPTH"),
            "standing pressure OFF must suppress automatic standing enforcement flags");
        Require(!PlayableQuestRewardPolicy.AutomaticFlagEnabled(granular, questRow, "HIGH_ITEM_VALUE_LOW_STRUCTURE"),
            "item pressure OFF must suppress automatic item enforcement flags");

        var restartableRow = FixtureQuest(restartable: true);
        Require(!PlayableQuestRewardPolicy.AutomaticFlagEnabled(granular, restartableRow, "RESTARTABLE_HIGH_XP"),
            "repeatable pressure OFF must suppress repeatable automatic reward enforcement");
        Require(PlayableQuestRewardPolicy.AutomaticFlagEnabled(granular, restartableRow, "PREREQUISITE_CYCLE"),
            "mechanism gates must not erase non-reward diagnostic flags");

        Console.WriteLine("PASS advanced economy cluster and mechanism controls");
    }

    private static QuestAnalysisRow FixtureQuest(bool restartable) => new()
    {
        QuestId = "fixture",
        QuestName = "fixture",
        TraderId = "fixture",
        IsVanillaTraderQuest = false,
        Restartable = restartable,
        SuccessKnownHandbookValue = 1,
        UnknownPriceRewardItemRecords = 0,
        Experience = 1,
        TraderStanding = 0.01,
        TraderUnlocks = 0,
        AssortmentUnlocks = 0,
        ProductionSchemeUnlocks = 0,
        DirectPrerequisiteCount = 0,
        MaximumPrerequisiteDepth = 0,
        IsPrerequisiteCycleMember = false,
        ObjectiveConditionCount = 1,
        StructuredConstraintCount = 0,
        TimedConditionCount = 0,
        OneSessionConditionCount = 0,
        FoundInRaidConditionCount = 0,
        PlantConditionCount = 0,
        DistanceConstraintCount = 0,
        DaytimeConstraintCount = 0,
        ObservationalFlags = [],
    };

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
