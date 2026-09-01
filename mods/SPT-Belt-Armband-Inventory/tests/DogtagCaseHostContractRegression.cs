using System;
using System.Collections.Generic;
using System.IO;
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
            () => DogtagCaseHostContract.CaptureVanillaEntries(new[] { vanillaA, vanillaB, foreignVanillaTpl }),
            "a second, different preload snapshot must be rejected as an ambiguous host contract");

        DogtagCaseHostContract.CaptureVanillaEntries(new[] { vanillaB, vanillaA });
        RequireLiveHostIdentitySourceContract();
    }

    internal static void RunConcurrentCommittedVerificationRegression()
    {
        var vanillaA = new MongoId(DogtagCaseHostContract.BearDogtagTemplateId);
        var vanillaB = new MongoId(DogtagCaseHostContract.UsecDogtagTemplateId);
        var laterForeignTpl = new MongoId("5c093db286f7740a1b2617e3");
        var caseTpl = new MongoId(RuntimeIdentity.DogtagCaseItemId);

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

    private static void RequireLiveHostIdentitySourceContract()
    {
        string sourcePath = Path.Combine("mods", "SPT-Belt-Armband-Inventory", "server", "DogtagCaseItem.cs");
        string source = File.ReadAllText(sourcePath);
        string[] required =
        {
            "new DogtagHostBoundary(inventory, slots[0], groups[0], hostFilter)",
            "ReferenceEquals(liveInventory, boundary.Inventory)",
            "ReferenceEquals(liveSlots[0], boundary.Slot)",
            "ReferenceEquals(liveGroups[0], boundary.FilterGroup)",
            "ReferenceEquals(liveGroups[0].Filter, boundary.Filter)",
            "RequireLiveDogtagHostIdentity(boundary);\n        DogtagCaseHostContract.RequirePreserved(filter);",
            "DogtagCaseHostContract.RequireCommitted(filter);\n            RequireLiveDogtagHostIdentity(boundary);"
        };

        foreach (string contract in required)
            if (!source.Contains(contract, StringComparison.Ordinal))
                throw new InvalidOperationException("Dogtag host regression failed: live preload-host identity contract missing: " + contract);
    }

    private static void ExpectFailure(Action action, string message)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Dogtag host regression failed: " + message);
    }
}
