using System;
using System.Collections;
using System.Collections.Generic;
using AdmiralCompatibilitySuite;

internal static class Program
{
    private static int Main()
    {
        try
        {
            IList eligible = new List<int> { 4, 12 };
            Require(
                SlotAccessSynchronizer.EnsureFollower(eligible, 12, 15) == SlotAccessSyncResult.Added,
                "Belt must be added when ArmBand is enabled.");
            Require(eligible.Count == 3 && eligible[2].Equals(15), "Belt must be appended exactly once.");
            Require(
                SlotAccessSynchronizer.EnsureFollower(eligible, 12, 15) == SlotAccessSyncResult.AlreadyPresent,
                "A second pass must be idempotent.");
            Require(eligible.Count == 3, "A second pass must not duplicate Belt.");

            IList ineligible = new List<int> { 4, 5 };
            Require(
                SlotAccessSynchronizer.EnsureFollower(ineligible, 12, 15) == SlotAccessSyncResult.Ineligible,
                "Belt must not be enabled where ArmBand is disabled.");
            Require(ineligible.Count == 2, "An ineligible list must remain untouched.");

            Console.WriteLine("Slot access synchronizer contract passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
