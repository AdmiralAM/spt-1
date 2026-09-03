using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class DogtagCaseAssortRollbackAtomicityRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Dogtag assort rollback atomicity regression failed: module root could not be resolved.");

        string source = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseAssort.cs"));
        int catchStart = source.IndexOf("catch\n        {", StringComparison.Ordinal);
        int logger = catchStart < 0 ? -1 : source.IndexOf("logger.Success", catchStart, StringComparison.Ordinal);
        if (catchStart < 0 || logger <= catchStart)
            throw new InvalidOperationException("Dogtag assort rollback atomicity regression failed: bounded rollback region was not found.");

        string rollback = source.Substring(catchStart, logger - catchStart);
        int wrapperProof = rollback.IndexOf("if (!IsAssortWrapperIdentityCurrent())", StringComparison.Ordinal);
        int ownsItem = rollback.IndexOf("bool ownsItem = !itemAdded || ownedItemIndex >= 0;", StringComparison.Ordinal);
        int ownsBarter = rollback.IndexOf("bool ownsBarter = barterAdded", StringComparison.Ordinal);
        int ownsLoyalty = rollback.IndexOf("bool ownsLoyalty = loyaltyAdded", StringComparison.Ordinal);
        int tupleGate = rollback.IndexOf("if (!ownsItem || !ownsBarter || !ownsLoyalty)", StringComparison.Ordinal);
        int firstRemove = rollback.IndexOf(".Remove", StringComparison.Ordinal);

        if (wrapperProof < 0 || ownsItem < 0 || ownsBarter < 0 || ownsLoyalty < 0 || tupleGate < 0 || firstRemove < 0
            || !(wrapperProof < ownsItem && ownsItem < ownsBarter && ownsBarter < ownsLoyalty && ownsLoyalty < tupleGate && tupleGate < firstRemove))
            throw new InvalidOperationException("Dogtag assort rollback atomicity regression failed: complete prefix ownership must be proven before the first rollback mutation.");

        foreach (string token in new[]
        {
            "? barterScheme.TryGetValue(id, out var currentBarter) && ReferenceEquals(currentBarter, barter)",
            ": !barterScheme.ContainsKey(id);",
            "? loyalLevelItems.TryGetValue(id, out var currentLoyalty) && currentLoyalty == LoyaltyLevel",
            ": !loyalLevelItems.ContainsKey(id);"
        })
            Require(rollback, token, "partial publication rollback must reject foreign metadata that this transaction did not add");

        int loyaltyBlock = rollback.IndexOf("if (loyaltyAdded)", tupleGate, StringComparison.Ordinal);
        int loyaltyRemove = loyaltyBlock < 0 ? -1 : rollback.IndexOf("loyalLevelItems.Remove(id);", loyaltyBlock, StringComparison.Ordinal);
        int barterBlock = loyaltyRemove < 0 ? -1 : rollback.IndexOf("if (barterAdded)", loyaltyRemove, StringComparison.Ordinal);
        int loyaltyAbsentBeforeBarter = barterBlock < 0 ? -1 : rollback.IndexOf("if (loyalLevelItems.ContainsKey(id)) throw;", barterBlock, StringComparison.Ordinal);
        int barterRemove = loyaltyAbsentBeforeBarter < 0 ? -1 : rollback.IndexOf("barterScheme.Remove(id);", loyaltyAbsentBeforeBarter, StringComparison.Ordinal);
        int itemBlock = barterRemove < 0 ? -1 : rollback.IndexOf("if (itemAdded)", barterRemove, StringComparison.Ordinal);
        int metadataAbsentBeforeItem = itemBlock < 0 ? -1 : rollback.IndexOf("if (barterScheme.ContainsKey(id) || loyalLevelItems.ContainsKey(id)) throw;", itemBlock, StringComparison.Ordinal);
        int itemRemove = metadataAbsentBeforeItem < 0 ? -1 : rollback.IndexOf("items.RemoveAt(ownedItemIndex);", metadataAbsentBeforeItem, StringComparison.Ordinal);

        if (loyaltyBlock < 0 || loyaltyRemove < 0 || barterBlock < 0 || loyaltyAbsentBeforeBarter < 0 || barterRemove < 0
            || itemBlock < 0 || metadataAbsentBeforeItem < 0 || itemRemove < 0
            || !(tupleGate < loyaltyBlock && loyaltyBlock < loyaltyRemove && loyaltyRemove < barterBlock
                && barterBlock < loyaltyAbsentBeforeBarter && loyaltyAbsentBeforeBarter < barterRemove
                && barterRemove < itemBlock && itemBlock < metadataAbsentBeforeItem && metadataAbsentBeforeItem < itemRemove))
            throw new InvalidOperationException("Dogtag assort rollback atomicity regression failed: rollback must peel loyalty -> barter -> item only after mutation-adjacent downstream-absence proofs.");

        if (rollback.Contains("if (loyaltyAdded && ownsItem && ownsBarter", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag assort rollback atomicity regression failed: legacy partial rollback gate can leave dangling Ragman metadata after tuple drift.");
    }

    private static void Require(string source, string token, string message)
    {
        if (!source.Contains(token, StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag assort rollback atomicity regression failed: " + message + ": " + token);
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "server", "DogtagCaseAssort.cs"))) return current.FullName;
            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "server", "DogtagCaseAssort.cs"))) return nested;
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "server", "DogtagCaseAssort.cs"))) return current.FullName;
            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "server", "DogtagCaseAssort.cs"))) return nested;
            current = current.Parent;
        }
        return null;
    }
}
