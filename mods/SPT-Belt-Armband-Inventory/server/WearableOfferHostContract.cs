using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTBeltArmbandInventory.Server;

/// <summary>
/// Trader offers are published only for wearable products that have a proven
/// live equipment host in the canonical default inventory. This keeps fail-safe
/// slot/host registration from degrading into purchasable but unequipable items.
/// </summary>
internal static class WearableOfferHostContract
{
    private static readonly MongoId DefaultInventoryTpl = new("55d7217a4bdc2d86028b456d");
    private static readonly MongoId BroadBeltParentTpl = new(RuntimeIdentity.BeltItemParentId);
    private static readonly MongoId DedicatedMagazineBeltTpl = new(RuntimeIdentity.DedicatedMagazineBeltItemId);
    private static readonly MongoId UtilityHeadBandTpl = new(RuntimeIdentity.EmergencyHeadBandItemId);
    private static readonly MongoId BeltSlotMongoId = new(RuntimeIdentity.DedicatedBeltSlotMongoId);
    private static readonly MongoId HeadBandSlotMongoId = new(RuntimeIdentity.DedicatedHeadBandSlotMongoId);

    internal static void RequireArmBandProduct(TemplateTable templateTable, MongoId productTemplate)
    {
        Slot armBand = RequireSingleSlot(templateTable, "ArmBand");
        HashSet<MongoId> filter = RequireSingleFilter(armBand, "ArmBand");

        if (filter.Contains(BroadBeltParentTpl))
            throw new InvalidOperationException("B&A&HB offer host contract refused broad Belt parent exposure through ArmBand.");
        if (filter.Contains(DedicatedMagazineBeltTpl) || filter.Contains(UtilityHeadBandTpl))
            throw new InvalidOperationException("B&A&HB offer host contract refused exact dedicated Belt/HeadBand cross-host exposure through ArmBand.");
        if (!filter.Contains(productTemplate))
            throw new InvalidOperationException($"B&A&HB offer host contract: ArmBand does not expose exact product template {productTemplate}.");
    }

    internal static void RequireDedicatedProducts(TemplateTable templateTable)
    {
        ValidateDedicatedSlot(
            RequireSingleSlot(templateTable, RuntimeIdentity.DedicatedBeltWireSlotId),
            RuntimeIdentity.DedicatedBeltWireSlotId,
            BeltSlotMongoId,
            DedicatedMagazineBeltTpl);
        ValidateDedicatedSlot(
            RequireSingleSlot(templateTable, RuntimeIdentity.DedicatedHeadBandWireSlotId),
            RuntimeIdentity.DedicatedHeadBandWireSlotId,
            HeadBandSlotMongoId,
            UtilityHeadBandTpl);
    }

    private static Slot RequireSingleSlot(TemplateTable templateTable, string wireName)
    {
        if (!templateTable.Items.TryGetValue(DefaultInventoryTpl, out var inventory))
            throw new InvalidOperationException("B&A&HB offer host contract: default inventory template missing.");

        var matches = inventory.Properties?.Slots?
            .Where(slot => string.Equals(slot.Name, wireName, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (matches == null || matches.Length != 1)
            throw new InvalidOperationException($"B&A&HB offer host contract: equipment slot {wireName} is missing or ambiguous.");
        return matches[0];
    }

    private static HashSet<MongoId> RequireSingleFilter(Slot slot, string wireName)
    {
        var filters = slot.Properties?.Filters?.ToArray();
        HashSet<MongoId>? accepted = filters?.Length == 1 ? filters[0].Filter : null;
        if (accepted == null)
            throw new InvalidOperationException($"B&A&HB offer host contract: equipment slot {wireName} does not have exactly one usable filter group.");
        return accepted;
    }

    private static void ValidateDedicatedSlot(Slot slot, string wireName, MongoId id, MongoId allowedTemplate)
    {
        if (!Equals(slot.Id, id)
            || !Equals(slot.Parent, DefaultInventoryTpl)
            || slot.MaxCount != 1
            || slot.Required == true)
            throw new InvalidOperationException($"B&A&HB offer host contract: dedicated slot {wireName} identity differs from the product contract.");

        HashSet<MongoId> accepted = RequireSingleFilter(slot, wireName);
        if (accepted.Count != 1 || !accepted.Contains(allowedTemplate))
            throw new InvalidOperationException($"B&A&HB offer host contract: dedicated slot {wireName} does not expose only its exact product template.");
    }
}
