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
        int existingFinalProof = existingReceipt < 0 ? -1 : item.IndexOf("canonicalLease.RequireCurrent(templateTable, source);", existingReceipt, StringComparison.Ordinal);
        int existingAccept = existingFinalProof < 0 ? -1 : item.IndexOf("receipt.Accept();", existingFinalProof, StringComparison.Ordinal);
        int existingFailureRollback = existingAccept < 0 ? -1 : item.IndexOf("if (!receipt.TryRollback())", existingAccept, StringComparison.Ordinal);
        if (existingReceipt < 0 || existingFinalProof < 0 || existingAccept < 0 || existingFailureRollback < 0
            || !(existingReceipt < existingFinalProof && existingFinalProof < existingAccept && existingAccept < existingFailureRollback))
            throw new InvalidOperationException("Dogtag post-commit receipt integration regression failed: retained-template path must commit -> final canonical proof -> host-reproved accept, with exact-owned rollback on proof failure.");

        int createdReceipt = item.IndexOf("DogtagHostCommitReceipt createdReceipt = CommitDogtagSlotExposure(dogtagHost, CancellationToken.None);", StringComparison.Ordinal);
        int createdFinalProof = createdReceipt < 0 ? -1 : item.IndexOf("canonicalLease.RequireCurrent(templateTable, source);", createdReceipt, StringComparison.Ordinal);
        int createdAccept = createdFinalProof < 0 ? -1 : item.IndexOf("createdReceipt.Accept();", createdFinalProof, StringComparison.Ordinal);
        int createdFailureRollback = createdAccept < 0 ? -1 : item.IndexOf("if (!createdReceipt.TryRollback())", createdAccept, StringComparison.Ordinal);
        if (createdReceipt < 0 || createdFinalProof < 0 || createdAccept < 0 || createdFailureRollback < 0
            || !(createdReceipt < createdFinalProof && createdFinalProof < createdAccept && createdAccept < createdFailureRollback))
            throw new InvalidOperationException("Dogtag post-commit receipt integration regression failed: created-template path must commit -> final canonical proof -> host-reproved accept, with exact-owned rollback on proof failure.");

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
