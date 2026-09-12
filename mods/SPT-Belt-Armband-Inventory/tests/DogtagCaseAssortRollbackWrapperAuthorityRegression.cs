using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class DogtagCaseAssortRollbackWrapperAuthorityRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Dogtag assort rollback wrapper-authority regression failed: module root could not be resolved.");

        string source = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseAssort.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
        Require(source, "bool IsAssortWrapperIdentityCurrent()", "rollback must have a non-mutating exact-wrapper authority predicate");
        Require(source, "ReferenceEquals(trader.Assort, assort)", "rollback authority must pin the captured Ragman Assort reference");
        Require(source, "ReferenceEquals(trader.Assort?.Items, items)", "rollback authority must pin the captured Items wrapper");
        Require(source, "ReferenceEquals(trader.Assort?.BarterScheme, barterScheme)", "rollback authority must pin the captured BarterScheme wrapper");
        Require(source, "ReferenceEquals(trader.Assort?.LoyalLevelItems, loyalLevelItems)", "rollback authority must pin the captured LoyalLevelItems wrapper");

        int catchStart = source.IndexOf("catch\n        {", StringComparison.Ordinal);
        int success = source.IndexOf("logger.Success", catchStart, StringComparison.Ordinal);
        if (catchStart < 0 || success <= catchStart)
            throw new InvalidOperationException("Dogtag assort rollback wrapper-authority regression failed: bounded rollback region is missing.");
        string rollback = source.Substring(catchStart, success - catchStart);

        int firstFence = rollback.IndexOf("if (!IsAssortWrapperIdentityCurrent())", StringComparison.Ordinal);
        int firstMutation = FirstMutationIndex(rollback);
        if (firstFence < 0 || firstMutation < 0 || firstFence > firstMutation)
            throw new InvalidOperationException("Dogtag assort rollback wrapper-authority regression failed: stale wrapper chain can be mutated before the first authority fence.");

        RequireMutationFence(
            rollback,
            "loyalLevelItems.Remove(id);",
            new[]
            {
                "if (!IsAssortWrapperIdentityCurrent()) throw;",
                "if (ownedItemIndex < 0 || ownedItemIndex >= items.Count || !ReferenceEquals(items[ownedItemIndex], offer)) throw;",
                "if (!barterScheme.TryGetValue(id, out var liveOwnedBarter) || !ReferenceEquals(liveOwnedBarter, barter)) throw;",
                "if (!loyalLevelItems.TryGetValue(id, out var liveOwnedLoyalty) || liveOwnedLoyalty != LoyaltyLevel) throw;"
            },
            "loyalty rollback must re-prove captured wrapper plus the complete owned tuple immediately before mutation");
        RequireMutationFence(
            rollback,
            "barterScheme.Remove(id);",
            new[]
            {
                "if (!IsAssortWrapperIdentityCurrent()) throw;",
                "if (!barterScheme.TryGetValue(id, out var liveOwnedBarter) || !ReferenceEquals(liveOwnedBarter, barter)) throw;"
            },
            "barter rollback must re-prove captured wrapper plus exact barter reference immediately before mutation");
        RequireMutationFence(
            rollback,
            "items.RemoveAt(ownedItemIndex);",
            new[]
            {
                "if (!IsAssortWrapperIdentityCurrent()) throw;",
                "if (ownedItemIndex < 0 || ownedItemIndex >= items.Count || !ReferenceEquals(items[ownedItemIndex], offer)) throw;"
            },
            "item rollback must re-prove captured wrapper plus exact item reference/index immediately before mutation");
    }

    private static void RequireMutationFence(string source, string mutation, string[] orderedProofs, string message)
    {
        int mutationIndex = source.IndexOf(mutation, StringComparison.Ordinal);
        if (mutationIndex < 0)
            throw new InvalidOperationException("Dogtag assort rollback wrapper-authority regression failed: " + message + " (mutation missing).");

        int cursor = source.LastIndexOf(orderedProofs[0], mutationIndex, StringComparison.Ordinal);
        if (cursor < 0)
            throw new InvalidOperationException("Dogtag assort rollback wrapper-authority regression failed: " + message + " (wrapper proof missing).");

        for (int i = 1; i < orderedProofs.Length; i++)
        {
            int next = source.IndexOf(orderedProofs[i], cursor + orderedProofs[i - 1].Length, StringComparison.Ordinal);
            if (next < 0 || next >= mutationIndex)
                throw new InvalidOperationException("Dogtag assort rollback wrapper-authority regression failed: " + message + " (tuple proof ordering changed).");
            cursor = next;
        }
    }

    private static int FirstMutationIndex(string source)
    {
        int loyalty = source.IndexOf("loyalLevelItems.Remove", StringComparison.Ordinal);
        int barter = source.IndexOf("barterScheme.Remove", StringComparison.Ordinal);
        int item = source.IndexOf("items.RemoveAt", StringComparison.Ordinal);
        int result = int.MaxValue;
        if (loyalty >= 0) result = Math.Min(result, loyalty);
        if (barter >= 0) result = Math.Min(result, barter);
        if (item >= 0) result = Math.Min(result, item);
        return result == int.MaxValue ? -1 : result;
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
            current = current.Parent;
        }
        return null;
    }

    private static void Require(string source, string token, string message)
    {
        if (!source.Contains(token, StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag assort rollback wrapper-authority regression failed: " + message + ".");
    }
}
