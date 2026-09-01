using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SPTarkov.Server.Core.Models.Common;
using SPTBeltArmbandInventory.Server;

internal static class DogtagCaseCapturedBaselineIsolationRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var bear = new MongoId(DogtagCaseHostContract.BearDogtagTemplateId);
        var usec = new MongoId(DogtagCaseHostContract.UsecDogtagTemplateId);
        var foreign = new MongoId("5c093e3486f77430cb02e594");
        var caseTpl = new MongoId(SPTBeltArmbandInventory.RuntimeIdentity.DogtagCaseItemId);

        // The preload baseline must never retain a caller-owned mutable collection.
        // Capture an exact canonical set through a HashSet, mutate that same caller
        // object afterwards, then prove the committed host still uses the internal
        // point-in-time snapshot rather than following the caller's later edits.
        var callerOwned = new HashSet<MongoId> { bear, usec };
        DogtagCaseHostContract.CaptureVanillaEntries(callerOwned);
        if (DogtagCaseHostContract.CapturedVanillaEntryCount != 2)
            throw new InvalidOperationException("Dogtag captured-baseline isolation regression failed: canonical baseline cardinality was not retained.");

        callerOwned.Remove(usec);
        callerOwned.Add(foreign);

        if (DogtagCaseHostContract.CapturedVanillaEntryCount != 2)
            throw new InvalidOperationException("Dogtag captured-baseline isolation regression failed: caller mutation changed retained baseline cardinality.");

        DogtagCaseHostContract.RequireCommitted(new HashSet<MongoId> { bear, usec, caseTpl });

        ExpectFailure(
            () => DogtagCaseHostContract.RequireCommitted(new HashSet<MongoId> { bear, foreign, caseTpl }),
            "caller mutation must not rewrite the captured USEC acceptance requirement");

        // Re-capturing the original semantic set through a different object remains
        // idempotent; the contract is value-stable but never collection-reference-owned.
        DogtagCaseHostContract.CaptureVanillaEntries(new HashSet<MongoId> { usec, bear });
        if (DogtagCaseHostContract.CapturedVanillaEntryCount != 2)
            throw new InvalidOperationException("Dogtag captured-baseline isolation regression failed: idempotent recapture changed baseline cardinality.");
    }

    private static void ExpectFailure(Action action, string message)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Dogtag captured-baseline isolation regression failed: " + message);
    }
}
