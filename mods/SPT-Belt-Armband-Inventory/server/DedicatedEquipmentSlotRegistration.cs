using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTBeltArmbandInventory.Server;

/// <summary>
/// Adds the two product-required equipment locations to the canonical default
/// inventory template. EFT 4.1.x parses slot names through EquipmentSlot during
/// InventoryEquipment construction, therefore dedicated locations serialize as
/// collision-checked numeric pseudo-enum IDs 15/16 rather than invented enum names.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Preload + 2)]
public sealed class DedicatedEquipmentSlotRegistration(
    TemplateTable templateTable,
    ISptLogger<DedicatedEquipmentSlotRegistration> logger) : IOnLoad
{
    private static readonly MongoId DefaultInventoryTpl = new("55d7217a4bdc2d86028b456d");
    private static readonly MongoId BeltSlotMongoId = new(RuntimeIdentity.DedicatedBeltSlotMongoId);
    private static readonly MongoId HeadBandSlotMongoId = new(RuntimeIdentity.DedicatedHeadBandSlotMongoId);
    private static readonly MongoId BeltParentTpl = new(RuntimeIdentity.BeltItemParentId);
    private static readonly MongoId HeadBandParentTpl = new(RuntimeIdentity.HeadBandItemParentId);

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        if (!templateTable.Items.TryGetValue(DefaultInventoryTpl, out var inventory))
            throw new InvalidOperationException("B&A&HB dedicated-slot default inventory template missing.");

        var slots = inventory.Properties?.Slots?.ToList()
            ?? throw new InvalidOperationException("B&A&HB dedicated-slot default inventory Slots missing.");

        var armBand = RequireVanillaSlot(slots, "ArmBand");
        RequireVanillaSlot(slots, "Pockets");
        RequireVanillaSlot(slots, "Backpack");
        RequireVanillaSlot(slots, "Headwear");
        RejectNumericCollision(slots, RuntimeIdentity.DedicatedBeltWireSlotId);
        RejectNumericCollision(slots, RuntimeIdentity.DedicatedHeadBandWireSlotId);

        UpsertDedicatedSlot(slots, armBand, RuntimeIdentity.DedicatedBeltWireSlotId, BeltSlotMongoId, BeltParentTpl);
        UpsertDedicatedSlot(slots, armBand, RuntimeIdentity.DedicatedHeadBandWireSlotId, HeadBandSlotMongoId, HeadBandParentTpl);

        MoveAfter(slots, RuntimeIdentity.DedicatedBeltWireSlotId, "Pockets");
        MoveBefore(slots, RuntimeIdentity.DedicatedHeadBandWireSlotId, "Headwear");

        ValidatePlacement(slots);
        inventory.Properties!.Slots = slots;

        logger.Success($"B&A&HB #2 MOD SPT dedicated equipment slots registered: Belt wire={RuntimeIdentity.DedicatedBeltWireSlotId} after Pockets/before Backpack; HeadBand wire={RuntimeIdentity.DedicatedHeadBandWireSlotId} before Headwear.");
        return Task.CompletedTask;
    }

    private static Slot RequireVanillaSlot(List<Slot> slots, string name)
    {
        var matches = slots.Where(x => string.Equals(x.Name, name, StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException($"B&A&HB dedicated-slot expected exactly one vanilla {name} slot, got {matches.Length}.");
        return matches[0];
    }

    private static void RejectNumericCollision(List<Slot> slots, string wireName)
    {
        var matches = slots.Where(x => string.Equals(x.Name, wireName, StringComparison.Ordinal)).ToArray();
        if (matches.Length > 1)
            throw new InvalidOperationException($"B&A&HB dedicated-slot duplicate wire-id collision: {wireName}.");
    }

    private static void UpsertDedicatedSlot(
        List<Slot> slots,
        Slot armBandPrototype,
        string wireName,
        MongoId id,
        MongoId allowedParent)
    {
        var matches = slots.Where(x => string.Equals(x.Name, wireName, StringComparison.Ordinal)).ToArray();
        if (matches.Length == 1)
        {
            ValidateDedicatedSlot(matches[0], wireName, id, allowedParent);
            return;
        }

        slots.Add(new Slot
        {
            Name = wireName,
            Id = id,
            Parent = DefaultInventoryTpl,
            MaxCount = 1,
            Required = false,
            MergeSlotWithChildren = armBandPrototype.MergeSlotWithChildren ?? true,
            Prototype = armBandPrototype.Prototype,
            Properties = new SlotProperties
            {
                MaxStackCount = 1,
                Filters =
                [
                    new SlotFilter
                    {
                        Filter = [allowedParent],
                        Locked = false,
                        MaxStackCount = 1
                    }
                ]
            }
        });
    }

    private static void ValidateDedicatedSlot(Slot slot, string wireName, MongoId id, MongoId allowedParent)
    {
        if (!Equals(slot.Id, id)
            || !Equals(slot.Parent, DefaultInventoryTpl)
            || slot.MaxCount != 1
            || slot.Required == true)
            throw new InvalidOperationException($"B&A&HB dedicated-slot identity collision for wire id {wireName}.");

        var filters = slot.Properties?.Filters?.ToArray();
        var accepted = filters?.Length == 1 ? filters[0].Filter : null;
        if (accepted == null || accepted.Count != 1 || !accepted.Contains(allowedParent))
            throw new InvalidOperationException($"B&A&HB dedicated-slot filter collision for wire id {wireName}.");
    }

    private static void MoveAfter(List<Slot> slots, string movingName, string anchorName)
    {
        var moving = slots.Single(x => string.Equals(x.Name, movingName, StringComparison.Ordinal));
        slots.Remove(moving);
        int anchor = slots.FindIndex(x => string.Equals(x.Name, anchorName, StringComparison.Ordinal));
        if (anchor < 0) throw new InvalidOperationException($"B&A&HB dedicated-slot missing anchor {anchorName}.");
        slots.Insert(anchor + 1, moving);
    }

    private static void MoveBefore(List<Slot> slots, string movingName, string anchorName)
    {
        var moving = slots.Single(x => string.Equals(x.Name, movingName, StringComparison.Ordinal));
        slots.Remove(moving);
        int anchor = slots.FindIndex(x => string.Equals(x.Name, anchorName, StringComparison.Ordinal));
        if (anchor < 0) throw new InvalidOperationException($"B&A&HB dedicated-slot missing anchor {anchorName}.");
        slots.Insert(anchor, moving);
    }

    private static void ValidatePlacement(List<Slot> slots)
    {
        int pockets = slots.FindIndex(x => string.Equals(x.Name, "Pockets", StringComparison.Ordinal));
        int belt = slots.FindIndex(x => string.Equals(x.Name, RuntimeIdentity.DedicatedBeltWireSlotId, StringComparison.Ordinal));
        int backpack = slots.FindIndex(x => string.Equals(x.Name, "Backpack", StringComparison.Ordinal));
        int headBand = slots.FindIndex(x => string.Equals(x.Name, RuntimeIdentity.DedicatedHeadBandWireSlotId, StringComparison.Ordinal));
        int headwear = slots.FindIndex(x => string.Equals(x.Name, "Headwear", StringComparison.Ordinal));

        // The SPT server inventory template is a serialization/filter contract, not
        // the visual ContainersPanel ordering contract. Vanilla 4.1.3 can contain
        // other equipment slots between Pockets and Backpack, so requiring direct
        // adjacency here is an invalid assumption and can abort server startup.
        // Belt must be after Pockets and before Backpack; exact visual adjacency is
        // owned by the bounded client ContainersPanel projection.
        if (pockets < 0 || belt < 0 || backpack < 0 || !(pockets < belt && belt < backpack))
            throw new InvalidOperationException("B&A&HB dedicated Belt placement drifted: Belt must remain after Pockets and before Backpack in the server inventory contract.");
        if (headBand < 0 || headwear < 0 || headBand + 1 != headwear)
            throw new InvalidOperationException("B&A&HB dedicated HeadBand placement drifted: must be immediately before Headwear.");
    }
}
