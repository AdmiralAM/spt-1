using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class ItemRewardRecordSelectorSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"Economy Admiral item reward record selector smoke: {message}");
        }

        var single = ItemRewardRecordSelectorCore.Select([
            new ItemRewardRecordCandidate(4, 3d, 300d, 200d),
        ], allowUniqueDominant: true);
        Require(single.Eligible && single.SelectedRecordIndex == 4 && single.Reason == "SingleReducibleRewardRecord",
            "single automatic reducible reward record must remain eligible");

        var economicDominant = ItemRewardRecordSelectorCore.Select([
            new ItemRewardRecordCandidate(0, 100d, 10_000d, 9_900d),
            new ItemRewardRecordCandidate(1, 2d, 50_000d, 25_000d),
            new ItemRewardRecordCandidate(2, 3d, 3_000d, 2_000d),
        ], allowUniqueDominant: true);
        Require(economicDominant.Eligible && economicDominant.SelectedRecordIndex == 1
            && economicDominant.Reason == "UniqueDominantReducibleRewardRecord",
            "automatic pressure must rank separate reward records by handbook economic contribution, not raw stack count");

        var reducibleTieBreak = ItemRewardRecordSelectorCore.Select([
            new ItemRewardRecordCandidate(0, 2d, 10_000d, 5_000d),
            new ItemRewardRecordCandidate(1, 5d, 10_000d, 8_000d),
        ], allowUniqueDominant: true);
        Require(reducibleTieBreak.Eligible && reducibleTieBreak.SelectedRecordIndex == 1,
            "equal total handbook contribution must use uniquely greater removable value as the safe tie-break");

        var trueTie = ItemRewardRecordSelectorCore.Select([
            new ItemRewardRecordCandidate(0, 5d, 10_000d, 8_000d),
            new ItemRewardRecordCandidate(1, 5d, 10_000d, 8_000d),
            new ItemRewardRecordCandidate(2, 2d, 2_000d, 1_000d),
        ], allowUniqueDominant: true);
        Require(!trueTie.Eligible && trueTie.Reason == "AmbiguousMultipleReducibleRewardRecords",
            "true economic ties across separate reward records must remain fail-closed");

        var missingEconomicValue = ItemRewardRecordSelectorCore.Select([
            new ItemRewardRecordCandidate(0, 9d),
        ], allowUniqueDominant: true);
        Require(!missingEconomicValue.Eligible && missingEconomicValue.Reason == "InvalidReducibleRecordEconomicValue",
            "automatic cross-record selection must fail closed without explicit economic values");

        var manualSingle = ItemRewardRecordSelectorCore.Select([
            new ItemRewardRecordCandidate(7, 3d),
        ], allowUniqueDominant: false);
        Require(manualSingle.Eligible && manualSingle.SelectedRecordIndex == 7,
            "manual exact selection must still allow one structurally unique reward record without handbook pricing");

        var manualMultiple = ItemRewardRecordSelectorCore.Select([
            new ItemRewardRecordCandidate(0, 9d),
            new ItemRewardRecordCandidate(1, 2d),
        ], allowUniqueDominant: false);
        Require(!manualMultiple.Eligible && manualMultiple.Reason == "AmbiguousMultipleReducibleRewardRecords",
            "manual exact selection must not guess between multiple separate reward records");
    }
}
