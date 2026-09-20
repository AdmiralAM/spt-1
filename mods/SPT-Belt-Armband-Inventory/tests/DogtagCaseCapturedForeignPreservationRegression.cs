using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTarkov.Server.Core.Models.Common;
using SPTBeltArmbandInventory;
using SPTBeltArmbandInventory.Server;

internal static class DogtagCaseCapturedForeignPreservationRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        Type contract = typeof(DogtagCaseHostContract);
        FieldInfo syncField = contract.GetField("SnapshotSync", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Dogtag captured-foreign regression: snapshot lock missing");
        FieldInfo snapshotField = contract.GetField("capturedVanillaEntries", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Dogtag captured-foreign regression: snapshot field missing");
        object sync = syncField.GetValue(null)
            ?? throw new InvalidOperationException("Dogtag captured-foreign regression: snapshot lock is null");

        lock (sync)
        {
            object prior = snapshotField.GetValue(null);
            try
            {
                snapshotField.SetValue(null, null);

                var bear = new MongoId(DogtagCaseHostContract.BearDogtagTemplateId);
                var usec = new MongoId(DogtagCaseHostContract.UsecDogtagTemplateId);
                var capturedForeign = new MongoId("5c093f2e86f7740a1b26180f");
                var laterForeign = new MongoId("5c093f9186f7740a1867ab1f");
                var dogtagCase = new MongoId(RuntimeIdentity.DogtagCaseItemId);

                DogtagCaseHostContract.CaptureVanillaEntries(new[] { bear, usec, capturedForeign });
                Assert(DogtagCaseHostContract.CapturedVanillaEntryCount == 3,
                    "preload snapshot must retain compatible foreign acceptance entries, not only canonical BEAR/USEC tags");

                ExpectFailure(
                    () => DogtagCaseHostContract.RequirePreserved(new HashSet<MongoId> { bear, usec }),
                    "removing a foreign entry that existed at preload must fail closed just like removing a canonical dogtag entry");

                var preservedWithLaterAddition = new HashSet<MongoId> { bear, usec, capturedForeign, laterForeign };
                DogtagCaseHostContract.RequirePreserved(preservedWithLaterAddition);
                Assert(preservedWithLaterAddition.Contains(laterForeign),
                    "post-capture foreign additions must remain compatible and verification must stay read-only");

                preservedWithLaterAddition.Add(dogtagCase);
                DogtagCaseHostContract.RequireCommitted(preservedWithLaterAddition);
                Assert(preservedWithLaterAddition.Count == 5,
                    "committed verification must preserve captured and later foreign host entries alongside the exact Dogtag Case");
            }
            finally
            {
                snapshotField.SetValue(null, prior);
            }
        }
    }

    static void ExpectFailure(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException("Dogtag captured-foreign regression failed: " + message);
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Dogtag captured-foreign regression failed: " + message);
    }
}
