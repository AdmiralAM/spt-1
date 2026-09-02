using System.Collections;
using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTBeltArmbandInventory.Server;

/// <summary>
/// Final preload semantic guard for the shared vanilla Dogtag host. SPT 4.1.3
/// SlotFilter has no ExcludedFilter member; included acceptance is therefore the
/// complete current equipment-slot contract. If a compatible future model adds an
/// ExcludedFilter member, inspect it fail-closed rather than silently ignoring it.
/// Foreign exclusions remain untouched and authoritative.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Preload + 4)]
public sealed class DogtagCaseHostExclusionGuard(TemplateTable templateTable) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DogtagCaseHostExclusionPolicy.RequireCurrentHost(templateTable);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Re-proves effective Dogtag-slot acceptance immediately before the Dogtag Case
/// trader offer is published. This closes the startup window between preload host
/// commit and TraderRegistration without mutating either included or any optional
/// future excluded set.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.TraderRegistration + 1)]
public sealed class DogtagCaseTraderHostExclusionGuard(TemplateTable templateTable) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DogtagCaseHostExclusionPolicy.RequireCurrentHost(templateTable);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

public static class DogtagCaseHostExclusionPolicy
{
    private static readonly MongoId DefaultInventoryTpl = RuntimeCandidateBeltItem.DefaultInventoryTpl;
    private const string DogtagSlotName = "Dogtag";
    private const string ExcludedFilterMemberName = "ExcludedFilter";

