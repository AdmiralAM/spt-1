using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class MutationPermissionSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        Require(!QuestRewardMutationPermission.AllowsDimension(
            potentiallyEligible: true,
            automaticMutationDenied: true,
            hasManualExactTarget: false),
            "AllowAutomaticMutation=false must block preset-derived automatic mutation.");

        Require(QuestRewardMutationPermission.AllowsDimension(
            potentiallyEligible: true,
            automaticMutationDenied: true,
            hasManualExactTarget: true),
            "AllowAutomaticMutation=false must not block an explicit manual exact target.");

        Require(!QuestRewardMutationPermission.AllowsDimension(
            potentiallyEligible: false,
            automaticMutationDenied: false,
            hasManualExactTarget: true),
            "Manual exact targets must not bypass provenance/dimension eligibility.");

        Require(!QuestRewardMutationPermission.AllowsDimension(
            potentiallyEligible: false,
            automaticMutationDenied: true,
            hasManualExactTarget: true),
            "Automatic deny plus manual exact target still must not bypass provenance/dimension eligibility.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Economy Admiral mutation-permission smoke: {message}");
    }
}
