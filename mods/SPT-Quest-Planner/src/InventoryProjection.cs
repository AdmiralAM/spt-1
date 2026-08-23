using System.Text.Json;

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
        JsonElement root = JsonSerializer.SerializeToElement(profile);
        List<string> warnings = new();
        Dictionary<string, MutableOwned> owned = new(StringComparer.Ordinal);

        if (!TryGetPropertyInsensitive(root, "Inventory", out JsonElement inventory) ||
            !TryGetPropertyInsensitive(inventory, "items", out JsonElement items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            warnings.Add("PMC profile has no Inventory.items array");
            return new InventoryProjection(new Dictionary<string, OwnedItemCount>(StringComparer.Ordinal), warnings);
        }

        foreach (JsonElement item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;

            string? templateId = GetString(item, "_tpl") ?? GetString(item, "tpl");
            if (string.IsNullOrWhiteSpace(templateId))
            {
                warnings.Add("Inventory item without _tpl/tpl skipped");
                continue;
            }

            double count = 1d;
            bool foundInRaid = false;
            if (TryGetPropertyInsensitive(item, "upd", out JsonElement upd) && upd.ValueKind == JsonValueKind.Object)
            {
                count = GetNumber(upd, "StackObjectsCount") ?? 1d;
                foundInRaid = GetBool(upd, "SpawnedInSession") ?? false;
            }

            if (count <= 0d) continue;
            if (!owned.TryGetValue(templateId, out MutableOwned? aggregate))
            {
                aggregate = new MutableOwned();
                owned[templateId] = aggregate;
            }

            aggregate.Total += count;
            if (foundInRaid) aggregate.FoundInRaid += count;
        }

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

            // Current requirements have first claim. FIR stock is reserved for FIR-only conditions first;
            // any FIR surplus may then satisfy non-FIR conditions. This prevents double counting.
            double currentFirAllocated = Math.Min(requirement.CurrentFirRequired, availableFir);
            availableFir -= currentFirAllocated;
            double currentFirOutstanding = Math.Max(0d, requirement.CurrentFirRequired - currentFirAllocated);

            double currentNonFirPool = availableNonFirOnly + availableFir;
            double currentNonFirAllocated = Math.Min(requirement.CurrentNonFirRequired, currentNonFirPool);
            double currentNonFirOutstanding = Math.Max(0d, requirement.CurrentNonFirRequired - currentNonFirAllocated);

            // Consume non-FIR-only stock before remaining FIR stock for generic requirements.
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

    private static string? GetString(JsonElement element, string name)
    {
        if (!TryGetPropertyInsensitive(element, name, out JsonElement value)) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static double? GetNumber(JsonElement element, string name)
    {
        if (!TryGetPropertyInsensitive(element, name, out JsonElement value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number)) return number;
        if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out number)) return number;
        return null;
    }

    private static bool? GetBool(JsonElement element, string name)
    {
        if (!TryGetPropertyInsensitive(element, name, out JsonElement value)) return null;
        if (value.ValueKind == JsonValueKind.True) return true;
        if (value.ValueKind == JsonValueKind.False) return false;
        return null;
    }

    private static bool TryGetPropertyInsensitive(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private sealed class MutableOwned
    {
        public double Total;
        public double FoundInRaid;
    }
}