    public static void RequireCurrentHost(TemplateTable templateTable)
    {
        ArgumentNullException.ThrowIfNull(templateTable);
        if (!templateTable.Items.TryGetValue(DefaultInventoryTpl, out var inventory))
            throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: DefaultInventory is missing.");

        var inventoryProperties = inventory.Properties
            ?? throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: DefaultInventory properties are missing.");
        var slotsCollection = inventoryProperties.Slots
            ?? throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: DefaultInventory Slots collection is missing.");
        var slots = slotsCollection
            .Where(x => string.Equals(x.Name, DogtagSlotName, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (slots.Length != 1)
            throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: Dogtag slot is missing or ambiguous.");

        var slot = slots[0];
        var slotProperties = slot.Properties
            ?? throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: Dogtag slot properties are missing.");
        var filtersCollection = slotProperties.Filters
            ?? throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: Dogtag Filters collection is missing.");
        var groups = filtersCollection.Take(2).ToArray();
        if (groups.Length != 1 || groups[0].Filter == null)
            throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: exact single Dogtag filter group is unavailable.");

        var filterGroup = groups[0];
        var hostFilter = filterGroup.Filter;

        // The effective proof is point-in-time and uses the same captured host/filter
        // authority throughout. Included content must already be the committed Dogtag
        // host, while optional future exclusions are detached before evaluation so a
        // lazy/mutable collection cannot change underneath RequireEffectiveAcceptance.
        DogtagCaseHostContract.RequireCommitted(hostFilter);
        var excludedBefore = SnapshotOptionalExcludedFilter(filterGroup);
        RequireEffectiveAcceptance(hostFilter, excludedBefore);
        var excludedAfter = SnapshotOptionalExcludedFilter(filterGroup);
        if (!OptionalFilterSetEquals(excludedBefore, excludedAfter))
            throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: optional ExcludedFilter changed during effective-host verification.");

        // Re-prove the complete mutable host wrapper chain after the effective proof.
        // A value-identical replacement is not allowed to inherit the authority that
        // was captured above; publication must consume the exact verified live chain.
        if (!templateTable.Items.TryGetValue(DefaultInventoryTpl, out var liveInventory)
            || !ReferenceEquals(liveInventory, inventory))
            throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: DefaultInventory changed during effective-host verification.");
        if (!ReferenceEquals(liveInventory.Properties, inventoryProperties))
            throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: DefaultInventory properties changed during effective-host verification.");
        if (!ReferenceEquals(liveInventory.Properties?.Slots, slotsCollection))
            throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: DefaultInventory Slots collection changed during effective-host verification.");

        var liveSlots = slotsCollection
            .Where(x => string.Equals(x.Name, DogtagSlotName, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (liveSlots.Length != 1 || !ReferenceEquals(liveSlots[0], slot))
            throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: Dogtag slot changed during effective-host verification.");
        if (!ReferenceEquals(liveSlots[0].Properties, slotProperties))
            throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: Dogtag slot properties changed during effective-host verification.");
        if (!ReferenceEquals(liveSlots[0].Properties?.Filters, filtersCollection))
            throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: Dogtag Filters collection changed during effective-host verification.");

        var liveGroups = filtersCollection.Take(2).ToArray();
        if (liveGroups.Length != 1
            || !ReferenceEquals(liveGroups[0], filterGroup)
            || !ReferenceEquals(liveGroups[0].Filter, hostFilter))
            throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: Dogtag filter group/filter changed during effective-host verification.");

        DogtagCaseHostContract.RequireCommitted(hostFilter);
    }

    private static IReadOnlyCollection<MongoId>? SnapshotOptionalExcludedFilter(object filterGroup)
    {
        IEnumerable<MongoId>? values = ReadOptionalExcludedFilter(filterGroup);
        return values?.ToArray();
    }

    private static bool OptionalFilterSetEquals(IReadOnlyCollection<MongoId>? first, IReadOnlyCollection<MongoId>? second)
    {
        if (first == null || second == null) return first == null && second == null;
        return first.ToHashSet().SetEquals(second);
    }

    private static IEnumerable<MongoId>? ReadOptionalExcludedFilter(object filterGroup)
    {
        ArgumentNullException.ThrowIfNull(filterGroup);
        MemberInfo? selected = null;

        // A future SPT model may introduce ExcludedFilter on this equipment-slot
        // filter group. Resolve the member across the inheritance chain explicitly:
        // a value-identical property/field hiding collision is semantic ambiguity and
        // must not silently prefer one representation over another.
        for (Type? current = filterGroup.GetType(); current != null; current = current.BaseType)
        {
            foreach (var property in current.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (!string.Equals(property.Name, ExcludedFilterMemberName, StringComparison.Ordinal))
                    continue;
                if (property.GetMethod == null || property.GetMethod.IsStatic || property.GetIndexParameters().Length != 0)
                    throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: optional ExcludedFilter property is not a readable zero-index instance member.");
                if (selected != null)
                    throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: optional ExcludedFilter member is ambiguous across the filter-group hierarchy.");
                selected = property;
            }

            foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (!string.Equals(field.Name, ExcludedFilterMemberName, StringComparison.Ordinal))
                    continue;
                if (field.IsStatic)
                    throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: optional ExcludedFilter field is static.");
                if (selected != null)
                    throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: optional ExcludedFilter member is ambiguous across the filter-group hierarchy.");
                selected = field;
            }
        }

        if (selected == null)
            return null;

        object? raw = selected switch
        {
            PropertyInfo property => property.GetValue(filterGroup),
            FieldInfo field => field.GetValue(filterGroup),
            _ => throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: optional ExcludedFilter member has an unsupported reflection shape.")
        };
        if (raw == null)
            return null;
        if (raw is IEnumerable<MongoId> typed)
            return typed;

        if (raw is not IEnumerable enumerable)
            throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: optional ExcludedFilter member has an unsupported non-enumerable shape.");

        var result = new List<MongoId>();
        foreach (var value in enumerable)
        {
            if (value is not MongoId id)
                throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: optional ExcludedFilter contains an unsupported value type.");
            result.Add(id);
        }
        return result;
    }

    public static void RequireEffectiveAcceptance(IEnumerable<MongoId> included, IEnumerable<MongoId>? excluded)
    {
        ArgumentNullException.ThrowIfNull(included);
        var accepted = included.ToHashSet();
        var bear = new MongoId(DogtagCaseHostContract.BearDogtagTemplateId);
        var usec = new MongoId(DogtagCaseHostContract.UsecDogtagTemplateId);
        var dogtagCase = new MongoId(RuntimeIdentity.DogtagCaseItemId);

        if (!accepted.Contains(bear) || !accepted.Contains(usec) || !accepted.Contains(dogtagCase))
            throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: included filter lost BEAR, USEC, or the exact Dogtag Case acceptance.");

        if (excluded == null) return;
        var denied = excluded.ToHashSet();
        if (denied.Contains(bear) || denied.Contains(usec) || denied.Contains(dogtagCase))
            throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: optional ExcludedFilter negates BEAR, USEC, or exact Dogtag Case effective acceptance.");
    }
}