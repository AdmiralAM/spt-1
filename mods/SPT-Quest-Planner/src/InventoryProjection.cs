namespace SPTQuestPlanner;

public sealed record OwnedItemCount(
    string TemplateId,
    double Total,
    double FoundInRaid);

public sealed record InventoryProjection(
    IReadOnlyDictionary<string, OwnedItemCount> ByTemplate,
    IReadOnlyList<string> Warnings)
{
    public OwnedItemCount Get(string templateId) =>
        ByTemplate.TryGetValue(templateId, out var value)
            ? value
            : new OwnedItemCount(templateId, 0d, 0d);
}

public sealed record OutstandingItemRequirement(
    string TemplateId,
    double CurrentFirRequired,
    double CurrentNonFirRequired,
    double FutureFirRequired,
    double FutureNonFirRequired,
    double OwnedTotal,
    double OwnedFoundInRaid,
    double CurrentFirOutstanding,
    double CurrentNonFirOutstanding,
    double FutureFirOutstandingAfterCurrent,
    double FutureNonFirOutstandingAfterCurrent,
    IReadOnlySet<string> CurrentQuestIds,
    IReadOnlySet<string> FutureQuestIds)
{
    public double CurrentRequired => CurrentFirRequired + CurrentNonFirRequired;
    public double FutureRequired => FutureFirRequired + FutureNonFirRequired;
    public double CurrentOutstanding => CurrentFirOutstanding + CurrentNonFirOutstanding;
    public double FutureOutstandingAfterCurrent => FutureFirOutstandingAfterCurrent + FutureNonFirOutstandingAfterCurrent;
}

public static class InventoryProjectionExtractor
{
    public static InventoryProjection Extract(object profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        List<string> warnings = new();
        Dictionary<string, MutableOwned> owned = new(StringComparer.Ordinal);

        object? inventory = SptObjectReader.Get(profile, "Inventory", "inventory");
        object? items = SptObjectReader.Get(inventory, "items", "Items");
        bool sawItem = false;
        foreach (object item in SptObjectReader.Values(items))
        {
            sawItem = true;
            string? templateId = SptObjectReader.String(SptObjectReader.Get(item, "_tpl", "tpl", "TemplateId"));
            if (string.IsNullOrWhiteSpace(templateId))
            {
                warnings.Add("Inventory item without _tpl/tpl skipped");
                continue;
            }

            object? upd = SptObjectReader.Get(item, "upd", "Upd");
            double count = SptObjectReader.Double(SptObjectReader.Get(upd, "StackObjectsCount", "stackObjectsCount")) ?? 1d;
            bool foundInRaid = SptObjectReader.Bool(SptObjectReader.Get(upd, "SpawnedInSession", "spawnedInSession")) ?? false;
            if (count <= 0d) continue;

            if (!owned.TryGetValue(templateId, out MutableOwned? aggregate))
            {
                aggregate = new MutableOwned();
                owned[templateId] = aggregate;
            }
            aggregate.Total += count;
            if (foundInRaid) aggregate.FoundInRaid += count;
        }

        if (!sawItem && items is null) warnings.Add("PMC profile has no Inventory.items array");
        IReadOnlyDictionary<string, OwnedItemCount> result = owned.ToDictionary(
            pair => pair.Key,
            pair => new OwnedItemCount(pair.Key, pair.Value.Total, pair.Value.FoundInRaid),
            StringComparer.Ordinal);
        return new InventoryProjection(result, warnings.Distinct(StringComparer.Ordinal).ToArray());
    }

    public static IReadOnlyList<OutstandingItemRequirement> CalculateOutstanding(
        IEnumerable<AggregatedItemRequirement> requirements,
        InventoryProjection inventory)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(inventory);

        List<OutstandingItemRequirement> result = new();
        foreach (AggregatedItemRequirement requirement in requirements)
        {
            OwnedItemCount owned = inventory.Get(requirement.TemplateId);
            double availableFir = Math.Max(0d, owned.FoundInRaid);
            double availableNonFirOnly = Math.Max(0d, owned.Total - owned.FoundInRaid);

            double currentFirAllocated = Math.Min(requirement.CurrentFirRequired, availableFir);
            availableFir -= currentFirAllocated;
            double currentFirOutstanding = Math.Max(0d, requirement.CurrentFirRequired - currentFirAllocated);

            double currentNonFirPool = availableNonFirOnly + availableFir;
            double currentNonFirAllocated = Math.Min(requirement.CurrentNonFirRequired, currentNonFirPool);
            double currentNonFirOutstanding = Math.Max(0d, requirement.CurrentNonFirRequired - currentNonFirAllocated);

            double consumeNonFirOnly = Math.Min(currentNonFirAllocated, availableNonFirOnly);
            availableNonFirOnly -= consumeNonFirOnly;
            double consumeFirForGeneric = currentNonFirAllocated - consumeNonFirOnly;
            availableFir = Math.Max(0d, availableFir - consumeFirForGeneric);

            double futureFirAllocated = Math.Min(requirement.FutureFirRequired, availableFir);
            availableFir -= futureFirAllocated;
            double futureFirOutstanding = Math.Max(0d, requirement.FutureFirRequired - futureFirAllocated);

            double futureNonFirPool = availableNonFirOnly + availableFir;
            double futureNonFirAllocated = Math.Min(requirement.FutureNonFirRequired, futureNonFirPool);
            double futureNonFirOutstanding = Math.Max(0d, requirement.FutureNonFirRequired - futureNonFirAllocated);

            result.Add(new OutstandingItemRequirement(
                requirement.TemplateId,
                requirement.CurrentFirRequired,
                requirement.CurrentNonFirRequired,
                requirement.FutureFirRequired,
                requirement.FutureNonFirRequired,
                owned.Total,
                owned.FoundInRaid,
                currentFirOutstanding,
                currentNonFirOutstanding,
                futureFirOutstanding,
                futureNonFirOutstanding,
                requirement.CurrentQuestIds,
                requirement.FutureQuestIds));
        }
        return result;
    }

    private sealed class MutableOwned
    {
        public double Total;
        public double FoundInRaid;
    }
}
