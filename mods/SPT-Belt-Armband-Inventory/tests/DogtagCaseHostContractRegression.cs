using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SPTarkov.Server.Core.Models.Common;
using SPTBeltArmbandInventory;
using SPTBeltArmbandInventory.Server;

internal static class DogtagCaseHostContractRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var vanillaA = new MongoId("59f32bb586f774757e1e8442");
        var vanillaB = new MongoId("59f32c3b86f77472a31742f0");
        var caseTpl = new MongoId(RuntimeIdentity.DogtagCaseItemId);
        var foreignOwnedTpl = new MongoId(RuntimeIdentity.CandidateItemId);

        ExpectFailure(
            () => DogtagCaseHostContract.CaptureVanillaEntries(new[] { vanillaA, caseTpl }),
            "pre-mutation snapshot must reject a B&A&HB-owned template presented as vanilla/foreign");

        DogtagCaseHostContract.CaptureVanillaEntries(new[] { vanillaA, vanillaB });
        if (DogtagCaseHostContract.CapturedVanillaEntryCount != 2)
            throw new InvalidOperationException("Dogtag host regression failed: exact pre-mutation vanilla snapshot count was not retained.");

        DogtagCaseHostContract.RequirePreserved(new HashSet<MongoId> { vanillaA, vanillaB, caseTpl });

        ExpectFailure(
            () => DogtagCaseHostContract.RequirePreserved(new HashSet<MongoId> { vanillaA, caseTpl }),
            "removing one captured vanilla entry must fail even though another non-case entry survives");

        ExpectFailure(
            () => DogtagCaseHostContract.RequirePreserved(new HashSet<MongoId> { vanillaA, vanillaB, caseTpl, foreignOwnedTpl }),
            "another B&A&HB-owned product must be rejected by the reusable Dogtag host contract itself");

        ExpectFailure(
            () => DogtagCaseHostContract.CaptureVanillaEntries(new[] { vanillaA }),
            "a second, different preload snapshot must be rejected as an ambiguous host contract");

        // Idempotent re-capture of the exact same set is safe and must not depend
        // on enumeration order.
        DogtagCaseHostContract.CaptureVanillaEntries(new[] { vanillaB, vanillaA });
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
