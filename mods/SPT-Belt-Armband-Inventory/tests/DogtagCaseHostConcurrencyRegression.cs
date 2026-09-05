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
        // baseline, proves a private pre-mutation host, then verifies a private
        // committed live set. RequirePreserved is exercised directly so its new
        // two-snapshot stability boundary cannot remain source-only authority.
        Task[] workers = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < 32; i++)
                {
                    DogtagCaseHostContract.CaptureVanillaEntries(new[] { bear, usec });
                    DogtagCaseHostContract.RequirePreserved(new HashSet<MongoId> { bear, usec });
                    DogtagCaseHostContract.RequireCommitted(new HashSet<MongoId> { bear, usec, caseTpl });
                }
            }))
            .ToArray();

        Task.WaitAll(workers);

        if (DogtagCaseHostContract.CapturedVanillaEntryCount != 2)
            throw new InvalidOperationException("Dogtag host concurrency regression failed: canonical snapshot cardinality drifted under parallel idempotent access.");

        // Two pre-add transactions must never simultaneously own rollback authority
        // for the same exact live HashSet. A stable committed proof does not consume
        // the first receipt and therefore must not release the per-host capture gate.
        // This specifically prevents external add/remove ABA from opening a second
        // baseline while the first snapshot-key rollback authority remains valid.
        var sharedHost = new HashSet<MongoId> { bear, usec };
        HashSet<MongoId> firstAuthority = DogtagCaseHostContract.CaptureRollbackBaseline(sharedHost);
        bool duplicateRejected = false;
        try
        {
            _ = DogtagCaseHostContract.CaptureRollbackBaseline(sharedHost);
        }
        catch (InvalidOperationException)
        {
            duplicateRejected = true;
        }
        if (!duplicateRejected)
            throw new InvalidOperationException("Dogtag host concurrency regression failed: duplicate pre-add rollback capture was accepted for the same exact host reference.");

        if (!sharedHost.Add(caseTpl))
            throw new InvalidOperationException("Dogtag host concurrency regression failed: exact case could not be committed for capture-gate retention proof.");
        DogtagCaseHostContract.RequireCommitted(sharedHost);

        // Simulate an external participant removing the committed Case after the
        // stable proof. The original rollback receipt is still live, so ABA back to
        // the baseline must not permit a second capture on the same host reference.
        if (!sharedHost.Remove(caseTpl))
            throw new InvalidOperationException("Dogtag host concurrency regression failed: external ABA simulation could not remove exact case.");
        bool abaCaptureRejected = false;
        try
        {
            _ = DogtagCaseHostContract.CaptureRollbackBaseline(sharedHost);
        }
        catch (InvalidOperationException)
        {
            abaCaptureRejected = true;
        }
        if (!abaCaptureRejected)
            throw new InvalidOperationException("Dogtag host concurrency regression failed: committed-proof ABA reopened a second rollback baseline before first receipt consumption.");

        // Restore the exact committed shape and consume the original receipt. Only
        // after this explicit single-consumer operation may a fresh capture proceed.
        if (!sharedHost.Add(caseTpl) || !DogtagCaseHostContract.TryRollbackOwnedCaseAddition(sharedHost, firstAuthority))
            throw new InvalidOperationException("Dogtag host concurrency regression failed: original exact rollback authority could not be consumed after ABA rejection.");

        HashSet<MongoId> freshAuthority = DogtagCaseHostContract.CaptureRollbackBaseline(sharedHost);
        if (!sharedHost.Add(caseTpl) || !DogtagCaseHostContract.TryRollbackOwnedCaseAddition(sharedHost, freshAuthority))
            throw new InvalidOperationException("Dogtag host concurrency regression failed: exact host did not admit a fresh rollback transaction after the prior authority was consumed.");

        // Metadata-only abandon is only justified by the exact concurrent-commit
        // explanation for Add(case)==false: pinned baseline + exact case and nothing
        // else. Foreign drift must not silently consume the real pre-add authority.
        var abandonHost = new HashSet<MongoId> { bear, usec };
        HashSet<MongoId> abandonAuthority = DogtagCaseHostContract.CaptureRollbackBaseline(abandonHost);
        var foreign = new MongoId("5c093ca986f7740a1867ab12");
        abandonHost.Add(caseTpl);
        abandonHost.Add(foreign);
        if (DogtagCaseHostContract.TryAbandonRollbackAuthority(abandonHost, abandonAuthority))
            throw new InvalidOperationException("Dogtag host concurrency regression failed: foreign-drift host incorrectly consumed metadata-only abandon authority.");
        abandonHost.Remove(foreign);
        if (!DogtagCaseHostContract.TryAbandonRollbackAuthority(abandonHost, abandonAuthority))
            throw new InvalidOperationException("Dogtag host concurrency regression failed: exact baseline-plus-case concurrent commit could not abandon metadata-only authority after a rejected drift attempt.");
        if (DogtagCaseHostContract.TryAbandonRollbackAuthority(abandonHost, abandonAuthority))
            throw new InvalidOperationException("Dogtag host concurrency regression failed: consumed metadata-only abandon authority was reusable.");

        // A conflicting parallel-era vanilla snapshot is still rejected after the
        // exact baseline is established; synchronization must never turn ambiguity
        // into last-writer-wins behavior.
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