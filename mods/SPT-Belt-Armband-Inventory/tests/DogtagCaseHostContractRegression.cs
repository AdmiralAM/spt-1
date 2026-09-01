using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
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
        var laterForeignTpl = new MongoId("5c093db286f7740a1b2617e3");
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

        DogtagCaseHostContract.RequirePreserved(new HashSet<MongoId> { vanillaA, vanillaB, laterForeignTpl });
        DogtagCaseHostContract.RequireCommitted(new HashSet<MongoId> { vanillaA, vanillaB, laterForeignTpl, caseTpl });

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

        DogtagCaseHostContract.CaptureVanillaEntries(new[] { vanillaB, vanillaA });

        ExerciseAtomicExposure(vanillaA, vanillaB, caseTpl, foreignOwnedTpl, laterForeignTpl);
    }

    internal static void RunConcurrentCommittedVerificationRegression()
    {
        var vanillaA = new MongoId(DogtagCaseHostContract.BearDogtagTemplateId);
        var vanillaB = new MongoId(DogtagCaseHostContract.UsecDogtagTemplateId);
        var laterForeignTpl = new MongoId("5c093db286f7740a1b2617e3");
        var caseTpl = new MongoId(RuntimeIdentity.DogtagCaseItemId);
        ExerciseConcurrentCommittedVerification(vanillaA, vanillaB, caseTpl, laterForeignTpl);
    }

    private static void ExerciseAtomicExposure(
        MongoId vanillaA,
        MongoId vanillaB,
        MongoId caseTpl,
        MongoId foreignOwnedTpl,
        MongoId laterForeignTpl)
    {
        MethodInfo commit = typeof(DogtagCaseItem).GetMethod(
            "CommitDogtagSlotExposure",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Dogtag host regression failed: atomic host commit method missing.");

        var clean = new HashSet<MongoId> { vanillaA, vanillaB };
        commit.Invoke(null, new object[] { clean, CancellationToken.None });
        DogtagCaseHostContract.RequireCommitted(clean);
        if (!clean.Contains(caseTpl) || !clean.Contains(vanillaA) || !clean.Contains(vanillaB))
            throw new InvalidOperationException("Dogtag host regression failed: atomic exposure did not append only the exact case while preserving captured vanilla entries.");

        int count = clean.Count;
        commit.Invoke(null, new object[] { clean, CancellationToken.None });
        DogtagCaseHostContract.RequireCommitted(clean);
        if (clean.Count != count)
            throw new InvalidOperationException("Dogtag host regression failed: repeated atomic exposure changed the host set.");

        var extended = new HashSet<MongoId> { vanillaA, vanillaB, laterForeignTpl };
        commit.Invoke(null, new object[] { extended, CancellationToken.None });
        DogtagCaseHostContract.RequireCommitted(extended);
        if (!extended.Contains(laterForeignTpl) || !extended.Contains(caseTpl) || extended.Count != 4)
            throw new InvalidOperationException("Dogtag host regression failed: atomic exposure did not preserve a compatible post-capture foreign host addition.");

        var contaminated = new HashSet<MongoId> { vanillaA, vanillaB, foreignOwnedTpl };
        try
        {
            commit.Invoke(null, new object[] { contaminated, CancellationToken.None });
            throw new InvalidOperationException("Dogtag host regression failed: contaminated host unexpectedly accepted atomic case exposure.");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException)
        {
            if (contaminated.Contains(caseTpl))
                throw new InvalidOperationException("Dogtag host regression failed: failed atomic exposure left the Dogtag Case partially appended.");
        }

        // Cancellation before mutation is an exact no-op.
        var canceledBefore = new HashSet<MongoId> { vanillaA, vanillaB };
        using (var cts = new CancellationTokenSource())
        {
            cts.Cancel();
            ExpectInvocationCancellation(() => commit.Invoke(null, new object[] { canceledBefore, cts.Token }));
        }
        if (canceledBefore.Contains(caseTpl) || canceledBefore.Count != 2)
            throw new InvalidOperationException("Dogtag host regression failed: pre-cancelled exposure mutated the caller-owned host.");

        // A pre-existing exact case is not owned by the current invocation and
        // therefore must survive cancellation/rollback unchanged.
        var preexisting = new HashSet<MongoId> { vanillaA, vanillaB, caseTpl };
        using (var cts = new CancellationTokenSource())
        {
            cts.Cancel();
            ExpectInvocationCancellation(() => commit.Invoke(null, new object[] { preexisting, cts.Token }));
        }
        if (!preexisting.Contains(caseTpl) || preexisting.Count != 3)
            throw new InvalidOperationException("Dogtag host regression failed: cancellation removed a pre-existing exact Dogtag Case entry.");
    }

    private static void ExerciseConcurrentCommittedVerification(
        MongoId vanillaA,
        MongoId vanillaB,
        MongoId caseTpl,
        MongoId laterForeignTpl)
    {
        var plain = new HashSet<MongoId> { vanillaA, vanillaB, caseTpl };
        var extended = new HashSet<MongoId> { vanillaA, vanillaB, laterForeignTpl, caseTpl };
        Exception leftFailure = null;
        Exception rightFailure = null;

        var left = new Thread(() =>
        {
            try { DogtagCaseHostContract.RequireCommitted(plain); }
            catch (Exception exception) { leftFailure = exception; }
        });
        var right = new Thread(() =>
        {
            try { DogtagCaseHostContract.RequireCommitted(extended); }
            catch (Exception exception) { rightFailure = exception; }
        });

        left.Start();
        right.Start();
        if (!left.Join(TimeSpan.FromSeconds(5)) || !right.Join(TimeSpan.FromSeconds(5)))
            throw new InvalidOperationException("Dogtag host regression failed: concurrent committed verification exceeded bounded time.");
        if (leftFailure != null || rightFailure != null)
            throw new InvalidOperationException("Dogtag host regression failed: concurrent committed verification was not read-only/deterministic.", leftFailure ?? rightFailure);
        if (DogtagCaseHostContract.CapturedVanillaEntryCount != 2)
            throw new InvalidOperationException("Dogtag host regression failed: concurrent committed verification mutated the captured preload baseline.");
        if (plain.Count != 3 || extended.Count != 4 || !extended.Contains(laterForeignTpl))
            throw new InvalidOperationException("Dogtag host regression failed: verification mutated a caller-owned compatible host set.");
    }

    private static void ExpectInvocationCancellation(Action action)
    {
        try
        {
            action();
        }
        catch (TargetInvocationException exception) when (exception.InnerException is OperationCanceledException)
        {
            return;
        }
        throw new InvalidOperationException("Dogtag host regression failed: expected cancellation was not propagated through atomic host exposure.");
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
