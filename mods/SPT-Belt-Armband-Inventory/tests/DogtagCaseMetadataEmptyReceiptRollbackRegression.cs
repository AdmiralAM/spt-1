using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class DogtagCaseMetadataEmptyReceiptRollbackRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Dogtag metadata-empty receipt rollback regression failed: module root could not be resolved.");

        string item = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseItem.cs"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        int receipt = item.IndexOf("private sealed class DogtagHostCommitReceipt", StringComparison.Ordinal);
        int rollback = receipt < 0 ? -1 : item.IndexOf("internal bool TryRollback()", receipt, StringComparison.Ordinal);
        int rollbackEnd = rollback < 0 ? -1 : item.IndexOf("    }\n\n    public Task OnLoadAsync", rollback, StringComparison.Ordinal);
        if (receipt < 0 || rollback < 0 || rollbackEnd < 0)
            throw new InvalidOperationException("Dogtag metadata-empty receipt rollback regression failed: receipt rollback region could not be resolved.");

        string region = item.Substring(rollback, rollbackEnd - rollback);
        int tryBlock = region.IndexOf("try", StringComparison.Ordinal);
        int hostProof = region.IndexOf("owner.RequireLiveDogtagHostIdentity(boundary);", StringComparison.Ordinal);
        int committedProof = region.IndexOf("DogtagCaseHostContract.RequireCommitted(boundary.Filter);", StringComparison.Ordinal);
        int nullAuthority = region.IndexOf("if (rollbackBaseline == null)", StringComparison.Ordinal);
        int consume = nullAuthority < 0 ? -1 : region.IndexOf("consumed = true;", nullAuthority, StringComparison.Ordinal);
        int ownedRollback = region.IndexOf("DogtagCaseHostContract.TryRollbackOwnedCaseAddition(boundary.Filter, rollbackBaseline)", StringComparison.Ordinal);

        if (tryBlock < 0 || hostProof < 0 || committedProof < 0 || nullAuthority < 0 || consume < 0 || ownedRollback < 0
            || !(tryBlock < hostProof
                && hostProof < committedProof
                && committedProof < nullAuthority
                && nullAuthority < consume
                && consume < ownedRollback))
            throw new InvalidOperationException("Dogtag metadata-empty receipt rollback regression failed: metadata-empty failure cleanup must re-prove exact live host identity + committed shape before it can report success; owned rollback must remain downstream of the same proofs.");

        string beforeTry = region.Substring(0, tryBlock);
        if (beforeTry.Contains("rollbackBaseline == null", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag metadata-empty receipt rollback regression failed: metadata-empty receipt still has an unconditional success path before host reproof.");
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
