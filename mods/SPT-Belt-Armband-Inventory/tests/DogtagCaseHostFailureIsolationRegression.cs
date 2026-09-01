using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using SPTarkov.Server.Core.Models.Common;
using SPTBeltArmbandInventory;
using SPTBeltArmbandInventory.Server;

internal static class DogtagCaseHostFailureIsolationRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var bear = new MongoId(DogtagCaseHostContract.BearDogtagTemplateId);
        var usec = new MongoId(DogtagCaseHostContract.UsecDogtagTemplateId);
        var dogtagCase = new MongoId(RuntimeIdentity.DogtagCaseItemId);
        var foreignOwned = new MongoId(RuntimeIdentity.CandidateItemId);

        // Same-set capture is intentionally idempotent regardless of module-initializer
        // order. This pins the canonical baseline before testing concurrent failure.
        DogtagCaseHostContract.CaptureVanillaEntries(new[] { bear, usec });

        var valid = new HashSet<MongoId> { bear, usec, dogtagCase };
        var contaminated = new HashSet<MongoId> { bear, usec, dogtagCase, foreignOwned };
        Exception validFailure = null;
        Exception contaminatedFailure = null;

        var validThread = new Thread(() =>
        {
            try { DogtagCaseHostContract.RequireCommitted(valid); }
            catch (Exception exception) { validFailure = exception; }
        });
        var contaminatedThread = new Thread(() =>
        {
            try { DogtagCaseHostContract.RequireCommitted(contaminated); }
            catch (Exception exception) { contaminatedFailure = exception; }
        });

        validThread.Start();
        contaminatedThread.Start();
        if (!validThread.Join(TimeSpan.FromSeconds(5)) || !contaminatedThread.Join(TimeSpan.FromSeconds(5)))
            throw new InvalidOperationException("Dogtag host failure-isolation regression failed: verification exceeded bounded time.");

        if (validFailure != null)
            throw new InvalidOperationException("Dogtag host failure-isolation regression failed: a concurrent incompatible host poisoned valid committed verification.", validFailure);
        if (!(contaminatedFailure is InvalidOperationException))
            throw new InvalidOperationException("Dogtag host failure-isolation regression failed: cross-owned contamination did not fail closed independently.", contaminatedFailure);

        if (DogtagCaseHostContract.CapturedVanillaEntryCount != 2)
            throw new InvalidOperationException("Dogtag host failure-isolation regression failed: failed concurrent verification mutated the captured preload baseline.");
        if (valid.Count != 3 || !valid.Contains(bear) || !valid.Contains(usec) || !valid.Contains(dogtagCase))
            throw new InvalidOperationException("Dogtag host failure-isolation regression failed: verification mutated the valid caller-owned host set.");
        if (contaminated.Count != 4 || !contaminated.Contains(foreignOwned) || !contaminated.Contains(dogtagCase))
            throw new InvalidOperationException("Dogtag host failure-isolation regression failed: rejected verification mutated the contaminated caller-owned host set.");

        // A rejected verification must not act as a global circuit breaker. The same
        // valid committed snapshot must still prove immediately afterward.
        DogtagCaseHostContract.RequireCommitted(valid);
    }
}
