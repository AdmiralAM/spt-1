namespace SPTEconomy;

public static class QuestRewardMutationPermission
{
    public static bool AllowsDimension(
        bool potentiallyEligible,
        bool automaticMutationDenied,
        bool hasManualExactTarget)
        => potentiallyEligible && (!automaticMutationDenied || hasManualExactTarget);
}
