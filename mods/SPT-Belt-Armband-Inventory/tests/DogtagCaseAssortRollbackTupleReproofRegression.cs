using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class DogtagCaseAssortRollbackTupleReproofRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Dogtag assort rollback tuple-reproof regression failed: module root missing.");

        string source = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseAssort.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
        int catchBlock = source.IndexOf("catch\n        {", StringComparison.Ordinal);
        int successLog = source.IndexOf("logger.Success($\"B&A&HB Dogtag Case added", catchBlock, StringComparison.Ordinal);
        if (catchBlock < 0 || successLog <= catchBlock)
            throw new InvalidOperationException("Dogtag assort rollback tuple-reproof regression failed: rollback block missing.");
        string rollback = source.Substring(catchBlock, successLog - catchBlock);

        RequireBefore(rollback,
            "if (!loyalLevelItems.TryGetValue(id, out var liveOwnedLoyalty) || liveOwnedLoyalty != LoyaltyLevel) throw;",
            "loyalLevelItems.Remove(id);",
            "loyalty value must be re-proved immediately before removal");
        RequireBefore(rollback,
            "if (!barterScheme.TryGetValue(id, out var liveOwnedBarter) || !ReferenceEquals(liveOwnedBarter, barter)) throw;",
            "barterScheme.Remove(id);",
            "exact barter reference must be re-proved immediately before removal");
        RequireBefore(rollback,
            "if (ownedItemIndex < 0 || ownedItemIndex >= items.Count || !ReferenceEquals(items[ownedItemIndex], offer)) throw;",
            "items.RemoveAt(ownedItemIndex);",
            "exact item reference at the captured index must be re-proved immediately before removal");

        if (Count(rollback, "if (!IsAssortWrapperIdentityCurrent()) throw;") < 3)
            throw new InvalidOperationException("Dogtag assort rollback tuple-reproof regression failed: each owned mutation must retain wrapper-chain reproof.");
    }

    private static void RequireBefore(string source, string proof, string mutation, string message)
    {
        int mutationIndex = source.IndexOf(mutation, StringComparison.Ordinal);
        int proofIndex = mutationIndex < 0 ? -1 : source.LastIndexOf(proof, mutationIndex, StringComparison.Ordinal);
        if (proofIndex < 0 || mutationIndex <= proofIndex)
            throw new InvalidOperationException("Dogtag assort rollback tuple-reproof regression failed: " + message + ".");
    }

    private static int Count(string source, string token)
    {
        int count = 0, offset = 0;
        while (true)
        {
            int index = source.IndexOf(token, offset, StringComparison.Ordinal);
            if (index < 0) return count;
            count++;
            offset = index + token.Length;
        }
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "server", "DogtagCaseAssort.cs"))) return current.FullName;
            current = current.Parent;
        }
        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "server", "DogtagCaseAssort.cs"))) return nested;
            if (File.Exists(Path.Combine(current.FullName, "server", "DogtagCaseAssort.cs"))) return current.FullName;
            current = current.Parent;
        }
        return null;
    }
}
