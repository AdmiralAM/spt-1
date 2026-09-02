using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class DogtagCaseHostCollectionIdentityRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Dogtag host collection identity regression failed: module root could not be resolved.");

        string source = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseItem.cs"));
        Require(source, "InventoryProperties = inventoryProperties",
            "capture must retain the exact DefaultInventory properties object");
        Require(source, "SlotsCollection = slotsCollection",
            "capture must retain the exact DefaultInventory slots collection");
        Require(source, "SlotProperties = slotProperties",
            "capture must retain the exact Dogtag slot properties object");
        Require(source, "FiltersCollection = filtersCollection",
            "capture must retain the exact Dogtag filters collection");
        Require(source, "!ReferenceEquals(liveInventory.Properties, boundary.InventoryProperties)",
            "live publication must reject replaced DefaultInventory properties");
        Require(source, "!ReferenceEquals(liveInventory.Properties?.Slots, boundary.SlotsCollection)",
            "live publication must reject a value-identical replacement slots collection");
        Require(source, "!ReferenceEquals(liveSlots[0].Properties, boundary.SlotProperties)",
            "live publication must reject replaced Dogtag slot properties");
        Require(source, "!ReferenceEquals(liveSlots[0].Properties?.Filters, boundary.FiltersCollection)",
            "live publication must reject a value-identical replacement filter collection");
        Require(source, "!ReferenceEquals(liveGroups[0], boundary.FilterGroup)",
            "live publication must retain the exact filter-group object proof");
        Require(source, "!ReferenceEquals(liveGroups[0].Filter, boundary.Filter)",
            "live publication must retain the exact HashSet proof");

        int captureInventory = source.IndexOf("InventoryProperties = inventoryProperties", StringComparison.Ordinal);
        int captureSlots = source.IndexOf("SlotsCollection = slotsCollection", captureInventory, StringComparison.Ordinal);
        int captureSlotProperties = source.IndexOf("SlotProperties = slotProperties", captureSlots, StringComparison.Ordinal);
        int captureFilters = source.IndexOf("FiltersCollection = filtersCollection", captureSlotProperties, StringComparison.Ordinal);
        int liveInventory = source.IndexOf("!ReferenceEquals(liveInventory.Properties, boundary.InventoryProperties)", captureFilters, StringComparison.Ordinal);
        int liveSlotsCollection = source.IndexOf("!ReferenceEquals(liveInventory.Properties?.Slots, boundary.SlotsCollection)", liveInventory, StringComparison.Ordinal);
        int liveSlotProperties = source.IndexOf("!ReferenceEquals(liveSlots[0].Properties, boundary.SlotProperties)", liveSlotsCollection, StringComparison.Ordinal);
        int liveFiltersCollection = source.IndexOf("!ReferenceEquals(liveSlots[0].Properties?.Filters, boundary.FiltersCollection)", liveSlotProperties, StringComparison.Ordinal);
        int liveGroup = source.IndexOf("!ReferenceEquals(liveGroups[0], boundary.FilterGroup)", liveFiltersCollection, StringComparison.Ordinal);
        int liveFilter = source.IndexOf("!ReferenceEquals(liveGroups[0].Filter, boundary.Filter)", liveGroup, StringComparison.Ordinal);
        if (captureInventory < 0 || captureSlots <= captureInventory || captureSlotProperties <= captureSlots
            || captureFilters <= captureSlotProperties || liveInventory <= captureFilters
            || liveSlotsCollection <= liveInventory || liveSlotProperties <= liveSlotsCollection
            || liveFiltersCollection <= liveSlotProperties || liveGroup <= liveFiltersCollection || liveFilter <= liveGroup)
            throw new InvalidOperationException("Dogtag host collection identity regression failed: capture/reproof ordering drifted from inventory properties -> slots collection -> slot properties -> filters collection -> group -> HashSet.");

        int commit = source.IndexOf("private void CommitDogtagSlotExposure", StringComparison.Ordinal);
        int firstLive = source.IndexOf("RequireLiveDogtagHostIdentity(boundary);", commit, StringComparison.Ordinal);
        int preserved = source.IndexOf("DogtagCaseHostContract.RequirePreserved(filter);", firstLive, StringComparison.Ordinal);
        int secondLive = source.IndexOf("RequireLiveDogtagHostIdentity(boundary);", firstLive + 1, StringComparison.Ordinal);
        int add = source.IndexOf("bool addedHere = filter.Add(DogtagCaseTpl);", secondLive, StringComparison.Ordinal);
        int committed = source.IndexOf("DogtagCaseHostContract.RequireCommitted(filter);", add, StringComparison.Ordinal);
        int finalLive = source.IndexOf("RequireLiveDogtagHostIdentity(boundary);", committed, StringComparison.Ordinal);
        if (commit < 0 || firstLive <= commit || preserved <= firstLive || secondLive <= preserved
            || add <= secondLive || committed <= add || finalLive <= committed)
            throw new InvalidOperationException("Dogtag host collection identity regression failed: collection-chain proof must remain inside the owned pre/post mutation boundary.");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string candidate = Path.Combine(current.FullName, "server", "DogtagCaseItem.cs");
            if (File.Exists(candidate)) return current.FullName;
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

    private static void Require(string source, string token, string message)
    {
        if (!source.Contains(token, StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag host collection identity regression failed: " + message + ".");
    }
}
