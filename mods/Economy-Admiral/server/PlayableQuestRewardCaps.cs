namespace SPTEconomy;

public sealed record PlayableQuestRewardCaps(
    string PolicyId,
    double ItemBudgetMultiple,
    double RestartableItemBudgetMultiple,
    double XpMultiple,
    double RestartableXpMultiple,
    double StandingMultiple,
    double RestartableStandingMultiple)
{
    public static PlayableQuestRewardCaps Resolve(EconomyConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Preset switch
        {
            EconomyPreset.Easy => new("PlayableQuestRewardPolicyV1/Easy", 2.25, 1.75, 2.25, 1.75, 2.25, 1.75),
            EconomyPreset.Normal => new("PlayableQuestRewardPolicyV1/Normal", 1.50, 1.15, 1.50, 1.15, 1.50, 1.15),
            EconomyPreset.Hard => new("PlayableQuestRewardPolicyV1/Hard", 1.10, 1.00, 1.10, 1.00, 1.10, 1.00),
            EconomyPreset.Custom => Validate(new(
                "PlayableQuestRewardPolicyV1/Custom",
                config.CustomQuestItemBudgetMultiple,
                config.CustomRestartableQuestItemBudgetMultiple,
                config.CustomQuestXpMultiple,
                config.CustomRestartableQuestXpMultiple,
                config.CustomQuestStandingMultiple,
                config.CustomRestartableQuestStandingMultiple)),
            _ => throw new ArgumentOutOfRangeException(nameof(config.Preset), config.Preset, "Unsupported Economy Admiral preset."),
        };
    }

    private static PlayableQuestRewardCaps Validate(PlayableQuestRewardCaps policy)
    {
        foreach (var value in new[]
                 {
                     policy.ItemBudgetMultiple,
                     policy.RestartableItemBudgetMultiple,
                     policy.XpMultiple,
                     policy.RestartableXpMultiple,
                     policy.StandingMultiple,
                     policy.RestartableStandingMultiple,
                 })
        {
            if (!double.IsFinite(value) || value < 0.1 || value > 10.0)
                throw new InvalidOperationException("Economy Admiral custom quest reward multipliers must be finite and within 0.1..10.0.");
        }
        return policy;
    }
}
