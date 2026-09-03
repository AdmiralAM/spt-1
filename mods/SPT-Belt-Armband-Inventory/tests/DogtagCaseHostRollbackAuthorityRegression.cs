using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using SPTarkov.Server.Core.Models.Common;
using SPTBeltArmbandInventory.Server;

internal static class DogtagCaseHostRollbackAuthorityRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var bear = new MongoId(DogtagCaseHostContract.BearDogtagTemplateId);
        var usec = new MongoId(DogtagCaseHostContract.UsecDogtagTemplateId);
        var caseTpl = new MongoId(DogtagCaseItem.TemplateId);
        var foreign = new MongoId("5448bf274bdc2dfc2f8b456a");

        var vanilla = new HashSet<MongoId> { bear, usec };
        DogtagCaseHostContract.CaptureVanillaEntries(vanilla);

        var clean = new HashSet<MongoId>(vanilla);
        HashSet<MongoId> cleanBaseline = DogtagCaseHostContract.CaptureRollbackBaseline(clean);
        clean.Add(caseTpl);
        if (!DogtagCaseHostContract.TryRollbackOwnedCaseAddition(clean, cleanBaseline)
            || !clean.SetEquals(cleanBaseline))
            throw new InvalidOperationException("Dogtag host rollback regression failed: exact owned add must roll back to the detached pre-commit snapshot.");
        clean.Add(caseTpl);
        if (DogtagCaseHostContract.TryRollbackOwnedCaseAddition(clean, cleanBaseline) || !clean.Contains(caseTpl))
            throw new InvalidOperationException("Dogtag host rollback regression failed: consumed rollback authority must not be reusable for a second removal.");

        var abandonHost = new HashSet<MongoId>(vanilla);
        HashSet<MongoId> abandonBaseline = DogtagCaseHostContract.CaptureRollbackBaseline(abandonHost);
        if (!DogtagCaseHostContract.TryAbandonRollbackAuthority(abandonHost, abandonBaseline))
            throw new InvalidOperationException("Dogtag host rollback regression failed: exact unused pre-add authority must be explicitly abandonable without host mutation.");
        if (!abandonHost.SetEquals(vanilla)
            || DogtagCaseHostContract.TryAbandonRollbackAuthority(abandonHost, abandonBaseline))
            throw new InvalidOperationException("Dogtag host rollback regression failed: abandon must be no-mutation and single-consumer.");
        HashSet<MongoId> freshAfterAbandon = DogtagCaseHostContract.CaptureRollbackBaseline(abandonHost);
        if (!DogtagCaseHostContract.TryAbandonRollbackAuthority(abandonHost, freshAfterAbandon))
            throw new InvalidOperationException("Dogtag host rollback regression failed: exact host capture gate was not released for a fresh transaction after abandon.");

        var abandonReplacementHost = new HashSet<MongoId>(vanilla);
        HashSet<MongoId> abandonReplacementBaseline = DogtagCaseHostContract.CaptureRollbackBaseline(abandonReplacementHost);
        var abandonValueIdenticalReplacement = new HashSet<MongoId>(abandonReplacementHost);
        if (DogtagCaseHostContract.TryAbandonRollbackAuthority(abandonValueIdenticalReplacement, abandonReplacementBaseline))
            throw new InvalidOperationException("Dogtag host rollback regression failed: replacement host must not inherit metadata-abandon authority.");
        if (!DogtagCaseHostContract.TryAbandonRollbackAuthority(abandonReplacementHost, abandonReplacementBaseline))
            throw new InvalidOperationException("Dogtag host rollback regression failed: rejected replacement-host abandon must not consume the real exact-host authority.");

        var replacementHost = new HashSet<MongoId>(vanilla);
        HashSet<MongoId> replacementBaseline = DogtagCaseHostContract.CaptureRollbackBaseline(replacementHost);
        replacementHost.Add(caseTpl);
        var valueIdenticalReplacement = new HashSet<MongoId>(replacementHost);
        if (DogtagCaseHostContract.TryRollbackOwnedCaseAddition(valueIdenticalReplacement, replacementBaseline))
            throw new InvalidOperationException("Dogtag host rollback regression failed: a value-identical replacement host must not inherit exact-reference rollback authority.");
        if (!valueIdenticalReplacement.Contains(caseTpl))
            throw new InvalidOperationException("Dogtag host rollback regression failed: replacement-host rejection must not mutate the foreign/replacement object.");
        if (!DogtagCaseHostContract.TryRollbackOwnedCaseAddition(replacementHost, replacementBaseline)
            || replacementHost.Contains(caseTpl))
            throw new InvalidOperationException("Dogtag host rollback regression failed: rejected foreign-host rollback must not consume the real exact-host token.");

        var tamperedBaselineHost = new HashSet<MongoId>(vanilla);
        HashSet<MongoId> tamperedBaseline = DogtagCaseHostContract.CaptureRollbackBaseline(tamperedBaselineHost);
        tamperedBaselineHost.Add(caseTpl);
        tamperedBaseline.Add(foreign);
        if (DogtagCaseHostContract.TryRollbackOwnedCaseAddition(tamperedBaselineHost, tamperedBaseline))
            throw new InvalidOperationException("Dogtag host rollback regression failed: caller mutation of the detached rollback baseline must invalidate authority.");
        if (!tamperedBaselineHost.Contains(caseTpl))
            throw new InvalidOperationException("Dogtag host rollback regression failed: tampered-baseline rejection must leave the live committed host untouched.");

        var drifted = new HashSet<MongoId>(vanilla);
        HashSet<MongoId> driftBaseline = DogtagCaseHostContract.CaptureRollbackBaseline(drifted);
        drifted.Add(caseTpl);
        drifted.Add(foreign);
        if (DogtagCaseHostContract.TryRollbackOwnedCaseAddition(drifted, driftBaseline))
            throw new InvalidOperationException("Dogtag host rollback regression failed: foreign/current host drift must make owned rollback ambiguous.");
        if (!drifted.Contains(caseTpl) || !drifted.Contains(foreign))
            throw new InvalidOperationException("Dogtag host rollback regression failed: ambiguous rollback must not blindly rewrite current/foreign host state.");

        var missingCase = new HashSet<MongoId>(vanilla);
        HashSet<MongoId> missingBaseline = DogtagCaseHostContract.CaptureRollbackBaseline(missingCase);
        if (DogtagCaseHostContract.TryRollbackOwnedCaseAddition(missingCase, missingBaseline))
            throw new InvalidOperationException("Dogtag host rollback regression failed: rollback cannot succeed when the exact owned committed shape is absent.");

        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Dogtag host rollback regression failed: module root could not be resolved.");
        string source = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseItem.cs"));
        int commit = source.IndexOf("private void CommitDogtagSlotExposure", StringComparison.Ordinal);
        int next = source.IndexOf("public static void RequireCanonicalRegisteredTemplate", commit, StringComparison.Ordinal);
        if (commit < 0 || next <= commit)
            throw new InvalidOperationException("Dogtag host rollback regression failed: commit boundary is missing.");
        string body = source.Substring(commit, next - commit);
        Require(body, "CaptureRollbackBaseline(filter)", "owned add must capture a detached pre-commit host baseline");
        Require(body, "TryRollbackOwnedCaseAddition(filter, rollbackBaseline)", "exception rollback must prove exact owned authority before mutation");
        Require(body, "TryAbandonRollbackAuthority(filter, rollbackBaseline)", "non-owned/failed add must explicitly abandon exact pre-add metadata authority");
        Require(body, "bool authorityReleased = addedHere", "exception cleanup must select rollback versus metadata-only abandon by exact ownership");
        Require(body, "ambiguous/foreign current host state is not blindly rewritten", "ambiguous rollback must remain explicitly fail-closed");
        if (body.Contains("filter.Remove(DogtagCaseTpl);", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag host rollback regression failed: unconditional value-only removal must not return.");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string server = Path.Combine(current.FullName, "server", "DogtagCaseItem.cs");
            if (File.Exists(server)) return current.FullName;
            current = current.Parent;
        }
        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "server", "DogtagCaseItem.cs"))) return nested;
            current = current.Parent;
        }
        return null;
    }

    private static void Require(string source, string token, string message)
    {
        if (!source.Contains(token, StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag host rollback regression failed: " + message + ".");
    }
}
