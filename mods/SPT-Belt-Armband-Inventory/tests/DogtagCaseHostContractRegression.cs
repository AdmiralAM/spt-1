using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTarkov.Server.Core.Models.Common;
using SPTBeltArmbandInventory;
using SPTBeltArmbandInventory.Server;

internal static class DogtagCaseHostContractRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var vanillaA = new MongoId(DogtagCaseHostContract.BearDogtagTemplateId);
        var vanillaB = new MongoId(DogtagCaseHostContract.UsecDogtagTemplateId);
        var foreignVanillaTpl = new MongoId("5c093ca986f7740a1867ab12");
        var caseTpl = new MongoId(RuntimeIdentity.DogtagCaseItemId);
        var foreignOwnedTpl = new MongoId(RuntimeIdentity.CandidateItemId);

        ExpectFailure(
            () => DogtagCaseHostContract.CaptureVanillaEntries(new[] { vanillaA, caseTpl }),
            "pre-mutation snapshot must reject a B&A&HB-owned template presented as vanilla/foreign");

        ExpectFailure(
            () => DogtagCaseHostContract.CaptureVanillaEntries(new[] { vanillaA }),
            "pre-mutation snapshot must fail closed if either canonical faction dogtag is missing");

        DogtagCaseHostContract.CaptureVanillaEntries(new[] { vanillaA, vanillaB });
        if (DogtagCaseHostContract.CapturedVanillaEntryCount != 2)
            throw new InvalidOperationException("Dogtag host regression failed: exact pre-mutation vanilla snapshot count was not retained.");

        DogtagCaseHostContract.RequirePreserved(new HashSet<MongoId> { vanillaA, vanillaB, caseTpl });
        DogtagCaseHostContract.RequireCommitted(new HashSet<MongoId> { vanillaA, vanillaB, caseTpl });

        ExpectFailure(
            () => DogtagCaseHostContract.RequireCommitted(new HashSet<MongoId> { vanillaA, vanillaB }),
            "committed host verification must reject a preserved host that lacks the exact Dogtag Case template");

        ExpectFailure(
            () => DogtagCaseHostContract.RequirePreserved(new HashSet<MongoId> { vanillaA, caseTpl }),
            "removing one captured vanilla entry must fail even though another non-case entry survives");

        ExpectFailure(
            () => DogtagCaseHostContract.RequirePreserved(new HashSet<MongoId> { vanillaA, vanillaB, caseTpl, foreignOwnedTpl }),
            "another B&A&HB-owned product must be rejected by the reusable Dogtag host contract itself");

        ExpectFailure(
            () => DogtagCaseHostContract.RequireCommitted(new HashSet<MongoId> { vanillaA, vanillaB, caseTpl, foreignOwnedTpl }),
            "committed host verification must retain ownership isolation as well as exact case presence");

        ExpectFailure(
            () => DogtagCaseHostContract.CaptureVanillaEntries(new[] { vanillaA, vanillaB, foreignVanillaTpl }),
            "a second, different preload snapshot must be rejected as an ambiguous host contract");

        // Idempotent re-capture of the exact same set is safe and must not depend
        // on enumeration order.
        DogtagCaseHostContract.CaptureVanillaEntries(new[] { vanillaB, vanillaA });

        ExerciseAtomicExposure(vanillaA, vanillaB, caseTpl, foreignOwnedTpl);
    }

    private static void ExerciseAtomicExposure(MongoId vanillaA, MongoId vanillaB, MongoId caseTpl, MongoId foreignOwnedTpl)
    {
        MethodInfo commit = typeof(DogtagCaseItem).GetMethod(
            "CommitDogtagSlotExposure",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Dogtag host regression failed: atomic host commit method missing.");

        var clean = new HashSet<MongoId> { vanillaA, vanillaB };
        commit.Invoke(null, new object[] { clean });
        DogtagCaseHostContract.RequireCommitted(clean);
        if (!clean.Contains(caseTpl) || !clean.Contains(vanillaA) || !clean.Contains(vanillaB))
            throw new InvalidOperationException("Dogtag host regression failed: atomic exposure did not append only the exact case while preserving captured vanilla entries.");

        // Repeating the commit is an exact idempotent no-op and must still pass
        // the centralized committed-state contract.
        int count = clean.Count;
        commit.Invoke(null, new object[] { clean });
        DogtagCaseHostContract.RequireCommitted(clean);
        if (clean.Count != count)
            throw new InvalidOperationException("Dogtag host regression failed: repeated atomic exposure changed the host set.");

        // A host that became contaminated after preload must fail before our append.
        // This proves fail-closed verification cannot leave a partial Dogtag Case entry.
        var contaminated = new HashSet<MongoId> { vanillaA, vanillaB, foreignOwnedTpl };
        try
        {
            commit.Invoke(null, new object[] { contaminated });
            throw new InvalidOperationException("Dogtag host regression failed: contaminated host unexpectedly accepted atomic case exposure.");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException)
        {
            if (contaminated.Contains(caseTpl))
                throw new InvalidOperationException("Dogtag host regression failed: failed atomic exposure left the Dogtag Case partially appended.");
        }
    }

    private static void ExpectFailure(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException("Dogtag host regression failed: " + message);
    }
}
