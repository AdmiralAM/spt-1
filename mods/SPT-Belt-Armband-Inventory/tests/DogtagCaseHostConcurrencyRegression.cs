using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SPTarkov.Server.Core.Models.Common;
using SPTBeltArmbandInventory;
using SPTBeltArmbandInventory.Server;

internal static class DogtagCaseHostConcurrencyRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var bear = new MongoId(DogtagCaseHostContract.BearDogtagTemplateId);
        var usec = new MongoId(DogtagCaseHostContract.UsecDogtagTemplateId);
        var caseTpl = new MongoId(RuntimeIdentity.DogtagCaseItemId);

        // Parallel idempotent capture/verification must be deterministic. This pins
        // the preload contract against future DI/load-order parallelization without
        // widening the accepted host: every worker presents the same canonical
        // baseline and verifies a private committed live set.
        Task[] workers = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < 32; i++)
                {
                    DogtagCaseHostContract.CaptureVanillaEntries(new[] { bear, usec });
                    DogtagCaseHostContract.RequireCommitted(new HashSet<MongoId> { bear, usec, caseTpl });
                }
            }))
            .ToArray();

        Task.WaitAll(workers);

        if (DogtagCaseHostContract.CapturedVanillaEntryCount != 2)
            throw new InvalidOperationException("Dogtag host concurrency regression failed: canonical snapshot cardinality drifted under parallel idempotent access.");

        // A conflicting parallel-era snapshot is still rejected after the exact
        // baseline is established; synchronization must never turn ambiguity into
        // last-writer-wins behavior.
        var foreign = new MongoId("5c093ca986f7740a1867ab12");
        try
        {
            DogtagCaseHostContract.CaptureVanillaEntries(new[] { bear, usec, foreign });
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException("Dogtag host concurrency regression failed: conflicting snapshot was accepted after canonical capture.");
    }
}
