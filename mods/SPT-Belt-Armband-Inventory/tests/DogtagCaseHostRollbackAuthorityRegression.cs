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
        abandonHost.Add(caseTpl);
        if (!DogtagCaseHostContract.TryAbandonRollbackAuthority(abandonHost, abandonBaseline))
            throw new InvalidOperationException("Dogtag host rollback regression failed: exact baseline-plus-Case concurrent-commit shape must be metadata-only abandonable without host mutation.");
        if (!abandonHost.Contains(caseTpl) || abandonHost.Count != vanilla.Count + 1
            || DogtagCaseHostContract.TryAbandonRollbackAuthority(abandonHost, abandonBaseline))
            throw new InvalidOperationException("Dogtag host rollback regression failed: abandon must preserve the exact committed host and remain single-consumer.");
        abandonHost.Remove(caseTpl);
        HashSet<MongoId> freshAfterAbandon = DogtagCaseHostContract.CaptureRollbackBaseline(abandonHost);
        abandonHost.Add(caseTpl);
        if (!DogtagCaseHostContract.TryAbandonRollbackAuthority(abandonHost, freshAfterAbandon))
            throw new InvalidOperationException("Dogtag host rollback regression failed: exact host capture gate was not released for a fresh transaction after proven abandon.");

        var abandonReplacementHost = new HashSet<MongoId>(vanilla);
        HashSet<MongoId> abandonReplacementBaseline = DogtagCaseHostContract.CaptureRollbackBaseline(abandonReplacementHost);
        abandonReplacementHost.Add(caseTpl);
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

        // A failed rollback must not consume ActiveRollbackHosts. Otherwise an
        // external value-ABA back to the original baseline could open a second
        // rollback capture while the first receipt is still live.
        var driftAbaHost = new HashSet<MongoId>(vanilla);
        HashSet<MongoId> driftAbaBaseline = DogtagCaseHostContract.CaptureRollbackBaseline(driftAbaHost);
        driftAbaHost.Add(caseTpl);
        driftAbaHost.Add(foreign);
        if (DogtagCaseHostContract.TryRollbackOwnedCaseAddition(driftAbaHost, driftAbaBaseline))
            throw new InvalidOperationException("Dogtag host rollback regression failed: drifted rollback unexpectedly succeeded before ABA gate proof.");
        driftAbaHost.Remove(foreign);
        driftAbaHost.Remove(caseTpl);
        bool secondCaptureRejected = false;
        try
        {
            _ = DogtagCaseHostContract.CaptureRollbackBaseline(driftAbaHost);
        }
        catch (InvalidOperationException)
        {
            secondCaptureRejected = true;
        }
        if (!secondCaptureRejected)
            throw new InvalidOperationException("Dogtag host rollback regression failed: failed rollback consumed the exact-host capture gate and allowed value-ABA to mint a second authority.");
        driftAbaHost.Add(caseTpl);
        if (!DogtagCaseHostContract.TryRollbackOwnedCaseAddition(driftAbaHost, driftAbaBaseline)
            || !driftAbaHost.SetEquals(vanilla))
            throw new InvalidOperationException("Dogtag host rollback regression failed: original rollback authority must remain retryable after drift clears to the exact committed shape.");
        HashSet<MongoId> freshAfterRecoveredRollback = DogtagCaseHostContract.CaptureRollbackBaseline(driftAbaHost);
        driftAbaHost.Add(caseTpl);
        if (!DogtagCaseHostContract.TryAbandonRollbackAuthority(driftAbaHost, freshAfterRecoveredRollback))
            throw new InvalidOperationException("Dogtag host rollback regression failed: capture gate must reopen only after exact rollback and baseline restoration are proven.");

        var missingCase = new HashSet<MongoId>(vanilla);
        HashSet<MongoId> missingBaseline = DogtagCaseHostContract.CaptureRollbackBaseline(missingCase);
        if (DogtagCaseHostContract.TryRollbackOwnedCaseAddition(missingCase, missingBaseline))
            throw new InvalidOperationException("Dogtag host rollback regression failed: rollback cannot succeed when the exact owned committed shape is absent.");

        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Dogtag host rollback regression failed: module root could not be resolved.");
        string source = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseItem.cs"));
        int commit = source.IndexOf("private DogtagHostCommitReceipt CommitDogtagSlotExposure", StringComparison.Ordinal);
        int next = commit < 0 ? -1 : source.IndexOf("public static void RequireCanonicalRegisteredTemplate", commit, StringComparison.Ordinal);
        if (commit < 0 || next <= commit)
            throw new InvalidOperationException("Dogtag host rollback regression failed: receipt-returning commit boundary is missing.");
        string body = source.Substring(commit, next - commit);
        Require(body, "CaptureRollbackBaseline(filter)", "owned add must capture a detached pre-commit host baseline");
        Require(body, "TryRollbackOwnedCaseAddition(filter, rollbackBaseline)", "in-transaction exception rollback must prove exact owned authority before mutation");
        Require(body, "TryAbandonRollbackAuthority(filter, rollbackBaseline)", "non-owned/failed add must explicitly abandon exact pre-add metadata authority");
        Require(body, "bool authorityReleased = addedHere", "exception cleanup must select rollback versus metadata-only abandon by exact ownership");
        Require(body, "return new DogtagHostCommitReceipt(this, boundary, addedHere ? rollbackBaseline : null);", "successful owned add must hand exact rollback authority to the post-commit receipt instead of consuming it early");
        Require(body, "ambiguous/foreign current host state is not blindly rewritten", "ambiguous rollback must remain explicitly fail-closed");
        if (body.Contains("filter.Remove(DogtagCaseTpl);", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag host rollback regression failed: unconditional value-only removal must not return.");

        string hostContract = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseHostContract.cs"));
        int rollback = hostContract.IndexOf("public static bool TryRollbackOwnedCaseAddition", StringComparison.Ordinal);
        int rollbackEnd = rollback < 0 ? -1 : hostContract.IndexOf("private static HashSet<MongoId> SnapshotCurrentFilter", rollback, StringComparison.Ordinal);
        if (rollback < 0 || rollbackEnd <= rollback)
            throw new InvalidOperationException("Dogtag host rollback regression failed: host rollback contract boundary is missing.");
        string rollbackBody = hostContract.Substring(rollback, rollbackEnd - rollback);
        int afterProof = rollbackBody.IndexOf("if (!after.SetEquals(baseline))", StringComparison.Ordinal);
        int consume = rollbackBody.IndexOf("RollbackAuthorities.Remove(preCommitSnapshot);", StringComparison.Ordinal);
        if (afterProof < 0 || consume <= afterProof)
            throw new InvalidOperationException("Dogtag host rollback regression failed: rollback/capture authority must be consumed only after exact baseline restoration is proven.");

        int receiptStart = source.IndexOf("private sealed class DogtagHostCommitReceipt", StringComparison.Ordinal);
        int receiptEnd = receiptStart < 0 ? -1 : source.IndexOf("public Task OnLoadAsync", receiptStart, StringComparison.Ordinal);
        if (receiptStart < 0 || receiptEnd <= receiptStart)
            throw new InvalidOperationException("Dogtag host rollback regression failed: post-commit receipt boundary is missing.");
        string receipt = source.Substring(receiptStart, receiptEnd - receiptStart);
        Require(receipt, "TryAbandonRollbackAuthority(boundary.Filter, rollbackBaseline)", "successful final publication proof must consume exact owned rollback metadata without removing the Case");
        Require(receipt, "TryRollbackOwnedCaseAddition(boundary.Filter, rollbackBaseline)", "failed final publication proof must retain exact-owned rollback capability");
        Require(receipt, "owner.RequireLiveDogtagHostIdentity(boundary);", "post-commit accept/rollback must remain bound to the exact captured live host identity");
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