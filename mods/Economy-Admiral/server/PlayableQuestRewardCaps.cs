namespace SPTEconomy;

public sealed record PlayableQuestRewardCaps(
    string PolicyId,
    double ItemBudgetMultiple,
    double RestartableItemBudgetMultiple,
    double XpMultiple,
    double RestartableXpMultiple,
    double StandingMultiple)
{
    public static PlayableQuestRewardCaps Resolve(EconomyPreset preset, AuditPolicy custom)
    {
        ArgumentNullException.ThrowIfNull(custom);
        return preset switch
        {
            EconomyPreset.Easy => new("PlayableQuestRewardPolicyV1/Easy", 2.25, 1.75, 2.25, 1.75, 2.25),
            EconomyPreset.Normal => new("PlayableQuestRewardPolicyV1/Normal", 1.50, 1.15, 1.50, 1.15, 1.50),
            EconomyPreset.Hard => new("PlayableQuestRewardPolicyV1/Hard", 1.10, 1.00, 1.10, 1.00, 1.10),
            EconomyPreset.Custom => Validate(new(
                "PlayableQuestRewardPolicyV1/Custom",
                custom.HighItemValueLowStructureWarnMultiple,
                custom.RestartableHighItemValueWarnMultiple,
                custom.HighXpLowDepthWarnMultiple,
                custom.RestartableHighXpWarnMultiple,
                custom.HighStandingLowDepthWarnMultiple)),
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unsupported Economy Admiral preset."),
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
                 })
        {
            if (!double.IsFinite(value) || value <= 0)
                throw new InvalidOperationException("Economy Admiral playable quest reward multipliers must be finite and positive.");
        }
        return policy;
    }
}
