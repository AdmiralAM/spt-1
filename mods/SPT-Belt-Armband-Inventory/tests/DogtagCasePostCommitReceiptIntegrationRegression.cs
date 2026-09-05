using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class DogtagCasePostCommitReceiptIntegrationRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Dogtag post-commit receipt integration regression failed: module root could not be resolved.");

        string item = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseItem.cs"));

        int receiptClass = item.IndexOf("private sealed class DogtagHostCommitReceipt", StringComparison.Ordinal);
        int accept = receiptClass < 0 ? -1 : item.IndexOf("internal void Accept()", receiptClass, StringComparison.Ordinal);
        int acceptHostProof = accept < 0 ? -1 : item.IndexOf("owner.RequireLiveDogtagHostIdentity(boundary);", accept, StringComparison.Ordinal);
        int acceptCommitted = acceptHostProof < 0 ? -1 : item.IndexOf("DogtagCaseHostContract.RequireCommitted(boundary.Filter);", acceptHostProof, StringComparison.Ordinal);
        int nullAuthorityBranch = acceptCommitted < 0 ? -1 : item.IndexOf("if (rollbackBaseline == null)", acceptCommitted, StringComparison.Ordinal);
        int abandon = nullAuthorityBranch < 0 ? -1 : item.IndexOf("DogtagCaseHostContract.TryAbandonRollbackAuthority(boundary.Filter, rollbackBaseline)", nullAuthorityBranch, StringComparison.Ordinal);
        int rollback = abandon < 0 ? -1 : item.IndexOf("internal bool TryRollback()", abandon, StringComparison.Ordinal);
        int rollbackHostProof = rollback < 0 ? -1 : item.IndexOf("owner.RequireLiveDogtagHostIdentity(boundary);", rollback, StringComparison.Ordinal);
        int ownedRollback = rollbackHostProof < 0 ? -1 : item.IndexOf("DogtagCaseHostContract.TryRollbackOwnedCaseAddition(boundary.Filter, rollbackBaseline)", rollbackHostProof, StringComparison.Ordinal);

        if (receiptClass < 0 || accept < 0 || acceptHostProof < 0 || acceptCommitted < 0 || nullAuthorityBranch < 0 || abandon < 0
            || rollback < 0 || rollbackHostProof < 0 || ownedRollback < 0
            || !(receiptClass < accept && accept < acceptHostProof && acceptHostProof < acceptCommitted
                && acceptCommitted < nullAuthorityBranch && nullAuthorityBranch < abandon
                && abandon < rollback && rollback < rollbackHostProof && rollbackHostProof < ownedRollback))
            throw new InvalidOperationException("Dogtag post-commit receipt integration regression failed: every acceptance path, including metadata-empty/pre-existing Case, must re-prove exact live host + committed shape before success; owned acceptance then consumes metadata and failed proof retains exact-owned rollback authority.");

        int commitSignature = item.IndexOf("private DogtagHostCommitReceipt CommitDogtagSlotExposure", StringComparison.Ordinal);
        int returnReceipt = commitSignature < 0 ? -1 : item.IndexOf("return new DogtagHostCommitReceipt(this, boundary, addedHere ? rollbackBaseline : null);", commitSignature, StringComparison.Ordinal);
        if (commitSignature < 0 || returnReceipt < 0 || returnReceipt <= commitSignature)
            throw new InvalidOperationException("Dogtag post-commit receipt integration regression failed: host commit must return a receipt carrying only exact locally-owned add authority.");

        int existingReceipt = item.IndexOf("DogtagHostCommitReceipt receipt = CommitDogtagSlotExposure(dogtagHost, cancellationToken);", StringComparison.Ordinal);
        int existingCancelAfterCommit = existingReceipt < 0 ? -1 : item.IndexOf("cancellationToken.ThrowIfCancellationRequested();", existingReceipt, StringComparison.Ordinal);
        int existingFinalProof = existingCancelAfterCommit < 0 ? -1 : item.IndexOf("canonicalLease.RequireCurrent(templateTable, source);", existingCancelAfterCommit, StringComparison.Ordinal);
        int existingCancelBeforeAccept = existingFinalProof < 0 ? -1 : item.IndexOf("cancellationToken.ThrowIfCancellationRequested();", existingFinalProof, StringComparison.Ordinal);
        int existingAccept = existingCancelBeforeAccept < 0 ? -1 : item.IndexOf("receipt.Accept();", existingCancelBeforeAccept, StringComparison.Ordinal);
        int existingFailureRollback = existingAccept < 0 ? -1 : item.IndexOf("if (!receipt.TryRollback())", existingAccept, StringComparison.Ordinal);
        if (existingReceipt < 0 || existingCancelAfterCommit < 0 || existingFinalProof < 0 || existingCancelBeforeAccept < 0 || existingAccept < 0 || existingFailureRollback < 0
            || !(existingReceipt < existingCancelAfterCommit && existingCancelAfterCommit < existingFinalProof
                && existingFinalProof < existingCancelBeforeAccept && existingCancelBeforeAccept < existingAccept
                && existingAccept < existingFailureRollback))
            throw new InvalidOperationException("Dogtag post-commit receipt integration regression failed: retained-template path must commit -> cancellation proof -> final canonical proof -> cancellation proof -> host-reproved accept, with exact-owned rollback on any pre-accept failure.");

        int createdBoundary = item.IndexOf("if (!templateTable.Items.TryGetValue(DogtagCaseTpl, out var created))", StringComparison.Ordinal);
        int createdCancelBeforeCommit = createdBoundary < 0 ? -1 : item.IndexOf("cancellationToken.ThrowIfCancellationRequested();", createdBoundary, StringComparison.Ordinal);
        int createdReceipt = createdCancelBeforeCommit < 0 ? -1 : item.IndexOf("DogtagHostCommitReceipt createdReceipt = CommitDogtagSlotExposure(dogtagHost, cancellationToken);", createdCancelBeforeCommit, StringComparison.Ordinal);
        int createdCancelAfterCommit = createdReceipt < 0 ? -1 : item.IndexOf("cancellationToken.ThrowIfCancellationRequested();", createdReceipt, StringComparison.Ordinal);
        int createdFinalProof = createdCancelAfterCommit < 0 ? -1 : item.IndexOf("canonicalLease.RequireCurrent(templateTable, source);", createdCancelAfterCommit, StringComparison.Ordinal);
        int createdCancelBeforeAccept = createdFinalProof < 0 ? -1 : item.IndexOf("cancellationToken.ThrowIfCancellationRequested();", createdFinalProof, StringComparison.Ordinal);
        int createdAccept = createdCancelBeforeAccept < 0 ? -1 : item.IndexOf("createdReceipt.Accept();", createdCancelBeforeAccept, StringComparison.Ordinal);
        int createdFailureRollback = createdAccept < 0 ? -1 : item.IndexOf("if (!createdReceipt.TryRollback())", createdAccept, StringComparison.Ordinal);
        if (createdBoundary < 0 || createdCancelBeforeCommit < 0 || createdReceipt < 0 || createdCancelAfterCommit < 0
            || createdFinalProof < 0 || createdCancelBeforeAccept < 0 || createdAccept < 0 || createdFailureRollback < 0
            || !(createdBoundary < createdCancelBeforeCommit && createdCancelBeforeCommit < createdReceipt
                && createdReceipt < createdCancelAfterCommit && createdCancelAfterCommit < createdFinalProof
                && createdFinalProof < createdCancelBeforeAccept && createdCancelBeforeAccept < createdAccept
                && createdAccept < createdFailureRollback))
            throw new InvalidOperationException("Dogtag post-commit receipt integration regression failed: created-template path must honor caller cancellation before host mutation and across the receipt window, then rollback exact-owned host exposure on any pre-accept failure.");

        if (item.Contains("CommitDogtagSlotExposure(dogtagHost, CancellationToken.None)", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag post-commit receipt integration regression failed: created-template host publication must not bypass the caller cancellation contract with CancellationToken.None.");

        string commitRegion = item.Substring(commitSignature, item.IndexOf("public static void RequireCanonicalRegisteredTemplate", commitSignature, StringComparison.Ordinal) - commitSignature);
        if (commitRegion.Contains("TryAbandonRollbackAuthority(filter, rollbackBaseline)", StringComparison.Ordinal)
            && !commitRegion.Contains("if (!addedHere && rollbackBaseline != null)", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag post-commit receipt integration regression failed: owned-add authority must not be consumed inside host commit before the final caller proof.");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string direct = Path.Combine(current.FullName, "server", "DogtagCaseItem.cs");
            if (File.Exists(direct)) return current.FullName;
            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "server", "DogtagCaseItem.cs"))) return nested;
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string direct = Path.Combine(current.FullName, "server", "DogtagCaseItem.cs");
            if (File.Exists(direct)) return current.FullName;
            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "server", "DogtagCaseItem.cs"))) return nested;
            current = current.Parent;
        }
        return null;
    }
}
