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
[Injectable(TypePriority = OnLoadOrder.Preload + 4)]
public sealed class DedicatedEquipmentSlotRegistration(
    TemplateTable templateTable,
    ISptLogger<DedicatedEquipmentSlotRegistration> logger) : IOnLoad
{
    private static readonly MongoId DefaultInventoryTpl = new("55d7217a4bdc2d86028b456d");
    private static readonly MongoId BeltSlotMongoId = new(RuntimeIdentity.DedicatedBeltSlotMongoId);
    private static readonly MongoId HeadBandSlotMongoId = new(RuntimeIdentity.DedicatedHeadBandSlotMongoId);
    private static readonly MongoId DedicatedMagazineBeltTpl = new(RuntimeIdentity.DedicatedMagazineBeltItemId);
    private static readonly MongoId EmergencyHeadBandTpl = new(RuntimeIdentity.EmergencyHeadBandItemId);

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // DedicatedWearableItems runs at Preload+3. Never publish slot15/16
            // filters until both exact product templates exist and are therefore
            // safe host targets for the remainder of server startup.
            if (!templateTable.Items.ContainsKey(DedicatedMagazineBeltTpl)
                || !templateTable.Items.ContainsKey(EmergencyHeadBandTpl))
            {
                logger.Warning("B&A&HB dedicated-slot registration skipped safely: dedicated product templates were not both initialized.");
                return Task.CompletedTask;
            }

            if (!templateTable.Items.TryGetValue(DefaultInventoryTpl, out var inventory))
            {
                logger.Warning("B&A&HB dedicated-slot registration skipped safely: default inventory template missing.");
                return Task.CompletedTask;
            }

            var slots = inventory.Properties?.Slots?.ToList();
            if (slots == null)
            {
                logger.Warning("B&A&HB dedicated-slot registration skipped safely: default inventory Slots missing.");
                return Task.CompletedTask;
            }

            var armBand = FindSingleVanillaSlot(slots, "ArmBand");
            if (armBand == null
                || FindSingleVanillaSlot(slots, "Pockets") == null
                || FindSingleVanillaSlot(slots, "Backpack") == null
                || FindSingleVanillaSlot(slots, "Headwear") == null)
            {
                logger.Warning("B&A&HB dedicated-slot registration skipped safely: required vanilla slot boundary is not unique.");
                return Task.CompletedTask;
            }

            if (HasDuplicateWireId(slots, RuntimeIdentity.DedicatedBeltWireSlotId)
                || HasDuplicateWireId(slots, RuntimeIdentity.DedicatedHeadBandWireSlotId))
            {
                logger.Warning("B&A&HB dedicated-slot registration skipped safely: dedicated wire-id collision detected.");
                return Task.CompletedTask;
            }

            // Prepare both contracts before mutating the canonical slot list. If an
            // existing slot15/slot16 collides, validation throws while `slots` is still
            // unchanged; this prevents a half-installed Belt-only/HeadBand-only state.
            Slot? beltAddition = PrepareDedicatedSlot(
                slots,
                armBand,
                RuntimeIdentity.DedicatedBeltWireSlotId,
                BeltSlotMongoId,
                DedicatedMagazineBeltTpl);
            Slot? headBandAddition = PrepareDedicatedSlot(
                slots,
                armBand,
                RuntimeIdentity.DedicatedHeadBandWireSlotId,
                HeadBandSlotMongoId,
                EmergencyHeadBandTpl);

            if (beltAddition != null) slots.Add(beltAddition);
            if (headBandAddition != null) slots.Add(headBandAddition);

            // Server slot-list order is not a UI layout contract. Do not reorder
            // vanilla inventory slots and do not abort startup based on relative
            // list positions. Client presentation owns the requested visual anchors.
            inventory.Properties!.Slots = slots;

            logger.Success($"B&A&HB #2 MOD SPT dedicated equipment slot contracts registered atomically after exact product templates: Belt wire={RuntimeIdentity.DedicatedBeltWireSlotId}; HeadBand wire={RuntimeIdentity.DedicatedHeadBandWireSlotId}. Visual placement is client-owned.");
        }
        catch (Exception exception)
        {
            logger.Error($"B&A&HB dedicated-slot registration failed safely without partial slot mutation: {exception.GetType().FullName}: {exception.Message}");
        }

        return Task.CompletedTask;
    }

    private static Slot? FindSingleVanillaSlot(List<Slot> slots, string name)
    {
        var matches = slots.Where(x => string.Equals(x.Name, name, StringComparison.Ordinal)).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static bool HasDuplicateWireId(List<Slot> slots, string wireName)
    {
        return slots.Count(x => string.Equals(x.Name, wireName, StringComparison.Ordinal)) > 1;
    }

    private static Slot? PrepareDedicatedSlot(List<Slot> slots, Slot armBandPrototype, string wireName, MongoId id, MongoId allowedTemplate)
    {
        var matches = slots.Where(x => string.Equals(x.Name, wireName, StringComparison.Ordinal)).ToArray();
        if (matches.Length == 1)
        {
            ValidateDedicatedSlot(matches[0], wireName, id, allowedTemplate);
            return null;
        }

        return new Slot
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
                Filters = [new SlotFilter { Filter = [allowedTemplate], Locked = false, MaxStackCount = 1 }]
            }
        };
    }

    private static void ValidateDedicatedSlot(Slot slot, string wireName, MongoId id, MongoId allowedTemplate)
    {
        if (!Equals(slot.Id, id) || !Equals(slot.Parent, DefaultInventoryTpl) || slot.MaxCount != 1 || slot.Required == true)
            throw new InvalidOperationException($"B&A&HB dedicated-slot identity collision for wire id {wireName}.");

        var filters = slot.Properties?.Filters?.ToArray();
        var accepted = filters?.Length == 1 ? filters[0].Filter : null;
        if (accepted == null || accepted.Count != 1 || !accepted.Contains(allowedTemplate))
            throw new InvalidOperationException($"B&A&HB dedicated-slot filter collision for wire id {wireName}.");
    }
}
