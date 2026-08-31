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
            new ItemRewardRecordCandidate(4, 3d),
        ], allowUniqueDominant: true);
        Require(single.Eligible && single.SelectedRecordIndex == 4 && single.Reason == "SingleReducibleRewardRecord",
            "single automatic reducible reward record must remain eligible");

        var dominant = ItemRewardRecordSelectorCore.Select([
            new ItemRewardRecordCandidate(0, 2d),
            new ItemRewardRecordCandidate(1, 6d),
            new ItemRewardRecordCandidate(2, 3d),
        ], allowUniqueDominant: true);
        Require(dominant.Eligible && dominant.SelectedRecordIndex == 1 && dominant.Reason == "UniqueDominantReducibleRewardRecord",
            "automatic pressure must select the unique largest reducible stack across separate Success Item reward records");

        var tie = ItemRewardRecordSelectorCore.Select([
            new ItemRewardRecordCandidate(0, 5d),
            new ItemRewardRecordCandidate(1, 5d),
            new ItemRewardRecordCandidate(2, 2d),
        ], allowUniqueDominant: true);
        Require(!tie.Eligible && tie.Reason == "AmbiguousMultipleReducibleRewardRecords",
            "equal dominant stacks across separate reward records must remain fail-closed");

        var manualSingle = ItemRewardRecordSelectorCore.Select([
            new ItemRewardRecordCandidate(7, 3d),
        ], allowUniqueDominant: false);
        Require(manualSingle.Eligible && manualSingle.SelectedRecordIndex == 7,
            "manual exact selection must still allow one structurally unique reward record");

        var manualMultiple = ItemRewardRecordSelectorCore.Select([
            new ItemRewardRecordCandidate(0, 9d),
            new ItemRewardRecordCandidate(1, 2d),
        ], allowUniqueDominant: false);
        Require(!manualMultiple.Eligible && manualMultiple.Reason == "AmbiguousMultipleReducibleRewardRecords",
            "manual exact selection must not guess between multiple separate reward records");
    }
}
