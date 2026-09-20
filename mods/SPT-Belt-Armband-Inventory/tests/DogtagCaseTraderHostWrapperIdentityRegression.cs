using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class DogtagCaseTraderHostWrapperIdentityRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Dogtag trader host wrapper-identity regression failed: module root could not be resolved.");

        string path = Path.Combine(root, "server", "DogtagCaseAssort.cs");
        string source = File.ReadAllText(path);
        int method = source.IndexOf("internal static void RequireExactDogtagHost", StringComparison.Ordinal);
        int validation = source.IndexOf("private static void ValidateExisting", method, StringComparison.Ordinal);
        if (method < 0 || validation <= method)
            throw new InvalidOperationException("Dogtag trader host wrapper-identity regression failed: exact host method is missing.");

        string body = source.Substring(method, validation - method);
        Require(body, "var inventoryProperties = inventory.Properties", "inventory Properties wrapper must be captured");
        Require(body, "var slotsCollection = inventoryProperties.Slots", "Slots collection wrapper must be captured");
        Require(body, "var slotProperties = slot.Properties", "Dogtag slot Properties wrapper must be captured");
        Require(body, "var filtersCollection = slotProperties.Filters", "Filters collection wrapper must be captured");
        Require(body, "ReferenceEquals(liveInventory.Properties, inventoryProperties)", "inventory Properties wrapper must be re-proven");
        Require(body, "ReferenceEquals(liveInventory.Properties?.Slots, slotsCollection)", "Slots collection wrapper must be re-proven");
        Require(body, "ReferenceEquals(liveSlots[0].Properties, slotProperties)", "slot Properties wrapper must be re-proven");
        Require(body, "ReferenceEquals(liveSlots[0].Properties?.Filters, filtersCollection)", "Filters collection wrapper must be re-proven");
        Require(body, "ReferenceEquals(liveGroups[0], groups[0])", "sole filter-group identity must remain pinned");
        Require(body, "ReferenceEquals(liveGroups[0].Filter, hostFilter)", "host HashSet identity must remain pinned");

        int firstCommitted = body.IndexOf("DogtagCaseHostContract.RequireCommitted(hostFilter);", StringComparison.Ordinal);
        int inventory = body.IndexOf("ReferenceEquals(liveInventory, inventory)", firstCommitted, StringComparison.Ordinal);
        int inventoryProps = body.IndexOf("ReferenceEquals(liveInventory.Properties, inventoryProperties)", inventory, StringComparison.Ordinal);
        int slots = body.IndexOf("ReferenceEquals(liveInventory.Properties?.Slots, slotsCollection)", inventoryProps, StringComparison.Ordinal);
        int slot = body.IndexOf("ReferenceEquals(liveSlots[0], slot)", slots, StringComparison.Ordinal);
        int slotProps = body.IndexOf("ReferenceEquals(liveSlots[0].Properties, slotProperties)", slot, StringComparison.Ordinal);
        int filters = body.IndexOf("ReferenceEquals(liveSlots[0].Properties?.Filters, filtersCollection)", slotProps, StringComparison.Ordinal);
        int group = body.IndexOf("ReferenceEquals(liveGroups[0], groups[0])", filters, StringComparison.Ordinal);
        int filter = body.IndexOf("ReferenceEquals(liveGroups[0].Filter, hostFilter)", group, StringComparison.Ordinal);
        int secondCommitted = body.IndexOf("DogtagCaseHostContract.RequireCommitted(hostFilter);", firstCommitted + 1, StringComparison.Ordinal);

        if (firstCommitted < 0 || inventory <= firstCommitted || inventoryProps <= inventory || slots <= inventoryProps
            || slot <= slots || slotProps <= slot || filters <= slotProps || group <= filters || filter <= group
            || secondCommitted <= filter)
            throw new InvalidOperationException("Dogtag trader host wrapper-identity regression failed: committed proof -> full wrapper chain -> committed content reproof ordering drifted.");

        if (body.Contains("inventory.Properties?.Slots?", StringComparison.Ordinal)
            || body.Contains("slot.Properties?.Filters?.ToArray()", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag trader host wrapper-identity regression failed: detached traversal that bypasses captured mutable wrappers was restored.");
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
            string direct = Path.Combine(current.FullName, "server", "DogtagCaseAssort.cs");
            if (File.Exists(direct)) return current.FullName;
            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "server", "DogtagCaseAssort.cs"))) return nested;
            current = current.Parent;
        }
        return null;
    }

    private static void Require(string source, string token, string message)
    {
        if (!source.Contains(token, StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag trader host wrapper-identity regression failed: " + message + ".");
    }
}
