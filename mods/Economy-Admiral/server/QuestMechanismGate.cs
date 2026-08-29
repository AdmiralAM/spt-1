namespace SPTEconomy;

public static class QuestMechanismGate
{
    public static bool AutomaticFlagEnabled(EconomyConfig config, bool restartable, string flag)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (restartable && !config.EnableRestartableQuestPressure && IsRewardPressureFlag(flag))
            return false;

        return flag switch
        {
            "HIGH_ITEM_VALUE_LOW_STRUCTURE" or "RESTARTABLE_HIGH_ITEM_VALUE" => config.EnableItemRewardStackNormalization,
            "HIGH_XP_LOW_DEPTH" or "RESTARTABLE_HIGH_XP" => config.EnableQuestXpPressure,
            "HIGH_STANDING_LOW_DEPTH" => config.EnableQuestStandingPressure,
            _ => true,
        };
    }

    private static bool IsRewardPressureFlag(string flag) => flag is
        "HIGH_ITEM_VALUE_LOW_STRUCTURE" or
        "RESTARTABLE_HIGH_ITEM_VALUE" or
        "HIGH_XP_LOW_DEPTH" or
        "RESTARTABLE_HIGH_XP" or
        "HIGH_STANDING_LOW_DEPTH";
}
